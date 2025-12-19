// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using Verse;

// namespace RimTalk.Memory
// {
//     /// <summary>
//     /// 记忆向量检索辅助类
//     /// ★ v3.3.20: 为MemoryEntry添加向量检索支持
//     /// </summary>
//     public static class MemoryVectorSearch
//     {
//         /// <summary>
//         /// 使用向量检索增强记忆匹配
//         /// </summary>
//         public static List<MemoryEntry> EnhanceMemoriesWithVectorSearch(
//             string context,
//             List<MemoryEntry> candidates,
//             int maxResults = 10)
//         {
//             // 检查向量服务是否可用
//             if (!AI.SiliconFlowEmbeddingService.IsAvailable())
//             {
//                 Log.Warning("[MemoryVectorSearch] SiliconFlow service not available");
//                 return candidates;
//             }
            
//             try
//             {
//                 // 异步获取上下文向量（同步等待）
//                 var contextEmbeddingTask = AI.SiliconFlowEmbeddingService.GetEmbeddingAsync(context);
//                 contextEmbeddingTask.Wait(TimeSpan.FromSeconds(10));
                
//                 if (!contextEmbeddingTask.IsCompleted || contextEmbeddingTask.Result == null)
//                 {
//                     Log.Warning("[MemoryVectorSearch] Failed to get context embedding");
//                     return candidates;
//                 }
                
//                 var contextEmbedding = contextEmbeddingTask.Result;
                
//                 // 为每个候选记忆计算向量相似度
//                 var scoredMemories = new List<Tuple<MemoryEntry, float>>();
                
//                 foreach (var memory in candidates)
//                 {
//                     try
//                     {
//                         // 获取记忆内容的向量
//                         var memoryEmbeddingTask = AI.SiliconFlowEmbeddingService.GetEmbeddingAsync(memory.content);
//                         memoryEmbeddingTask.Wait(TimeSpan.FromSeconds(5));
                        
//                         if (memoryEmbeddingTask.IsCompleted && memoryEmbeddingTask.Result != null)
//                         {
//                             var memoryEmbedding = memoryEmbeddingTask.Result;
                            
//                             // 计算余弦相似度
//                             float similarity = AI.SiliconFlowEmbeddingService.CosineSimilarity(contextEmbedding, memoryEmbedding);
                            
//                             scoredMemories.Add(new Tuple<MemoryEntry, float>(memory, similarity));
//                         }
//                     }
//                     catch (Exception ex)
//                     {
//                         Log.Warning($"[MemoryVectorSearch] Failed to process memory {memory.id}: {ex.Message}");
//                     }
//                 }
                
//                 // 按相似度降序排序
//                 var sorted = scoredMemories
//                     .OrderByDescending(tuple => tuple.Item2)
//                     .Take(maxResults)
//                     .Select(tuple => tuple.Item1)
//                     .ToList();
                
//                 Log.Message($"[MemoryVectorSearch] Enhanced {sorted.Count} memories from {candidates.Count} candidates");
                
//                 return sorted;
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[MemoryVectorSearch] Error during vector search: {ex.Message}");
//                 return candidates;
//             }
//         }
        
//         /// <summary>
//         /// 计算记忆的向量相似度分数
//         /// </summary>
//         public static float CalculateVectorScore(string context, string memoryContent)
//         {
//             if (!AI.SiliconFlowEmbeddingService.IsAvailable())
//             {
//                 return 0f;
//             }
            
//             try
//             {
//                 // 获取向量并计算相似度（同步等待）
//                 var contextTask = AI.SiliconFlowEmbeddingService.GetEmbeddingAsync(context);
//                 var memoryTask = AI.SiliconFlowEmbeddingService.GetEmbeddingAsync(memoryContent);
                
//                 Task.WaitAll(new Task[] { contextTask, memoryTask }, TimeSpan.FromSeconds(8));
                
//                 if (contextTask.IsCompleted && memoryTask.IsCompleted && 
//                     contextTask.Result != null && memoryTask.Result != null)
//                 {
//                     return AI.SiliconFlowEmbeddingService.CosineSimilarity(contextTask.Result, memoryTask.Result);
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Log.Warning($"[MemoryVectorSearch] Failed to calculate vector score: {ex.Message}");
//             }
            
//             return 0f;
//         }
        
//         /// <summary>
//         /// 混合检索：结合关键词匹配和向量相似度（用于记忆检索）
//         /// </summary>
//         public static float CalculateHybridMemoryScore(
//             MemoryEntry memory,
//             List<string> contextKeywords,
//             float keywordScore,
//             float vectorWeight)
//         {
//             if (!AI.SiliconFlowEmbeddingService.IsAvailable())
//             {
//                 return keywordScore;
//             }
            
//             // 获取向量分数（需要context，这里简化处理）
//             // 实际使用时需要传入完整context
//             float vectorScore = 0f;
            
//             // 混合计算
//             float finalScore = keywordScore * (1f - vectorWeight) + vectorScore * vectorWeight;
            
//             return finalScore;
//         }
//     }
// }
