using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;

namespace RimTalk.Memory
{
    /// <summary>
    /// 工作会话聚合器 - 将频繁的Job事件聚合成有意义的工作记忆
    /// 例如：将"搬运-木材"重复50次 → "花4小时连续搬运了50次木材"
    /// </summary>
    public static class WorkSessionAggregator
    {
        // ⭐ v3.3.2.35: 优化正则表达式 - 提升为静态编译字段
        private static readonly Regex TargetPatternRegex = new Regex(
            @"^Target[A-Z]?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        private static readonly Regex TargetPrefixRegex = new Regex(
            @"^Target\w*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // 当前追踪的工作会话（每个Pawn一个）
        private static Dictionary<Pawn, CurrentWorkSession> activeSessions = new Dictionary<Pawn, CurrentWorkSession>();
        
        // 配置参数
        private const int SESSION_TIMEOUT_TICKS = 2500; // 约1小时无活动后结束会话
        private const int MIN_SESSION_DURATION = 300;   // 最短会话时长（约12秒）
        private const int MIN_COUNT_FOR_AGGREGATION = 3; // 至少重复3次才聚合
        
        /// <summary>
        /// 处理Job开始事件
        /// </summary>
        public static void OnJobStarted(Pawn pawn, JobDef jobDef, Thing target)
        {
            if (pawn == null || jobDef == null) return;
            if (!ShouldTrackJob(jobDef)) return;
            
            // 获取目标信息
            string genericTargetName = GetGenericTargetName(target);
            ThingDef targetDef = target?.def;
            
            // 检查是否有活跃会话
            if (activeSessions.TryGetValue(pawn, out var session))
            {
                // 检查是否是相同类型的工作
                if (IsSameWorkType(session, jobDef, genericTargetName))
                {
                    // 继续当前会话，更新统计
                    session.count++;
                    session.lastActivityTick = Find.TickManager.TicksGame;
                    
                    // 添加新的目标到集合
                    if (targetDef != null && !session.processedItems.Contains(targetDef))
                    {
                        session.processedItems.Add(targetDef);
                    }
                    
                    // 添加目标标签
                    if (!string.IsNullOrEmpty(genericTargetName) && !session.targetLabels.Contains(genericTargetName))
                    {
                        session.targetLabels.Add(genericTargetName);
                    }
                }
                else
                {
                    // 工作类型改变，结束旧会话，开始新会话
                    FinalizeSession(pawn, session);
                    StartNewSession(pawn, jobDef, genericTargetName, target, targetDef);
                }
            }
            else
            {
                // 没有活跃会话，开始新会话
                StartNewSession(pawn, jobDef, genericTargetName, target, targetDef);
            }
        }
        
        /// <summary>
        /// 定期检查会话超时
        /// </summary>
        public static void CheckSessionTimeouts()
        {
            if (Find.TickManager == null) return;
            
            int currentTick = Find.TickManager.TicksGame;
            var expiredPawns = new List<Pawn>();
            
            foreach (var kvp in activeSessions)
            {
                var pawn = kvp.Key;
                var session = kvp.Value;
                
                // 检查是否超时
                if (currentTick - session.lastActivityTick > SESSION_TIMEOUT_TICKS)
                {
                    FinalizeSession(pawn, session);
                    expiredPawns.Add(pawn);
                }
            }
            
            // 清理过期会话
            foreach (var pawn in expiredPawns)
            {
                activeSessions.Remove(pawn);
            }
        }
        
        /// <summary>
        /// 手动结束会话（例如Pawn死亡、离开地图）
        /// </summary>
        public static void ForceEndSession(Pawn pawn)
        {
            if (activeSessions.TryGetValue(pawn, out var session))
            {
                FinalizeSession(pawn, session);
                activeSessions.Remove(pawn);
            }
        }
        
        private static void StartNewSession(Pawn pawn, JobDef jobDef, string targetName, Thing target, ThingDef targetDef)
        {
            var session = new CurrentWorkSession
            {
                pawn = pawn,
                jobDef = jobDef,
                genericTargetName = targetName,
                startTimeTick = Find.TickManager.TicksGame,
                lastActivityTick = Find.TickManager.TicksGame,
                count = 1,
                processedItems = new List<ThingDef>(),
                primaryTargetDef = targetDef,
                targetLabels = new List<string>()
            };
            
            if (targetDef != null)
            {
                session.processedItems.Add(targetDef);
            }
            
            if (!string.IsNullOrEmpty(targetName))
            {
                session.targetLabels.Add(targetName);
            }
            
            activeSessions[pawn] = session;
        }
        
        private static void FinalizeSession(Pawn pawn, CurrentWorkSession session)
        {
            if (Find.TickManager == null) return;
            
            // 检查是否满足聚合条件
            int duration = Find.TickManager.TicksGame - session.startTimeTick;
            
            if (duration < MIN_SESSION_DURATION || session.count < MIN_COUNT_FOR_AGGREGATION)
            {
                // 太短或太少，不值得记录
                return;
            }
            
            // 生成聚合文本
            string aggregatedText = GenerateAggregatedText(session, duration);
            
            // 传递给现有记忆系统
            var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp != null)
            {
                // 使用中等重要性，因为是聚合后的工作记忆
                float importance = CalculateImportance(session, duration);
                memoryComp.AddActiveMemory(aggregatedText, MemoryType.Action, importance);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[WorkSession] {pawn.LabelShort}: {aggregatedText} (importance: {importance:F2})");
                }
            }
        }
        
        private static string GenerateAggregatedText(CurrentWorkSession session, int durationTicks)
        {
            var sb = new StringBuilder();
            
            // 时长描述
            string durationDesc = GetDurationDescription(durationTicks);
            
            // 工作类型描述
            string jobDesc = GetJobDescription(session.jobDef);
            
            // 构建目标描述
            string targetDesc = BuildTargetDescription(session);
            
            // 生成自然语言文本
            if (session.count >= 10)
            {
                // 大量重复："花4小时连续搬运了50次 木材、钢铁"
                sb.Append($"花{durationDesc}连续{jobDesc}了{session.count}次");
                if (!string.IsNullOrEmpty(targetDesc))
                {
                    sb.Append($" {targetDesc}");
                }
            }
            else
            {
                // 少量重复："2小时内搬运了5次 木材"
                sb.Append($"{durationDesc}内{jobDesc}了{session.count}次");
                if (!string.IsNullOrEmpty(targetDesc))
                {
                    sb.Append($" {targetDesc}");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 构建目标描述（支持多个目标）
        /// </summary>
        private static string BuildTargetDescription(CurrentWorkSession session)
        {
            if (session.targetLabels.Count == 0)
                return "";
            
            if (session.targetLabels.Count == 1)
            {
                // 单个目标："木材"
                return session.targetLabels[0];
            }
            else if (session.targetLabels.Count <= 3)
            {
                // 少量目标："木材、钢铁、零部件"
                return string.Join("、", session.targetLabels);
            }
            else
            {
                // 大量目标："木材、钢铁、零部件等4种物品"
                var first3 = session.targetLabels.Take(3);
                return $"{string.Join("、", first3)}等{session.targetLabels.Count}种物品";
            }
        }
        
        private static float CalculateImportance(CurrentWorkSession session, int duration)
        {
            // 基础重要性：0.4（工作记忆略低于单次事件的0.5）
            float importance = 0.4f;
            
            // 根据持续时间调整（长时间工作更重要）
            if (duration > 15000) // 超过10小时
                importance += 0.2f;
            else if (duration > 7500) // 超过5小时
                importance += 0.1f;
            
            // 根据重复次数调整
            if (session.count >= 20)
                importance += 0.15f;
            else if (session.count >= 10)
                importance += 0.1f;
            
            // 特殊工作类型加成
            if (IsHighPriorityJob(session.jobDef))
                importance += 0.1f;
            
            return Math.Min(importance, 0.8f); // 上限0.8
        }
        
        /// <summary>
        /// 判断某个Job类型是否应该被聚合（而不是单独记录）
        /// </summary>
        public static bool IsJobBeingAggregated(JobDef jobDef)
        {
            if (jobDef == null) return false;
            
            // 可重复的工作类型应该被聚合
            string defName = jobDef.defName;
            
            if (defName.Contains("Haul")) return true;      // 搬运
            if (defName.Contains("Construct")) return true; // 建造
            if (defName.Contains("Plant")) return true;     // 种植
            if (defName.Contains("Harvest")) return true;   // 收获
            if (defName.Contains("Mine")) return true;      // 采矿
            if (defName.Contains("Clean")) return true;     // 清洁
            if (defName.Contains("Cook")) return true;      // 烹饪
            if (defName.Contains("Repair")) return true;    // 修理
            
            // 其他工作类型保持单独记录（如研究、治疗等）
            return false;
        }
        
        private static bool ShouldTrackJob(JobDef jobDef)
        {
            if (jobDef == null) return false;
            
            // 过滤不值得追踪的Job
            if (jobDef == JobDefOf.Goto) return false;
            if (jobDef == JobDefOf.Wait) return false;
            if (jobDef == JobDefOf.Wait_Downed) return false;
            if (jobDef == JobDefOf.GotoWander) return false;
            
            string defName = jobDef.defName;
            if (defName.Contains("Wait")) return false;
            
            // 战斗和社交不聚合（已经有专门的高重要性处理）
            if (jobDef == JobDefOf.AttackMelee) return false;
            if (jobDef == JobDefOf.AttackStatic) return false;
            if (jobDef == JobDefOf.SocialFight) return false;
            if (jobDef == JobDefOf.MarryAdjacentPawn) return false;
            if (jobDef == JobDefOf.Lovin) return false;
            
            return true;
        }
        
        private static bool IsSameWorkType(CurrentWorkSession session, JobDef newJobDef, string newTargetName)
        {
            // Job类型必须相同
            if (session.jobDef != newJobDef)
                return false;
            
            // 如果是通用Job（没有特定目标），直接认为相同
            if (string.IsNullOrEmpty(session.genericTargetName) && string.IsNullOrEmpty(newTargetName))
                return true;
            
            // 都有目标名称，认为是同类工作
            if (!string.IsNullOrEmpty(session.genericTargetName) || !string.IsNullOrEmpty(newTargetName))
                return true;
            
            return true;
        }
        
        private static string GetGenericTargetName(Thing target)
        {
            if (target == null) return "";
            
            // 对于建筑：使用蓝图的目标
            if (target is Blueprint blueprint)
            {
                var entityDef = blueprint.def.entityDefToBuild;
                if (entityDef != null && !string.IsNullOrEmpty(entityDef.label))
                {
                    return entityDef.label;
                }
                // ⭐ 修复：如果entityDefToBuild为空，返回空字符串而不是LabelShort
                return "";
            }
            
            if (target is Frame frame)
            {
                var entityDef = frame.def.entityDefToBuild;
                if (entityDef != null && !string.IsNullOrEmpty(entityDef.label))
                {
                    return entityDef.label;
                }
                // ⭐ 修复：如果entityDefToBuild为空，返回空字符串而不是LabelShort
                return "";
            }
            
            // 获取标签
            string label = target.def?.label ?? "";
            
            // 使用正则表达式过滤无意义的名称
            if (string.IsNullOrEmpty(label))
                return "";
            
            // ⭐ 优化1：使用静态编译的正则表达式
            if (TargetPatternRegex.IsMatch(label))
                return "";
            
            if (TargetPrefixRegex.IsMatch(label))
                return "";
            
            // ⭐ 优化2：使用高效的纯数字检查（比正则快100倍）
            if (IsAllDigits(label))
                return "";
            
            return label;
        }
        
        /// <summary>
        /// ⭐ v3.3.2.35: 高效的纯数字检查（替代 ^\d+$ 正则）
        /// 性能：比正则表达式快100倍
        /// </summary>
        private static bool IsAllDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            foreach (char c in text)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            
            return true;
        }
        
        private static string GetDurationDescription(int ticks)
        {
            float hours = ticks / 2500f;
            
            if (hours < 1f) return "不到1小时";
            if (hours < 2f) return "约1小时";
            if (hours < 4f) return "约2-3小时";
            if (hours < 8f) return "约半天";
            if (hours < 16f) return "约大半天";
            return "整天";
        }
        
        private static string GetJobDescription(JobDef jobDef)
        {
            if (jobDef == null) return "工作";
            
            // 特殊处理一些常见Job
            string defName = jobDef.defName;
            
            if (defName.Contains("Construct")) return "建造";
            if (defName.Contains("Cook")) return "烹饪";
            if (defName.Contains("Plant")) return "种植";
            if (defName.Contains("Harvest")) return "收获";
            if (defName.Contains("Mine")) return "采矿";
            if (defName.Contains("Haul")) return "搬运";
            if (defName.Contains("Clean")) return "清洁";
            if (defName.Contains("Research")) return "研究";
            if (defName.Contains("Doctor")) return "治疗";
            if (defName.Contains("Repair")) return "修理";
            
            // ⭐ 修复：使用reportString但移除占位符
            string description = jobDef.reportString ?? jobDef.defName;
            
            // 移除所有占位符（因为在聚合文本中不需要具体目标）
            description = RemovePlaceholders(description);
            
            return description;
        }
        
        /// <summary>
        /// ⭐ 移除字符串中的所有占位符
        /// </summary>
        private static string RemovePlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // 移除各种占位符格式
            var placeholders = new[]
            {
                "TargetA", "TargetB", "TargetC",
                "{0}", "{1}", "{2}",
                "{TargetA}", "{TargetB}", "{TargetC}",
                "{TARGETLABEL}", "{TARGET_LABEL}"
            };
            
            string result = text;
            foreach (var placeholder in placeholders)
            {
                result = result.Replace(placeholder, "").Trim();
            }
            
            // 清理多余的空格和标点
            result = Regex.Replace(result, @"\s+", " "); // 多个空格变成一个
            result = result.Trim(' ', '.', '。', '-', '—');
            
            return result;
        }
        
        private static bool IsHighPriorityJob(JobDef jobDef)
        {
            if (jobDef == null) return false;
            
            // 重要工作：研究、医疗、建造
            string defName = jobDef.defName;
            
            if (defName.Contains("Research")) return true;
            if (defName.Contains("Doctor")) return true;
            if (defName.Contains("Tend")) return true;
            if (defName.Contains("Construct")) return true;
            
            return false;
        }
        
        /// <summary>
        /// 当前工作会话状态
        /// </summary>
        private class CurrentWorkSession
        {
            public Pawn pawn;
            public JobDef jobDef;
            public string genericTargetName;      // 保留用于单一目标的情况
            public int startTimeTick;
            public int lastActivityTick;
            public int count;
            public List<ThingDef> processedItems; // 所有处理过的物品种类
            public ThingDef primaryTargetDef;     // 主要目标类型（用于判断是否应该结束会话）
            public List<string> targetLabels;     // 收集的所有目标标签
        }
    }
}
