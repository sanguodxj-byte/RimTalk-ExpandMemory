using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace RimTalk.Memory
{
    /// <summary>
    /// 动态记忆注入系统 - 类似酒馆的智能注入
    /// 基于时间衰减、重要性和关键词匹配动态评分
    /// </summary>
    public static class DynamicMemoryInjection
    {
        /// <summary>
        /// 权重配置
        /// </summary>
        public static class Weights
        {
            public static float TimeDecay = 0.3f;        // 时间衰减权重
            public static float Importance = 0.3f;       // 重要性权重
            public static float KeywordMatch = 0.4f;     // 关键词匹配权重
            public static float LayerBonus = 0.2f;       // 层级加成（ABM > SCM > ELS > CLPA）
            public static float PinnedBonus = 0.5f;      // 固定记忆加成
            public static float UserEditedBonus = 0.3f;  // 用户编辑加成
        }

        /// <summary>
        /// 动态注入记忆到对话提示词
        /// </summary>
        /// <param name="pawn">目标小人</param>
        /// <param name="context">对话上下文（用于提取关键词）</param>
        /// <param name="maxMemories">最大注入记忆数量</param>
        /// <returns>格式化的记忆提示文本</returns>
        public static string InjectMemories(Pawn pawn, string context, int maxMemories = 10)
        {
            return InjectMemoriesWithDetails(pawn.TryGetComp<FourLayerMemoryComp>(), context, maxMemories, out _);
        }

        /// <summary>
        /// 动态注入记忆（带详细评分信息）- 用于预览
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

            // 提取上下文关键词
            List<string> contextKeywords = ExtractKeywords(context);

            // 收集所有记忆
            var allMemories = new List<MemoryEntry>();
            allMemories.AddRange(memoryComp.ActiveMemories);
            allMemories.AddRange(memoryComp.SituationalMemories);
            allMemories.AddRange(memoryComp.EventLogMemories);
            
            // 如果上下文明确需要，也包含归档记忆
            if (ShouldIncludeArchive(context))
            {
                allMemories.AddRange(memoryComp.ArchiveMemories.Take(20));
            }

            if (allMemories.Count == 0)
                return string.Empty;

            // 计算每个记忆的评分
            var scoredMemories = allMemories
                .Select(m => new ScoredMemory
                {
                    Memory = m,
                    Score = CalculateMemoryScore(m, contextKeywords)
                })
                .OrderByDescending(sm => sm.Score)
                .Take(maxMemories)
                .ToList();

            // 生成详细评分信息
            foreach (var scored in scoredMemories)
            {
                float timeScore = CalculateTimeDecayScore(scored.Memory) * Weights.TimeDecay;
                float importanceScore = scored.Memory.importance * Weights.Importance;
                float keywordScore = CalculateKeywordMatchScore(scored.Memory, contextKeywords) * Weights.KeywordMatch;
                float bonusScore = GetLayerBonus(scored.Memory.layer) * Weights.LayerBonus;
                if (scored.Memory.isPinned) bonusScore += Weights.PinnedBonus;
                if (scored.Memory.isUserEdited) bonusScore += Weights.UserEditedBonus;

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
        /// 计算记忆评分
        /// </summary>
        private static float CalculateMemoryScore(MemoryEntry memory, List<string> contextKeywords)
        {
            float score = 0f;
            int currentTick = Find.TickManager.TicksGame;
            int age = currentTick - memory.timestamp;

            // 1. 时间衰减分数（越新越好，使用指数衰减）
            // 半衰期设置为 1 天（60000 ticks）
            float timeScore = UnityEngine.Mathf.Exp(-age / 60000f);
            score += timeScore * Weights.TimeDecay;

            // 2. 重要性分数
            score += memory.importance * Weights.Importance;

            // 3. 关键词匹配分数
            float keywordScore = CalculateKeywordMatchScore(memory, contextKeywords);
            score += keywordScore * Weights.KeywordMatch;

            // 4. 层级加成（越活跃的层级加成越高）
            float layerBonus = GetLayerBonus(memory.layer);
            score += layerBonus * Weights.LayerBonus;

            // 5. 特殊加成
            if (memory.isPinned)
                score += Weights.PinnedBonus;
            
            if (memory.isUserEdited)
                score += Weights.UserEditedBonus;

            // 6. 活跃度加成
            score += memory.activity * 0.1f;

            return score;
        }

        /// <summary>
        /// 计算时间衰减分数
        /// </summary>
        private static float CalculateTimeDecayScore(MemoryEntry memory)
        {
            int currentTick = Find.TickManager.TicksGame;
            int age = currentTick - memory.timestamp;

            // 使用指数衰减
            // 半衰期设置为 1 天（60000 ticks）
            return UnityEngine.Mathf.Exp(-age / 60000f);
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
        /// 提取上下文关键词（简单的中文分词）
        /// </summary>
        private static List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var keywords = new HashSet<string>();

            // 简单的中文关键词提取（2-4字词语）
            for (int length = 2; length <= 4; length++)
            {
                for (int i = 0; i <= text.Length - length; i++)
                {
                    string word = text.Substring(i, length);
                    
                    // 过滤纯符号和空白
                    if (word.Any(c => char.IsLetterOrDigit(c)))
                    {
                        keywords.Add(word);
                    }
                }
            }

            // 限制关键词数量
            return keywords.Take(20).ToList();
        }

        /// <summary>
        /// 格式化记忆用于注入
        /// </summary>
        private static string FormatMemoriesForInjection(List<ScoredMemory> scoredMemories)
        {
            if (scoredMemories == null || scoredMemories.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("## 角色记忆");
            sb.AppendLine();

            // 按层级分组显示
            var byLayer = scoredMemories.GroupBy(sm => sm.Memory.layer);

            foreach (var group in byLayer.OrderBy(g => g.Key))
            {
                string layerName = GetLayerName(group.Key);
                sb.AppendLine($"### {layerName}");

                foreach (var scored in group)
                {
                    var memory = scored.Memory;
                    
                    // 构建单条记忆
                    string tags = memory.tags.Any() ? $"[{string.Join(", ", memory.tags)}]" : "";
                    string timeStr = memory.TimeAgoString;
                    
                    sb.AppendLine($"- {tags} {memory.content} ({timeStr})");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取层级名称
        /// </summary>
        private static string GetLayerName(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return "当前状态";
                case MemoryLayer.Situational:
                    return "近期经历";
                case MemoryLayer.EventLog:
                    return "重要事件";
                case MemoryLayer.Archive:
                    return "长期记忆";
                default:
                    return "未知";
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
