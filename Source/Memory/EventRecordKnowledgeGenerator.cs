using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 事件记录常识生成器 (PlayLog扫描 - 补充监听)
    /// ? 职责：捕获IncidentPatch无法监听的事件
    /// - 死亡通知（非Incident触发的死亡）
    /// - 关系变化细节（包含参与者名字）
    /// - 其他重要日志（兜底机制）
    /// </summary>
    public static class EventRecordKnowledgeGenerator
    {
        // 已处理的记录ID（防止重复）
        private static HashSet<int> processedLogIDs = new HashSet<int>();
        
        // 重要事件关键词（优先级）
        private static readonly Dictionary<string, float> ImportantKeywords = new Dictionary<string, float>
        {
            // 死亡相关（最重要1.0）
            { "死亡", 1.0f }, { "倒下", 1.0f }, { "被杀", 1.0f }, { "击杀", 1.0f }, { "牺牲", 1.0f },
            { "died", 1.0f }, { "killed", 1.0f }, { "death", 1.0f }, { "dead", 1.0f },
            
            // 战斗相关（重要性0.9）
            { "袭击", 0.9f }, { "进攻", 0.9f }, { "防御", 0.9f }, { "raid", 0.9f }, { "attack", 0.9f },
            { "击退", 0.85f }, { "战胜", 0.85f }, { "defeated", 0.85f },
            
            // ? 新增：葬礼相关（重要性0.9）
            { "葬礼", 0.9f }, { "葬", 0.9f }, { "埋葬", 0.9f }, { "funeral", 0.9f }, { "burial", 0.9f },
            { "举行葬礼", 0.9f }, { "安葬", 0.9f },
            
            // 关系相关（重要性0.85）
            { "结婚", 0.85f }, { "订婚", 0.85f }, { "married", 0.85f }, { "engaged", 0.85f },
            { "婚礼", 0.85f }, { "wedding", 0.85f }, { "举行婚礼", 0.85f },
            { "分手", 0.75f }, { "离婚", 0.75f }, { "breakup", 0.75f },
            
            // ? 新增：生日相关（重要性0.7）
            { "生日", 0.7f }, { "birthday", 0.7f }, { "庆祝", 0.6f }, { "celebration", 0.6f },
            { "过生日", 0.7f }, { "庆祝生日", 0.7f },
            
            // ? 新增：研究突破（重要性0.8）
            { "突破", 0.8f }, { "breakthrough", 0.8f }, { "完成研究", 0.8f }, { "research complete", 0.8f },
            { "研究完成", 0.8f }, { "发明", 0.8f }, { "invention", 0.8f },
            
            // ? 新增：周年纪念（重要性0.7）
            { "周年", 0.7f }, { "anniversary", 0.7f }, { "周年纪念", 0.7f },
            
            // 成员变动（重要性0.8）
            { "加入", 0.8f }, { "逃跑", 0.8f }, { "离开", 0.8f }, { "joined", 0.8f }, { "fled", 0.8f },
            { "招募", 0.75f }, { "recruited", 0.75f }, { "新成员", 0.8f },
            
            // 灾害相关（重要性0.85）
            { "爆炸", 0.85f }, { "烟雾", 0.85f }, { "火灾", 0.85f }, { "explosion", 0.85f }, { "fire", 0.85f },
            { "毒船", 0.85f }, { "龙卷风", 0.85f }, { "tornado", 0.85f },
            { "疾病", 0.85f }, { "饥荒", 0.8f }, { "饿死", 0.8f }, { "starvation", 0.8f },
            
            // ? 新增：其他重要事件
            { "日食", 0.75f }, { "eclipse", 0.75f },
            { "虫族", 0.85f }, { "infestation", 0.85f },
            { "贸易", 0.6f }, { "caravan", 0.6f }, { "visitor", 0.6f },
            { "任务", 0.65f }, { "quest", 0.65f },
        };
        
        /// <summary>
        /// 每小时扫描PlayLog事件
        /// 生成全局共享的殖民地历史常识
        /// </summary>
        public static void ScanRecentPlayLog()
        {
            if (!RimTalkMemoryPatchMod.Settings.enableEventRecordKnowledge)
                return;
            
            try
            {
                var gameHistory = Find.PlayLog;
                if (gameHistory == null)
                    return;
                
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return;
                
                int processedCount = 0;
                int currentTick = Find.TickManager.TicksGame;
                
                // ? 修复：PlayLog.Age是事件发生时的tick（不是时间差）
                // 应该比较：currentTick - entry.Age < GenDate.TicksPerHour
                int oneHourAgoTick = currentTick - GenDate.TicksPerHour;
                
                var recentEntries = gameHistory.AllEntries
                    .Where(e => e != null && e.Age >= oneHourAgoTick) // 事件tick >= 1小时前的tick
                    .OrderByDescending(e => e.Age) // 按时间倒序（最新的优先）
                    .Take(50);
                
                foreach (var logEntry in recentEntries)
                {
                    try
                    {
                        // 使用LogEntry的ID去重
                        int logID = logEntry.GetHashCode();
                        
                        if (processedLogIDs.Contains(logID))
                            continue;
                        
                        processedLogIDs.Add(logID);
                        
                        // 控制集合大小
                        if (processedLogIDs.Count > 2000)
                        {
                            var toRemove = processedLogIDs.Take(1000).ToList();
                            foreach (var id in toRemove)
                            {
                                processedLogIDs.Remove(id);
                            }
                        }
                        
                        // 提取事件信息
                        string eventText = ExtractEventInfo(logEntry);
                        
                        if (!string.IsNullOrEmpty(eventText))
                        {
                            // 检查是否已存在
                            bool exists = library.Entries.Any(e => 
                                e.content.Contains(eventText.Substring(0, Math.Min(15, eventText.Length)))
                            );
                            
                            if (!exists)
                            {
                                // 计算重要性
                                float importance = CalculateImportance(eventText);
                                
                                // ? 创建全局常识（targetPawnId保持默认-1）
                                var entry = new CommonKnowledgeEntry("事件,历史", eventText)
                                {
                                    importance = importance,
                                    isEnabled = true,
                                    isUserEdited = false
                                    // targetPawnId = -1 (默认全局)
                                };
                                
                                library.AddEntry(entry);
                                processedCount++;
                                
                                // ? v3.3.2: 降低日志输出 - 仅DevMode且10%概率
                                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                                {
                                    Log.Message($"[EventRecord] Created global event knowledge: {eventText.Substring(0, Math.Min(50, eventText.Length))}...");
                                }
                            }
                        }
                        // ? v3.3.2: 移除调试日志
                        /*
                        else if (Prefs.DevMode)
                        {
                            // 调试日志已移除
                        }
                        */
                    }
                    catch (Exception ex)
                    {
                        // ? v3.3.2: 仅在DevMode下输出警告
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                        {
                            Log.Warning($"[EventRecord] Error processing log entry: {ex.Message}");
                        }
                    }
                }
                
                // ? v3.3.2: 降低日志输出 - 仅DevMode且10%概率
                if (processedCount > 0 && Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[EventRecord] Processed {processedCount} new PlayLog events");
                }
            }
            catch (Exception ex)
            {
                // ? v3.3.2: 错误日志保留但降低频率
                if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                {
                    Log.Error($"[EventRecord] Error scanning PlayLog: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 从LogEntry提取事件信息
        /// </summary>
        private static string ExtractEventInfo(LogEntry logEntry)
        {
            if (logEntry == null)
                return null;
            
            try
            {
                // 跳过对话类型的日志（已由RimTalk对话记忆处理）
                if (logEntry.GetType().Name == "PlayLogEntry_Interaction")
                {
                    return null;
                }
                
                // ? 修改：允许处理一些重要的非Incident事件，特别是死亡和葬礼
                if (logEntry.GetType().Name == "PlayLogEntry_Incident")
                {
                    // 对于Incident事件，检查是否是需要补充的事件类型
                    string previewText = logEntry.ToGameStringFromPOV(null, false);
                    if (!string.IsNullOrEmpty(previewText))
                    {
                        // 允许处理：死亡、葬礼、结婚等重要事件（作为IncidentPatch的补充）
                        bool isImportantEvent = 
                            previewText.Contains("死亡") || previewText.Contains("died") || previewText.Contains("killed") || 
                            previewText.Contains("dead") || previewText.Contains("death") ||
                            previewText.Contains("葬礼") || previewText.Contains("葬") || previewText.Contains("埋葬") ||
                            previewText.Contains("结婚") || previewText.Contains("婚礼") || previewText.Contains("married") ||
                            previewText.Contains("生日") || previewText.Contains("birthday") ||
                            previewText.Contains("突破") || previewText.Contains("breakthrough");
                        
                        if (!isImportantEvent)
                        {
                            // 其他Incident事件：跳过，避免重复（IncidentPatch已处理）
                            return null;
                        }
                        // 重要事件：继续处理
                    }
                    else
                    {
                        return null;
                    }
                }
                
                string text = logEntry.ToGameStringFromPOV(null, false);
                
                if (string.IsNullOrEmpty(text))
                    return null;
                
                // 过滤长度
                if (text.Length < 10 || text.Length > 200)
                    return null;
                
                // 过滤无聊事件
                if (IsBoringMessage(text))
                    return null;
                
                // ? 增强关键词检测：检查是否包含重要关键词
                bool hasImportantKeyword = ImportantKeywords.Keys.Any(k => text.Contains(k));
                
                if (!hasImportantKeyword)
                {
                    // ? 新增：如果没有匹配重要关键词，但这是Incident事件，也记录（宽松模式）
                    if (logEntry.GetType().Name == "PlayLogEntry_Incident")
                    {
                        // 移除调试日志
                        // 对于Incident事件，即使关键词不匹配也记录（但降低重要性）
                    }
                    else
                    {
                        return null; // 非Incident事件必须有关键词匹配
                    }
                }
                
                // ? 过滤对话内容：如果包含对话标记，跳过
                if (IsConversationContent(text))
                    return null;
                
                // 添加时间前缀
                int ticksAgo = Find.TickManager.TicksGame - logEntry.Age;
                int daysAgo = ticksAgo / GenDate.TicksPerDay;
                
                string timePrefix = "";
                if (daysAgo < 1)
                {
                    timePrefix = "今天";
                }
                else if (daysAgo < 3)
                {
                    timePrefix = $"{daysAgo}天前";
                }
                else if (daysAgo < 7)
                {
                    timePrefix = $"约{daysAgo}天前";
                }
                else
                {
                    return null; // 超过7天的事件不记录
                }
                
                return $"{timePrefix}{text}";
            }
            catch (Exception ex)
            {
                // ? v3.3.2: 移除调试日志
                // if (Prefs.DevMode) { ... }
                return null;
            }
        }
        
        /// <summary>
        /// ? 新增：检测是否是对话内容（避免记录对话）
        /// </summary>
        private static bool IsConversationContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            // 检测对话标记
            string[] conversationMarkers = { 
                "说:", "said:", "说：", "说道:", "说道：",
                "问:", "asked:", "问：", "问道:", "问道：",
                "回答:", "replied:", "回答：", "答道:", "答道：",
                "叫道:", "shouted:", "叫道：", "喊道:", "喊道："
            };
            
            return conversationMarkers.Any(marker => text.Contains(marker));
        }
        
        /// <summary>
        /// 过滤无聊事件
        /// </summary>
        private static bool IsBoringMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;
            
            var boringKeywords = new[] 
            { 
                "走路", "吃饭", "睡觉", "娱乐", "闲逛", "休息",
                "walking", "eating", "sleeping", "recreation", "wandering"
            };
            
            return boringKeywords.Any(k => text.Contains(k));
        }
        
        /// <summary>
        /// 计算事件重要性
        /// </summary>
        private static float CalculateImportance(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0.5f;
            
            // 找到匹配的关键词
            var matched = ImportantKeywords
                .Where(kv => text.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();
            
            if (matched.Key != null)
            {
                return matched.Value;
            }
            
            // ? 新增：没有关键词匹配但可能是Incident事件，给较低默认重要性
            return 0.4f; // 比普通事件低，但仍会被记录
        }
        
        /// <summary>
        /// 清理过期记录（维护性能）
        /// </summary>
        public static void CleanupProcessedRecords()
        {
            // 控制集合大小
            if (processedLogIDs.Count > 2000)
            {
                var toRemove = processedLogIDs.Take(1000).ToList();
                foreach (var id in toRemove)
                {
                    processedLogIDs.Remove(id);
                }
            }
        }
    }
}
