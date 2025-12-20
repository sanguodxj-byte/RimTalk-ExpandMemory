using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 自适应阈值管理器 - 根据评分分布动态调整阈值
    /// v3.0.0
    /// ? v3.3.2.3: 添加日志降频，避免刷屏
    /// </summary>
    public static class AdaptiveThresholdManager
    {
        // 评分历史记录
        private static List<float> memoryScoreHistory = new List<float>();
        private static List<float> knowledgeScoreHistory = new List<float>();

        // ? 日志降频控制
        private static int logCounter = 0;
        private const int LOG_INTERVAL = 100; // 每100次计算才输出一次日志
        
        // 配置参数
        private const int MAX_HISTORY_SIZE = 1000;  // 最大历史记录数
        private const int MIN_SAMPLES = 50;         // 最小样本数（用于统计）
        private const float PERCENTILE_TARGET = 0.20f; // 目标百分位（保留前20%）

        // 阈值调整范围
        private const float MIN_THRESHOLD = 0.05f;
        private const float MAX_THRESHOLD = 0.50f;
        private const float ADJUSTMENT_RATE = 0.05f; // 每次调整幅度

        /// <summary>
        /// 记录记忆评分
        /// </summary>
        public static void RecordMemoryScore(float score)
        {
            if (score < 0 || score > 1)
                return;

            memoryScoreHistory.Add(score);

            // 限制历史记录大小
            if (memoryScoreHistory.Count > MAX_HISTORY_SIZE)
            {
                memoryScoreHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 记录常识评分
        /// </summary>
        public static void RecordKnowledgeScore(float score)
        {
            if (score < 0 || score > 1)
                return;

            knowledgeScoreHistory.Add(score);

            if (knowledgeScoreHistory.Count > MAX_HISTORY_SIZE)
            {
                knowledgeScoreHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 批量记录评分（用于SmartInjectionManager）
        /// </summary>
        public static void RecordScores(
            List<ScoredItem<MemoryEntry>> memoryScores,
            List<ScoredItem<CommonKnowledgeEntry>> knowledgeScores)
        {
            if (memoryScores != null)
            {
                foreach (var item in memoryScores)
                {
                    RecordMemoryScore(item.Score);
                }
            }

            if (knowledgeScores != null)
            {
                foreach (var item in knowledgeScores)
                {
                    RecordKnowledgeScore(item.Score);
                }
            }
        }

        /// <summary>
        /// 获取推荐的记忆阈值
        /// </summary>
        public static float GetRecommendedMemoryThreshold()
        {
            if (memoryScoreHistory.Count < MIN_SAMPLES)
            {
                // 样本不足，返回固定的默认推荐值
                return 0.20f; // 固定推荐值，不随用户调整变化
            }

            return CalculateAdaptiveThreshold(memoryScoreHistory, "Memory");
        }

        /// <summary>
        /// 获取推荐的常识阈值
        /// </summary>
        public static float GetRecommendedKnowledgeThreshold()
        {
            if (knowledgeScoreHistory.Count < MIN_SAMPLES)
            {
                // 样本不足，返回固定的默认推荐值
                return 0.15f; // 固定推荐值，不随用户调整变化
            }

            return CalculateAdaptiveThreshold(knowledgeScoreHistory, "Knowledge");
        }

        /// <summary>
        /// 计算自适应阈值
        /// ? v3.3.2.3: 降频日志输出
        /// </summary>
        private static float CalculateAdaptiveThreshold(List<float> scores, string type)
        {
            // 1. 计算统计数据
            var stats = CalculateStatistics(scores);

            // 2. 基于百分位数计算阈值
            float percentileThreshold = CalculatePercentile(scores, PERCENTILE_TARGET);

            // 3. 基于均值和标准差计算阈值
            float meanThreshold = stats.Mean - (stats.StdDev * 0.5f);

            // 4. 取两者的加权平均
            float recommendedThreshold = (percentileThreshold * 0.7f) + (meanThreshold * 0.3f);

            // 5. 限制在合理范围内
            recommendedThreshold = Math.Max(MIN_THRESHOLD, Math.Min(MAX_THRESHOLD, recommendedThreshold));

            // 6. 获取当前阈值，平滑调整
            float currentThreshold = type == "Memory" 
                ? GetCurrentMemoryThreshold() 
                : GetCurrentKnowledgeThreshold();

            float smoothedThreshold = SmoothAdjustment(currentThreshold, recommendedThreshold);

            // ? 降频日志输出（每100次才输出一次）
            logCounter++;
            if (Prefs.DevMode && logCounter % LOG_INTERVAL == 0)
            {
                Log.Message($"[Adaptive Threshold] {type} - Current: {currentThreshold:F3}, " +
                           $"Recommended: {recommendedThreshold:F3}, Smoothed: {smoothedThreshold:F3} " +
                           $"(Mean: {stats.Mean:F3}, StdDev: {stats.StdDev:F3}, Samples: {scores.Count})");
            }

            return smoothedThreshold;
        }

        /// <summary>
        /// 计算百分位数
        /// </summary>
        private static float CalculatePercentile(List<float> scores, float percentile)
        {
            var sorted = scores.OrderByDescending(s => s).ToList();
            int index = (int)(sorted.Count * percentile);
            index = Math.Max(0, Math.Min(sorted.Count - 1, index));
            return sorted[index];
        }

        /// <summary>
        /// 计算统计数据
        /// </summary>
        private static Statistics CalculateStatistics(List<float> scores)
        {
            if (scores.Count == 0)
                return new Statistics { Mean = 0.2f, StdDev = 0.1f };

            float mean = scores.Average();
            float variance = scores.Select(s => (s - mean) * (s - mean)).Average();
            float stdDev = (float)Math.Sqrt(variance);

            return new Statistics
            {
                Mean = mean,
                StdDev = stdDev,
                Min = scores.Min(),
                Max = scores.Max(),
                Count = scores.Count
            };
        }

        /// <summary>
        /// 平滑调整阈值（避免剧烈波动）
        /// </summary>
        private static float SmoothAdjustment(float current, float target)
        {
            float difference = target - current;
            float adjustment = Math.Sign(difference) * Math.Min(Math.Abs(difference), ADJUSTMENT_RATE);
            return current + adjustment;
        }

        /// <summary>
        /// 自动应用推荐阈值
        /// ? v3.3.2.3: 只在DevMode输出日志
        /// </summary>
        public static void ApplyRecommendedThresholds()
        {
            var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
            if (settings == null)
                return;

            float memoryThreshold = GetRecommendedMemoryThreshold();
            float knowledgeThreshold = GetRecommendedKnowledgeThreshold();

            settings.memoryScoreThreshold = memoryThreshold;
            settings.knowledgeScoreThreshold = knowledgeThreshold;

            // ? 只在DevMode输出日志
            if (Prefs.DevMode)
            {
                Log.Message($"[Adaptive Threshold] Applied - Memory: {memoryThreshold:F3}, Knowledge: {knowledgeThreshold:F3}");
            }
        }

        /// <summary>
        /// 获取当前记忆阈值
        /// </summary>
        private static float GetCurrentMemoryThreshold()
        {
            return RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings?.memoryScoreThreshold ?? 0.20f;
        }

        /// <summary>
        /// 获取当前常识阈值
        /// </summary>
        private static float GetCurrentKnowledgeThreshold()
        {
            return RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings?.knowledgeScoreThreshold ?? 0.15f;
        }

        /// <summary>
        /// 获取诊断报告
        /// </summary>
        public static ThresholdDiagnostics GetDiagnostics()
        {
            var memoryStats = CalculateStatistics(memoryScoreHistory);
            var knowledgeStats = CalculateStatistics(knowledgeScoreHistory);

            return new ThresholdDiagnostics
            {
                MemoryStats = memoryStats,
                KnowledgeStats = knowledgeStats,
                CurrentMemoryThreshold = GetCurrentMemoryThreshold(),
                CurrentKnowledgeThreshold = GetCurrentKnowledgeThreshold(),
                RecommendedMemoryThreshold = GetRecommendedMemoryThreshold(),
                RecommendedKnowledgeThreshold = GetRecommendedKnowledgeThreshold(),
                MemorySampleCount = memoryScoreHistory.Count,
                KnowledgeSampleCount = knowledgeScoreHistory.Count
            };
        }

        /// <summary>
        /// 重置历史记录
        /// ? v3.3.2.3: 只在DevMode输出日志
        /// </summary>
        public static void ResetHistory()
        {
            memoryScoreHistory.Clear();
            knowledgeScoreHistory.Clear();
            
            // ? 只在DevMode输出日志
            if (Prefs.DevMode)
            {
                Log.Message("[Adaptive Threshold] History reset");
            }
        }

        /// <summary>
        /// 导出评分分布（用于分析）
        /// </summary>
        public static ScoreDistribution GetScoreDistribution()
        {
            return new ScoreDistribution
            {
                MemoryScores = new List<float>(memoryScoreHistory),
                KnowledgeScores = new List<float>(knowledgeScoreHistory),
                MemoryBuckets = CreateHistogramBuckets(memoryScoreHistory, 10),
                KnowledgeBuckets = CreateHistogramBuckets(knowledgeScoreHistory, 10)
            };
        }

        /// <summary>
        /// 创建直方图桶
        /// </summary>
        private static Dictionary<string, int> CreateHistogramBuckets(List<float> scores, int bucketCount)
        {
            var buckets = new Dictionary<string, int>();
            if (scores.Count == 0)
                return buckets;

            float bucketSize = 1.0f / bucketCount;

            for (int i = 0; i < bucketCount; i++)
            {
                float lower = i * bucketSize;
                float upper = (i + 1) * bucketSize;
                string key = $"{lower:F2}-{upper:F2}";
                int count = scores.Count(s => s >= lower && s < upper);
                buckets[key] = count;
            }

            return buckets;
        }

        #region 数据结构

        public struct Statistics
        {
            public float Mean;
            public float StdDev;
            public float Min;
            public float Max;
            public int Count;
        }

        public class ThresholdDiagnostics
        {
            public Statistics MemoryStats;
            public Statistics KnowledgeStats;
            public float CurrentMemoryThreshold;
            public float CurrentKnowledgeThreshold;
            public float RecommendedMemoryThreshold;
            public float RecommendedKnowledgeThreshold;
            public int MemorySampleCount;
            public int KnowledgeSampleCount;
        }

        public class ScoreDistribution
        {
            public List<float> MemoryScores;
            public List<float> KnowledgeScores;
            public Dictionary<string, int> MemoryBuckets;
            public Dictionary<string, int> KnowledgeBuckets;
        }

        #endregion
    }
}
