using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 动态记忆注入系统 - 整合场景分析和游戏状态感知
    /// ⭐ v3.3.16: 重构整合SceneAnalyzer + 游戏状态检查
    /// 设计哲学：
    /// - 优先级1：游戏状态（Drafted, Downed, JobState）
    /// - 优先级2：文本分析（SceneAnalyzer）
    /// - 动态权重：根据场景调整检索策略
    /// </summary>
    public static class DynamicMemoryInjection
    {
        /// <summary>
        /// 静态权重配置（用户覆盖和固定记忆）
        /// </summary>
        public static class Weights
        {
            public static float LayerBonus = 0.2f;       // 层级加成
            public static float PinnedBonus = 0.5f;      // 固定记忆加成（绝对优先级）
            public static float UserEditedBonus = 0.3f;  // 用户编辑加成（绝对优先级）
        }

        /// <summary>
        /// ⭐ v4.2: 跨 Pawn 去重用的 conversationId 集合
        /// Key: conversationId, Value: 第一个显示该对话的 Pawn 的 ThingID
        /// 在模板循环 {{ for p in pawns }} 中，用于避免同一轮对话被重复注入
        /// </summary>
        [ThreadStatic]
        private static Dictionary<string, string> _displayedConversations;
        
        /// <summary>
        /// ⭐ v4.2: 上一次调用的时间戳（用于自动检测新的对话上下文）
        /// </summary>
        [ThreadStatic]
        private static int _lastContextTick;
        
        /// <summary>
        /// ⭐ v4.2: 上下文有效期（2秒 = 120 ticks）
        /// 如果距离上次调用超过这个时间，认为是新的对话上下文
        /// </summary>
        private const int CONTEXT_EXPIRE_TICKS = 120;
        
        /// <summary>
        /// ⭐ v4.2: 检查并自动重置上下文（基于时间戳）
        /// 如果距离上次调用超过 CONTEXT_EXPIRE_TICKS，重置去重集合
        /// </summary>
        private static void AutoResetContextIfNeeded()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            
            // 如果距离上次调用超过阈值，或者集合为空，重置上下文
            if (_displayedConversations == null ||
                currentTick - _lastContextTick > CONTEXT_EXPIRE_TICKS)
            {
                _displayedConversations = new Dictionary<string, string>();
                
                if (Prefs.DevMode && _lastContextTick > 0)
                {
                    Log.Message($"[ABM Inject] Auto-reset context (last tick: {_lastContextTick}, current: {currentTick}, diff: {currentTick - _lastContextTick})");
                }
            }
            
            _lastContextTick = currentTick;
        }
        
        /// <summary>
        /// ⭐ v4.2: 兼容旧代码，但不再有实际作用
        /// </summary>
        public static void BeginConversationContext()
        {
            // 现在由 AutoResetContextIfNeeded() 自动管理
        }

        /// <summary>
        /// 动态注入记忆到对话提示词
        /// </summary>
        public static string InjectMemories(Pawn pawn, string context, int maxMemories = 10)
        {
            return InjectMemoriesWithDetails(pawn.TryGetComp<FourLayerMemoryComp>(), context, maxMemories, out _);
        }
        
        /// <summary>
        /// ⭐ v4.2: 注入 ABM（最近N轮记忆），支持跨 Pawn 去重
        /// 格式: "1. [Type] Content (TimeAgo)"
        ///
        /// 去重规则：
        /// - 不同 Pawn 共享同一个 conversationId：只有第一个 Pawn 显示完整对话
        /// - 同一个 Pawn 多次调用：每次都返回相同结果（不做 Pawn 级别去重）
        /// </summary>
        /// <param name="pawn">目标 Pawn</param>
        /// <param name="maxRounds">最大注入条数（从设置读取）</param>
        /// <returns>格式化的记忆文本</returns>
        /*
        public static string InjectABM(Pawn pawn, int maxRounds = -1)
        {
            if (pawn == null) return string.Empty;
            
            string pawnId = pawn.ThingID;
            
            // ⭐ v4.2: 自动检测并重置上下文
            AutoResetContextIfNeeded();
            
            var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null) return string.Empty;
            
            // 使用设置中的默认值
            if (maxRounds < 0)
            {
                maxRounds = RimTalkMemoryPatchMod.Settings?.maxABMInjectionRounds ?? 3;
            }
            
            var abmList = memoryComp.ActiveMemories;
            if (abmList == null || abmList.Count == 0) return string.Empty;
            
            var sb = new StringBuilder();
            int index = 1;
            int roundsAdded = 0;
            
            // 按时间降序遍历 ABM
            foreach (var memory in abmList.OrderByDescending(m => m.timestamp))
            {
                if (roundsAdded >= maxRounds) break;
                
                // ⭐ v4.2: 跨 Pawn 去重：如果这个 conversationId 已经被其他 Pawn 显示过，跳过
                if (!string.IsNullOrEmpty(memory.conversationId))
                {
                    if (_displayedConversations.TryGetValue(memory.conversationId, out string ownerPawnId))
                    {
                        // 如果是同一个 Pawn 的重复调用，允许显示
                        if (ownerPawnId != pawnId)
                        {
                            // 这个对话已经被其他 Pawn 显示过了，跳过
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[ABM Inject] Skipped conversation {memory.conversationId} for {pawn.LabelShort} (already shown by {ownerPawnId})");
                            }
                            continue;
                        }
                        // 同一个 Pawn 的重复调用，继续显示（不跳过）
                    }
                    else
                    {
                        // 标记这个对话由当前 Pawn 显示
                        _displayedConversations[memory.conversationId] = pawnId;
                    }
                }
                
                // ⭐ 使用统一格式：序号. [类型] 内容 (时间)
                string typeTag = GetMemoryTypeTag(memory.type);
                string timeStr = memory.TimeAgoString;
                sb.AppendLine($"{index}. [{typeTag}] {memory.content} ({timeStr})");
                
                index++;
                roundsAdded++;
            }
            
            if (Prefs.DevMode && roundsAdded > 0)
            {
                Log.Message($"[ABM Inject] Added {roundsAdded} ABM entries for {pawn.LabelShort}");
            }
            
            return sb.ToString().TrimEnd();
        }
        */

        /// <summary>
        /// 动态注入记忆（带详细评分信息）- 用于预览
        /// ⭐ v3.3.16: 使用SceneAnalyzer + 游戏状态感知
        /// </summary>
        public static string InjectMemoriesWithDetails(
            FourLayerMemoryComp memoryComp, 
            string context, 
            int maxMemories,
            out List<MemoryScore> scores)
        {
            scores = new List<MemoryScore>();

            if (memoryComp == null)
                return string.Empty;

            // ⭐ v3.3.16: 步骤1 - 获取Pawn对象
            var pawn = memoryComp.parent as Pawn;
            if (pawn == null)
                return string.Empty;

            // ⭐ v3.3.16: 步骤2 - 综合场景识别（游戏状态 + 文本分析）
            SceneType sceneType = DetermineScene(pawn, context);
            
            // ⭐ v3.3.16: 步骤3 - 获取动态权重
            var analysis = SceneAnalyzer.AnalyzeScene(context);
            DynamicWeights sceneWeights = SceneAnalyzer.GetDynamicWeights(sceneType, analysis.Confidence);
            
            // 开发模式日志
            if (Prefs.DevMode)
            {
                Log.Message($"[Memory Injection] Scene: {SceneAnalyzer.GetSceneDisplayName(sceneType)}");
                Log.Message($"[Memory Injection] Confidence: {analysis.Confidence:P0}");
                Log.Message($"[Memory Injection] Weights: {sceneWeights}");
            }

            // 提取上下文关键词
            List<string> contextKeywords = ExtractKeywords(context);

            // 收集记忆：跳过超短期记忆(ABM)，只收集SCM、ELS、CLPA
            var allMemories = new List<MemoryEntry>();
            allMemories.AddRange(memoryComp.SituationalMemories);
            allMemories.AddRange(memoryComp.EventLogMemories);
            
            // 根据场景决定是否包含归档记忆
            // 社交/事件场景更倾向于包含长期记忆（讲故事模式）
            if (sceneType == SceneType.Social || sceneType == SceneType.Event || ShouldIncludeArchive(context))
            {
                allMemories.AddRange(memoryComp.ArchiveMemories.Take(20));
            }

            if (allMemories.Count == 0)
                return string.Empty;

            // 获取阈值设置
            float threshold = RimTalkMemoryPatchMod.Settings?.memoryScoreThreshold ?? 0.15f;

            // ⭐ v3.3.16: 步骤4 - 使用动态权重计算评分
            var scoredMemories = allMemories
                .Select(m => new ScoredMemory
                {
                    Memory = m,
                    Score = CalculateMemoryScore(m, contextKeywords, sceneWeights, memoryComp)
                })
                .Where(sm => sm.Score >= threshold)
                .OrderByDescending(sm => sm.Score)
                .Take(maxMemories)
                .ToList();

            // 如果没有记忆达到阈值，返回null
            if (scoredMemories.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[Memory Injection] No memories met threshold ({threshold:F2}), returning null");
                }
                return null;
            }

            // 生成详细评分信息
            foreach (var scored in scoredMemories)
            {
                float timeScore = CalculateTimeDecayScore(scored.Memory, sceneWeights.TimeDecay, sceneWeights.RecencyWindow);
                float importanceScore = scored.Memory.importance * sceneWeights.Importance;
                float keywordScore = CalculateKeywordMatchScore(scored.Memory, contextKeywords) * sceneWeights.KeywordMatch;
                
                float bonusScore = GetLayerBonus(scored.Memory.layer) * Weights.LayerBonus;
                
                // 关系加成
                float relationshipScore = CalculateRelationshipBonus(scored.Memory, pawn) * sceneWeights.RelationshipBonus;
                bonusScore += relationshipScore;
                
                // ⭐ 用户覆盖：绝对优先级（不受场景影响）
                if (scored.Memory.isPinned) 
                    bonusScore += Weights.PinnedBonus;
                if (scored.Memory.isUserEdited) 
                    bonusScore += Weights.UserEditedBonus;

                scores.Add(new MemoryScore
                {
                    Memory = scored.Memory,
                    TotalScore = scored.Score,
                    TimeScore = timeScore,
                    ImportanceScore = importanceScore,
                    KeywordScore = keywordScore,
                    BonusScore = bonusScore
                });
            }

            // 构建注入文本
            return FormatMemoriesForInjection(scoredMemories);
        }
        
        /// <summary>
        /// ⭐ v3.3.16: 综合场景识别（游戏状态优先 + 文本分析回退）
        /// </summary>
        private static SceneType DetermineScene(Pawn pawn, string context)
        {
            if (pawn == null)
                return SceneType.Neutral;
            
            // ⭐ 优先级1：游戏状态检查（实时状态覆盖文本分析）
            
            // 1.1 战斗状态检查
            if (pawn.Drafted || (pawn.mindState?.enemyTarget != null))
            {
                return SceneType.Combat;
            }
            
            // 1.2 医疗状态检查
            if (pawn.Downed || pawn.health?.State == PawnHealthState.Down)
            {
                return SceneType.Medical;
            }
            
            // 1.3 社交状态检查（Lovin任务）
            if (pawn.CurJob?.def?.defName != null && pawn.CurJob.def.defName.Contains("Lovin"))
            {
                return SceneType.Social;
            }
            
            // 1.4 工作状态检查（当前Job）
            if (pawn.CurJob?.def != null)
            {
                string jobDefName = pawn.CurJob.def.defName;
                
                // 研究任务
                if (jobDefName.Contains("Research"))
                {
                    return SceneType.Research;
                }
                
                // 医疗任务
                if (jobDefName.Contains("Doctor") || jobDefName.Contains("Tend") || jobDefName.Contains("Surgery"))
                {
                    return SceneType.Medical;
                }
                
                // 工作任务（建造、种植、搬运等）
                if (jobDefName.Contains("Construct") || jobDefName.Contains("Plant") || 
                    jobDefName.Contains("Haul") || jobDefName.Contains("Cook") ||
                    jobDefName.Contains("Mine") || jobDefName.Contains("Clean"))
                {
                    return SceneType.Work;
                }
            }
            
            // ⭐ 优先级2：文本分析回退
            if (!string.IsNullOrEmpty(context))
            {
                var analysis = SceneAnalyzer.AnalyzeScene(context);
                return analysis.PrimaryScene;
            }
            
            // 默认：中性
            return SceneType.Neutral;
        }
        
        /// <summary>
        /// ⭐ v3.3.16: 计算记忆评分（使用动态权重）
        /// </summary>
        private static float CalculateMemoryScore(
            MemoryEntry memory, 
            List<string> contextKeywords, 
            DynamicWeights sceneWeights,
            FourLayerMemoryComp memoryComp)
        {
            float score = 0f;

            // 1. 时间衰减分数（根据场景动态调整）
            float timeScore = CalculateTimeDecayScore(memory, sceneWeights.TimeDecay, sceneWeights.RecencyWindow);
            score += timeScore;

            // 2. 重要性分数
            score += memory.importance * sceneWeights.Importance;

            // 3. 关键词匹配分数
            float keywordScore = CalculateKeywordMatchScore(memory, contextKeywords);
            score += keywordScore * sceneWeights.KeywordMatch;

            // 4. 层级加成
            float layerBonus = GetLayerBonus(memory.layer);
            score += layerBonus * Weights.LayerBonus;
            
            // 5. 关系加成
            var pawn = memoryComp.parent as Pawn;
            float relationshipBonus = CalculateRelationshipBonus(memory, pawn);
            score += relationshipBonus * sceneWeights.RelationshipBonus;

            // 6. ⭐ 用户覆盖（绝对优先级，不受场景影响）
            if (memory.isPinned)
                score += Weights.PinnedBonus;
            
            if (memory.isUserEdited)
                score += Weights.UserEditedBonus;

            // 7. 活跃度加成
            score += memory.activity * 0.1f;

            return score;
        }

        /// <summary>
        /// ⭐ v3.3.16: 计算时间衰减分数（支持时间窗口）
        /// </summary>
        private static float CalculateTimeDecayScore(MemoryEntry memory, float decayRate, int recencyWindow)
        {
            int currentTick = Find.TickManager.TicksGame;
            int age = currentTick - memory.timestamp;

            // 超过时间窗口的记忆大幅衰减
            if (age > recencyWindow)
            {
                float excessAge = (age - recencyWindow) / 60000f; // 超出部分（天）
                return UnityEngine.Mathf.Exp(-excessAge * 2.0f) * 0.1f; // 窗口外大幅衰减
            }

            // 窗口内：正常指数衰减
            float normalizedAge = age / 60000f; // 转换为游戏天数
            return UnityEngine.Mathf.Exp(-normalizedAge * decayRate);
        }

        /// <summary>
        /// 计算关键词匹配分数
        /// </summary>
        private static float CalculateKeywordMatchScore(MemoryEntry memory, List<string> contextKeywords)
        {
            if (contextKeywords == null || contextKeywords.Count == 0)
                return 0f;

            if (memory.keywords == null || memory.keywords.Count == 0)
                return 0f;

            // 使用 Jaccard 相似度
            var intersection = memory.keywords.Intersect(contextKeywords).Count();
            var union = memory.keywords.Union(contextKeywords).Count();

            if (union == 0)
                return 0f;

            float jaccardSimilarity = (float)intersection / union;

            // 同时考虑内容直接匹配
            float contentMatch = 0f;
            foreach (var keyword in contextKeywords)
            {
                if (memory.content.Contains(keyword))
                    contentMatch += 0.2f;
            }

            return UnityEngine.Mathf.Min(jaccardSimilarity + contentMatch, 1f);
        }
        
        /// <summary>
        /// 计算关系加成（记忆涉及的人物与当前Pawn的关系）
        /// </summary>
        private static float CalculateRelationshipBonus(MemoryEntry memory, Pawn currentPawn)
        {
            if (string.IsNullOrEmpty(memory.relatedPawnId) || currentPawn == null)
                return 0f;
            
            // 查找关联的Pawn
            Pawn relatedPawn = null;
            foreach (var map in Find.Maps)
            {
                relatedPawn = map.mapPawns.AllPawns.FirstOrDefault(p => 
                    p.ThingID == memory.relatedPawnId || 
                    p.LabelShort == memory.relatedPawnName
                );
                
                if (relatedPawn != null)
                    break;
            }
            
            if (relatedPawn == null)
                return 0f;
            
            // 计算关系亲密度
            if (currentPawn.relations == null || relatedPawn.relations == null)
                return 0f;
            
            // 检查是否有直接关系
            var directRelations = currentPawn.relations.DirectRelations
                .Where(r => r.otherPawn == relatedPawn)
                .ToList();
            
            if (directRelations.Any())
            {
                // 配偶/恋人：高加成
                if (directRelations.Any(r => r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Lover))
                    return 1.0f;
                
                // 家人：中等加成
                if (directRelations.Any(r => r.def == PawnRelationDefOf.Parent || 
                                            r.def == PawnRelationDefOf.Child ||
                                            r.def == PawnRelationDefOf.Sibling))
                    return 0.6f;
                
                // 其他关系：小加成
                return 0.3f;
            }
            
            // 检查好感度
            int opinion = currentPawn.relations.OpinionOf(relatedPawn);
            if (opinion > 50)
                return 0.5f;
            else if (opinion > 20)
                return 0.3f;
            else if (opinion < -20)
                return 0.2f; // 负面关系也值得记忆
            
            return 0f;
        }

        /// <summary>
        /// 获取层级加成
        /// </summary>
        private static float GetLayerBonus(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 1.0f;
                case MemoryLayer.Situational:
                    return 0.7f;
                case MemoryLayer.EventLog:
                    return 0.4f;
                case MemoryLayer.Archive:
                    return 0.2f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 判断是否应该包含归档记忆
        /// </summary>
        private static bool ShouldIncludeArchive(string context)
        {
            if (string.IsNullOrEmpty(context))
                return false;

            // 检测是否提到过去、历史等关键词
            string[] archiveKeywords = { "过去", "以前", "曾经", "记得", "回忆", "历史", "当时", "那时候" };
            
            foreach (var keyword in archiveKeywords)
            {
                if (context.Contains(keyword))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 提取上下文关键词（确定性双重策略）
        /// </summary>
        private static List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            // 截断过长文本
            const int MAX_TEXT_LENGTH = 500;
            if (text.Length > MAX_TEXT_LENGTH)
            {
                text = text.Substring(0, MAX_TEXT_LENGTH);
            }

            // 使用超级关键词引擎获取候选词
            var weightedKeywords = SuperKeywordEngine.ExtractKeywords(text, 100);
            
            if (weightedKeywords.Count == 0)
            {
                return new List<string>();
            }
            
            // 核心词：按长度降序 + 字母顺序升序，取前10个
            var sortedByLength = weightedKeywords
                .OrderByDescending(kw => kw.Word.Length)
                .ThenBy(kw => kw.Word, StringComparer.Ordinal)
                .ToList();
            
            var coreKeywords = sortedByLength.Take(10).ToList();
            
            // 模糊词：从剩余池按字母顺序选10个
            var remainingPool = sortedByLength.Skip(10).ToList();
            var fuzzyKeywords = new List<WeightedKeyword>();
            
            if (remainingPool.Count > 0)
            {
                fuzzyKeywords = remainingPool
                    .OrderBy(kw => kw.Word, StringComparer.Ordinal)
                    .Take(10)
                    .ToList();
            }
            
            // 合并核心词 + 模糊词（最多20个）
            var finalKeywords = new List<string>();
            finalKeywords.AddRange(coreKeywords.Select(kw => kw.Word));
            finalKeywords.AddRange(fuzzyKeywords.Select(kw => kw.Word));
            
            return finalKeywords;
        }

        /// <summary>
        /// 格式化记忆用于注入到System Rule
        /// ⭐ v3.4.0: ELS和CLPA使用游戏日期时间戳，SCM使用模糊时间
        /// </summary>
        private static string FormatMemoriesForInjection(List<ScoredMemory> scoredMemories)
        {
            if (scoredMemories == null || scoredMemories.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            // 按层级分组显示
            var byLayer = scoredMemories.GroupBy(sm => sm.Memory.layer);

            int index = 1;
            foreach (var group in byLayer.OrderBy(g => g.Key))
            {
                foreach (var scored in group)
                {
                    var memory = scored.Memory;
                    
                    // 简洁格式，适合system rule
                    string typeTag = GetMemoryTypeTag(memory.type);
                    
                    // ⭐ v3.4.0: ELS(EventLog)和CLPA(Archive)使用游戏日期时间戳
                    // SCM(Situational)和ABM(Active)使用模糊时间
                    string timeStr;
                    if (memory.layer == MemoryLayer.EventLog || memory.layer == MemoryLayer.Archive)
                    {
                        timeStr = memory.GameDateString;
                    }
                    else
                    {
                        timeStr = memory.TimeAgoString;
                    }
                    
                    sb.AppendLine($"{index}. [{typeTag}] {memory.content} ({timeStr})");
                    index++;
                }
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// 获取记忆类型标签
        /// </summary>
        private static string GetMemoryTypeTag(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Conversation:
                    return "Conversation";
                case MemoryType.Action:
                    return "Action";
                case MemoryType.Observation:
                    return "Observation";
                case MemoryType.Event:
                    return "Event";
                case MemoryType.Emotion:
                    return "Emotion";
                case MemoryType.Relationship:
                    return "Relationship";
                default:
                    return "Memory";
            }
        }

        /// <summary>
        /// 评分后的记忆
        /// </summary>
        private class ScoredMemory
        {
            public MemoryEntry Memory;
            public float Score;
        }

        /// <summary>
        /// 记忆评分详情
        /// </summary>
        public class MemoryScore
        {
            public MemoryEntry Memory;
            public float TotalScore;
            public float TimeScore;
            public float ImportanceScore;
            public float KeywordScore;
            public float BonusScore;
        }
    }
}
