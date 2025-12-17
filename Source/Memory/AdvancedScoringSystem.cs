using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 高级评分系统 v3.0
    /// 
    /// 设计目标：
    /// 1. 上下文相关性优先（而不是简单的关键词匹配）
    /// 2. 语义层级匹配（主题 > 实体 > 细节）
    /// 3. 动态权重调整（根据对话场景）
    /// 4. 去重和多样性平衡
    /// </summary>
    public static class AdvancedScoringSystem
    {
        /// <summary>
        /// 场景类型（自动识别对话场景）
        /// </summary>
        public enum SceneType
        {
            Casual,         // 日常闲聊
            EmotionalTalk,  // 情感交流
            WorkDiscussion, // 工作讨论
            HistoryRecall,  // 回忆过去
            Emergency,      // 紧急情况
            Introduction    // 自我介绍
        }

        /// <summary>
        /// 评分维度权重配置
        /// </summary>
        public class ScoringWeights
        {
            // 核心维度
            public float ContextRelevance = 0.40f;  // 上下文相关性（最重要）
            public float Recency = 0.20f;           // 时间新鲜度
            public float Importance = 0.20f;        // 重要性
            public float Diversity = 0.10f;         // 多样性加成
            public float LayerPriority = 0.10f;     // 层级优先级

            // 调节因子
            public float EmotionalBoost = 1.3f;     // 情感记忆加成
            public float RelationshipBoost = 1.2f;  // 关系记忆加成
            public float ConversationBoost = 1.1f;  // 对话记忆加成
        }

        private static ScoringWeights defaultWeights = new ScoringWeights();

        /// <summary>
        /// 智能评分记忆（主入口）
        /// </summary>
        public static List<ScoredItem<MemoryEntry>> ScoreMemories(
            List<MemoryEntry> memories,
            string context,
            Pawn speaker = null,
            Pawn listener = null)
        {
            if (memories == null || memories.Count == 0)
                return new List<ScoredItem<MemoryEntry>>();

            // 1. 自动识别场景类型
            SceneType scene = IdentifyScene(context);

            // 2. 根据场景调整权重
            ScoringWeights weights = AdjustWeightsForScene(scene, defaultWeights);

            // 3. 提取上下文特征
            ContextFeatures features = ExtractContextFeatures(context, speaker, listener);

            // 4. 对每条记忆评分
            var scored = new List<ScoredItem<MemoryEntry>>();
            foreach (var memory in memories)
            {
                float score = ScoreSingleMemory(memory, features, weights, speaker, listener);
                
                scored.Add(new ScoredItem<MemoryEntry>
                {
                    Item = memory,
                    Score = score,
                    Breakdown = new ScoreBreakdown
                    {
                        ContextRelevance = CalculateContextRelevance(memory, features),
                        Recency = CalculateRecency(memory),
                        Importance = memory.importance,
                        Diversity = 0f, // 稍后计算
                        LayerPriority = GetLayerPriority(memory.layer)
                    }
                });
            }

            // 5. 应用多样性调整
            ApplyDiversityBoost(scored);

            // 6. 排序并返回
            return scored.OrderByDescending(s => s.Score).ToList();
        }

        /// <summary>
        /// 智能评分常识（主入口）
        /// </summary>
        public static List<ScoredItem<CommonKnowledgeEntry>> ScoreKnowledge(
            List<CommonKnowledgeEntry> knowledge,
            string context,
            Pawn speaker = null,
            Pawn listener = null)
        {
            if (knowledge == null || knowledge.Count == 0)
                return new List<ScoredItem<CommonKnowledgeEntry>>();

            ContextFeatures features = ExtractContextFeatures(context, speaker, listener);
            var scored = new List<ScoredItem<CommonKnowledgeEntry>>();

            foreach (var entry in knowledge)
            {
                if (!entry.isEnabled)
                    continue;

                float score = ScoreSingleKnowledge(entry, features, speaker);
                
                scored.Add(new ScoredItem<CommonKnowledgeEntry>
                {
                    Item = entry,
                    Score = score,
                    Breakdown = new ScoreBreakdown
                    {
                        ContextRelevance = CalculateKnowledgeRelevance(entry, features),
                        Importance = entry.importance,
                        Diversity = 0f
                    }
                });
            }

            ApplyDiversityBoost(scored);

            return scored.OrderByDescending(s => s.Score).ToList();
        }

        #region 场景识别

        /// <summary>
        /// 自动识别对话场景
        /// </summary>
        private static SceneType IdentifyScene(string context)
        {
            if (string.IsNullOrEmpty(context))
                return SceneType.Casual;

            context = context.ToLower();

            // 紧急情况关键词
            if (ContainsAny(context, new[] { "袭击", "敌人", "危险", "受伤", "死", "快", "救" }))
                return SceneType.Emergency;

            // 历史回忆关键词
            if (ContainsAny(context, new[] { "过去", "以前", "曾经", "记得", "那时", "当时" }))
                return SceneType.HistoryRecall;

            // 情感交流关键词
            if (ContainsAny(context, new[] { "感觉", "心情", "难过", "开心", "想", "喜欢", "讨厌" }))
                return SceneType.EmotionalTalk;

            // 工作讨论关键词
            if (ContainsAny(context, new[] { "工作", "任务", "建造", "种植", "研究", "搬运" }))
                return SceneType.WorkDiscussion;

            // 自我介绍关键词
            if (ContainsAny(context, new[] { "你是", "叫什么", "来自", "背景", "擅长" }))
                return SceneType.Introduction;

            return SceneType.Casual;
        }

        /// <summary>
        /// 根据场景调整权重
        /// </summary>
        private static ScoringWeights AdjustWeightsForScene(SceneType scene, ScoringWeights baseWeights)
        {
            var adjusted = new ScoringWeights
            {
                ContextRelevance = baseWeights.ContextRelevance,
                Recency = baseWeights.Recency,
                Importance = baseWeights.Importance,
                Diversity = baseWeights.Diversity,
                LayerPriority = baseWeights.LayerPriority
            };

            switch (scene)
            {
                case SceneType.Emergency:
                    // 紧急情况：最新信息最重要
                    adjusted.Recency = 0.50f;
                    adjusted.ContextRelevance = 0.30f;
                    adjusted.Importance = 0.15f;
                    break;

                case SceneType.HistoryRecall:
                    // 回忆过去：降低时间因素，提高归档层级
                    adjusted.Recency = 0.10f;
                    adjusted.LayerPriority = 0.25f;
                    adjusted.ContextRelevance = 0.45f;
                    break;

                case SceneType.EmotionalTalk:
                    // 情感交流：提高情感记忆权重
                    adjusted.EmotionalBoost = 1.5f;
                    adjusted.RelationshipBoost = 1.4f;
                    break;

                case SceneType.WorkDiscussion:
                    // 工作讨论：行动记忆优先
                    adjusted.ContextRelevance = 0.50f;
                    adjusted.Recency = 0.25f;
                    break;

                case SceneType.Introduction:
                    // 自我介绍：归档记忆（长期经历）更重要
                    adjusted.LayerPriority = 0.30f;
                    adjusted.ContextRelevance = 0.35f;
                    adjusted.Importance = 0.25f;
                    break;
            }

            return adjusted;
        }

        #endregion

        #region 上下文特征提取

        /// <summary>
        /// 上下文特征
        /// </summary>
        public class ContextFeatures
        {
            public List<string> Keywords = new List<string>();      // 关键词
            public List<string> Entities = new List<string>();      // 实体（人名、地名）
            public List<string> Topics = new List<string>();        // 主题标签
            public HashSet<string> EmotionWords = new HashSet<string>(); // 情感词
            public SceneType Scene = SceneType.Casual;
        }

        /// <summary>
        /// 提取上下文特征（增强版）
        /// </summary>
        private static ContextFeatures ExtractContextFeatures(string context, Pawn speaker, Pawn listener)
        {
            var features = new ContextFeatures
            {
                Scene = IdentifyScene(context)
            };

            if (string.IsNullOrEmpty(context))
                return features;

            // 1. 提取关键词（改进版：使用TF-IDF思想）
            features.Keywords = ExtractImportantKeywords(context);

            // 2. 提取实体（人名）
            if (speaker != null)
                features.Entities.Add(speaker.LabelShort);
            if (listener != null && listener != speaker)
                features.Entities.Add(listener.LabelShort);

            // 3. 提取主题
            features.Topics = ExtractTopics(context);

            // 4. 提取情感词
            features.EmotionWords = ExtractEmotionWords(context);

            return features;
        }

        /// <summary>
        /// 提取重要关键词（改进版）
        /// </summary>
        private static List<string> ExtractImportantKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var keywords = new HashSet<string>();
            
            // 提取2-4字词语
            for (int length = 2; length <= 4; length++)
            {
                for (int i = 0; i <= text.Length - length; i++)
                {
                    string word = text.Substring(i, length);
                    if (word.Any(c => char.IsLetterOrDigit(c)))
                    {
                        keywords.Add(word);
                    }
                }
            }

            // 过滤常见停用词
            var stopWords = new HashSet<string> { "的", "了", "在", "是", "我", "你", "他", "她", "它", "们", "这", "那" };
            keywords.RemoveWhere(k => stopWords.Contains(k));

            return keywords.Take(30).ToList();
        }

        /// <summary>
        /// 提取主题标签
        /// </summary>
        private static List<string> ExtractTopics(string context)
        {
            var topics = new List<string>();

            // 工作相关
            if (ContainsAny(context, new[] { "工作", "任务", "建造", "种植" }))
                topics.Add("工作");

            // 战斗相关
            if (ContainsAny(context, new[] { "战斗", "袭击", "敌人", "武器" }))
                topics.Add("战斗");

            // 社交相关
            if (ContainsAny(context, new[] { "聊天", "朋友", "关系", "喜欢" }))
                topics.Add("社交");

            // 健康相关
            if (ContainsAny(context, new[] { "受伤", "治疗", "生病", "健康" }))
                topics.Add("健康");

            return topics;
        }

        /// <summary>
        /// 提取情感词
        /// </summary>
        private static HashSet<string> ExtractEmotionWords(string context)
        {
            var emotions = new HashSet<string>();
            var emotionKeywords = new[] 
            { 
                "开心", "高兴", "快乐", "愉快",
                "难过", "悲伤", "伤心", "痛苦",
                "愤怒", "生气", "恼火", "讨厌",
                "害怕", "恐惧", "担心", "焦虑"
            };

            foreach (var word in emotionKeywords)
            {
                if (context.Contains(word))
                    emotions.Add(word);
            }

            return emotions;
        }

        #endregion

        #region 评分计算

        /// <summary>
        /// 计算单条记忆的分数
        /// </summary>
        private static float ScoreSingleMemory(
            MemoryEntry memory,
            ContextFeatures features,
            ScoringWeights weights,
            Pawn speaker,
            Pawn listener)
        {
            float baseScore = 0f;

            // 1. 上下文相关性（最重要）
            float relevance = CalculateContextRelevance(memory, features);
            baseScore += relevance * weights.ContextRelevance;

            // 2. 时间新鲜度
            float recency = CalculateRecency(memory);
            baseScore += recency * weights.Recency;

            // 3. 重要性
            baseScore += memory.importance * weights.Importance;

            // 4. 层级优先级（修复：确保层级加成被计算）
            float layerScore = GetLayerPriority(memory.layer);
            baseScore += layerScore * weights.LayerPriority;

            // 5. 类型加成（修复：使用乘法而不是替换）
            float typeBoost = GetTypeBoost(memory.type, weights);
            float finalScore = baseScore * typeBoost;

            // 6. 特殊标记加成
            if (memory.isPinned)
                finalScore *= 1.5f;
            if (memory.isUserEdited)
                finalScore *= 1.3f;

            // 7. 相关人物加成
            if (!string.IsNullOrEmpty(memory.relatedPawnName))
            {
                if (listener != null && memory.relatedPawnName == listener.LabelShort)
                    finalScore *= 1.2f; // 与对话对象相关
            }

            return finalScore;
        }

        /// <summary>
        /// 计算单条常识的分数
        /// </summary>
        private static float ScoreSingleKnowledge(
            CommonKnowledgeEntry entry,
            ContextFeatures features,
            Pawn speaker)
        {
            float score = 0f;

            // 1. 上下文相关性
            float relevance = CalculateKnowledgeRelevance(entry, features);
            score += relevance * 0.6f;

            // 2. 重要性
            score += entry.importance * 0.4f;

            return score;
        }

        /// <summary>
        /// 计算上下文相关性（核心算法）
        /// </summary>
        private static float CalculateContextRelevance(MemoryEntry memory, ContextFeatures features)
        {
            float relevance = 0f;
            int matches = 0;
            int total = 0;

            // 1. 关键词匹配（使用改进的相似度算法）
            if (memory.keywords != null && memory.keywords.Count > 0)
            {
                var intersection = memory.keywords.Intersect(features.Keywords).Count();
                if (features.Keywords.Count > 0)
                {
                    relevance += (float)intersection / features.Keywords.Count * 0.4f;
                    matches += intersection;
                    total += features.Keywords.Count;
                }
            }

            // 2. 内容直接匹配
            foreach (var keyword in features.Keywords.Take(10)) // 只检查前10个最重要的
            {
                if (memory.content.Contains(keyword))
                {
                    relevance += 0.1f;
                }
            }

            // 3. 主题匹配
            foreach (var topic in features.Topics)
            {
                if (memory.tags.Contains(topic) || memory.content.Contains(topic))
                {
                    relevance += 0.15f;
                }
            }

            // 4. 情感匹配
            if (memory.type == MemoryType.Emotion)
            {
                foreach (var emotion in features.EmotionWords)
                {
                    if (memory.content.Contains(emotion))
                    {
                        relevance += 0.2f;
                    }
                }
            }

            return Math.Min(relevance, 1.0f);
        }

        /// <summary>
        /// 计算常识相关性
        /// </summary>
        private static float CalculateKnowledgeRelevance(CommonKnowledgeEntry entry, ContextFeatures features)
        {
            float relevance = 0f;

            // 标签匹配
            var tags = entry.GetTags();
            foreach (var tag in tags)
            {
                if (features.Topics.Any(t => t.Contains(tag) || tag.Contains(t)))
                {
                    relevance += 0.3f;
                }
            }

            // 内容关键词匹配
            int matchCount = 0;
            foreach (var keyword in features.Keywords.Take(15))
            {
                if (entry.content.Contains(keyword))
                {
                    matchCount++;
                }
            }

            if (features.Keywords.Count > 0)
            {
                relevance += (float)matchCount / features.Keywords.Count * 0.5f;
            }

            return Math.Min(relevance, 1.0f);
        }

        /// <summary>
        /// 计算时间新鲜度
        /// </summary>
        private static float CalculateRecency(MemoryEntry memory)
        {
            int currentTick = Find.TickManager.TicksGame;
            int age = currentTick - memory.timestamp;

            // 使用分段衰减（更符合人类记忆规律）
            if (age < 2500) // < 1小时
                return 1.0f;
            else if (age < 15000) // < 6小时
                return 0.9f;
            else if (age < 60000) // < 1天
                return 0.7f;
            else if (age < 300000) // < 5天
                return 0.5f;
            else if (age < 900000) // < 15天
                return 0.3f;
            else
                return 0.1f;
        }

        /// <summary>
        /// 获取层级优先级
        /// </summary>
        private static float GetLayerPriority(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 1.0f; // ABM: 最高
                case MemoryLayer.Situational:
                    return 0.8f; // SCM
                case MemoryLayer.EventLog:
                    return 0.5f; // ELS
                case MemoryLayer.Archive:
                    return 0.3f; // CLPA
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 获取类型加成
        /// </summary>
        private static float GetTypeBoost(MemoryType type, ScoringWeights weights)
        {
            switch (type)
            {
                case MemoryType.Emotion:
                    return weights.EmotionalBoost;
                case MemoryType.Relationship:
                    return weights.RelationshipBoost;
                case MemoryType.Conversation:
                    return weights.ConversationBoost;
                default:
                    return 1.0f;
            }
        }

        #endregion

        #region 多样性优化

        /// <summary>
        /// 应用多样性加成（避免注入内容过于单一）
        /// </summary>
        private static void ApplyDiversityBoost<T>(List<ScoredItem<T>> scored) where T : class
        {
            if (scored.Count == 0)
                return;

            var typeCount = new Dictionary<string, int>();

            foreach (var item in scored)
            {
                string type = GetItemType(item.Item);
                
                if (!typeCount.ContainsKey(type))
                    typeCount[type] = 0;

                typeCount[type]++;

                // 同类型越多，加成越少
                float diversityFactor = 1.0f / (1.0f + typeCount[type] * 0.1f);
                item.Breakdown.Diversity = diversityFactor - 1.0f;
                item.Score *= diversityFactor;
            }
        }

        private static string GetItemType(object item)
        {
            if (item is MemoryEntry memory)
                return memory.type.ToString();
            else if (item is CommonKnowledgeEntry knowledge)
                return knowledge.GetTags().FirstOrDefault() ?? "unknown";
            else
                return "unknown";
        }

        #endregion

        #region 辅助方法

        private static bool ContainsAny(string text, string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword))
                    return true;
            }
            return false;
        }

        #endregion
    }

    #region 数据结构

    /// <summary>
    /// 评分后的项目
    /// </summary>
    public class ScoredItem<T> where T : class
    {
        public T Item;
        public float Score;
        public ScoreBreakdown Breakdown;
    }

    /// <summary>
    /// 评分细分
    /// </summary>
    public class ScoreBreakdown
    {
        public float ContextRelevance;  // 上下文相关性
        public float Recency;           // 时间新鲜度
        public float Importance;        // 重要性
        public float Diversity;         // 多样性
        public float LayerPriority;     // 层级优先级
        public float TypeBoost;         // 类型加成
    }

    #endregion
}
