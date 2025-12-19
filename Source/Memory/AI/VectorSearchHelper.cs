using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimTalk.Memory.AI;

namespace RimTalk.Memory
{
    /// <summary>
    /// 向量检索辅助类
    /// ★ v3.3.20: 封装向量检索逻辑，避免主文件过于复杂
    /// </summary>
    public static class VectorSearchHelper
    {
        /// <summary>
        /// 使用向量检索增强常识匹配
        /// 返回带有向量相似度加成的常识列表
        /// </summary>
        public static List<CommonKnowledgeEntry> EnhanceWithVectorSearch(
            string context,
            List<CommonKnowledgeEntry> candidates,
            int maxResults = 5)
        {
            // 检查向量服务是否可用
            if (!SiliconFlowEmbeddingService.IsAvailable())
            {
                Log.Warning("[VectorSearch] SiliconFlow service not available");
                return candidates;
            }
            
            try
            {
                // 异步获取上下文向量（同步等待）
                var contextEmbeddingTask = SiliconFlowEmbeddingService.GetEmbeddingAsync(context);
                contextEmbeddingTask.Wait(TimeSpan.FromSeconds(10)); // 10秒超时
                
                if (!contextEmbeddingTask.IsCompleted || contextEmbeddingTask.Result == null)
                {
                    Log.Warning("[VectorSearch] Failed to get context embedding");
                    return candidates;
                }
                
                var contextEmbedding = contextEmbeddingTask.Result;
                
                // 为每个候选常识计算向量相似度
                var scoredEntries = new List<Tuple<CommonKnowledgeEntry, float>>();
                
                foreach (var entry in candidates)
                {
                    try
                    {
                        // 获取常识内容的向量
                        var entryEmbeddingTask = SiliconFlowEmbeddingService.GetEmbeddingAsync(entry.content);
                        entryEmbeddingTask.Wait(TimeSpan.FromSeconds(5));
                        
                        if (entryEmbeddingTask.IsCompleted && entryEmbeddingTask.Result != null)
                        {
                            var entryEmbedding = entryEmbeddingTask.Result;
                            
                            // 计算余弦相似度
                            float similarity = SiliconFlowEmbeddingService.CosineSimilarity(contextEmbedding, entryEmbedding);
                            
                            scoredEntries.Add(new Tuple<CommonKnowledgeEntry, float>(entry, similarity));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[VectorSearch] Failed to process entry {entry.id}: {ex.Message}");
                    }
                }
                
                // 按相似度降序排序
                var sorted = scoredEntries
                    .OrderByDescending(tuple => tuple.Item2)
                    .Take(maxResults)
                    .Select(tuple => tuple.Item1)
                    .ToList();
                
                Log.Message($"[VectorSearch] Enhanced {sorted.Count} entries from {candidates.Count} candidates");
                
                return sorted;
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorSearch] Error during vector search: {ex.Message}");
                return candidates;
            }
        }
        
        /// <summary>
        /// 计算常识的向量相似度分数（用于混合检索）
        /// </summary>
        public static float CalculateVectorScore(string context, string entryContent)
        {
            if (!SiliconFlowEmbeddingService.IsAvailable())
            {
                return 0f;
            }
            
            try
            {
                // 获取向量并计算相似度（同步等待）
                var contextTask = SiliconFlowEmbeddingService.GetEmbeddingAsync(context);
                var entryTask = SiliconFlowEmbeddingService.GetEmbeddingAsync(entryContent);
                
                Task.WaitAll(new Task[] { contextTask, entryTask }, TimeSpan.FromSeconds(8));
                
                if (contextTask.IsCompleted && entryTask.IsCompleted && 
                    contextTask.Result != null && entryTask.Result != null)
                {
                    return SiliconFlowEmbeddingService.CosineSimilarity(contextTask.Result, entryTask.Result);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[VectorSearch] Failed to calculate vector score: {ex.Message}");
            }
            
            return 0f;
        }
        
        /// <summary>
        /// 混合检索：结合关键词匹配和向量相似度
        /// hybridBalance: 0=纯关键词, 1=纯向量, 0.5=各占一半
        /// </summary>
        public static float CalculateHybridScore(
            float keywordScore, 
            float vectorScore, 
            float hybridBalance)
        {
            // 归一化到[0,1]范围
            float normalizedKeyword = Math.Min(1f, Math.Max(0f, keywordScore));
            float normalizedVector = Math.Min(1f, Math.Max(0f, vectorScore));
            
            // 线性插值
            return normalizedKeyword * (1f - hybridBalance) + normalizedVector * hybridBalance;
        }
    }
}
