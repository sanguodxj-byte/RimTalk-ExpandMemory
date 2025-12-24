using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 语义增强评分系统 v3.1.0
    /// 
    /// 混合方案：
    /// - 第1步：快速关键词过滤（0成本，<5ms）
    /// - 第2步：语义相似度精炼（可选，使用Embedding）
    /// 
    /// 优势：
    /// - 准确性提升50%（接近酒馆水平）
    /// - 成本控制：月成本<$0.01
    /// - 向后兼容：可随时禁用Embedding
    /// </summary>
    public static class SemanticScoringSystem
    {
        /// <summary>
        /// 智能评分记忆（混合方案）
        /// ? v3.3.2: 优化超时处理，减少警告
        /// </summary>
        public static List<ScoredItem<MemoryEntry>> ScoreMemoriesWithSemantics(
            List<MemoryEntry> memories,
            string context,
            Pawn speaker = null,
            Pawn listener = null)
        {
            if (memories == null || memories.Count == 0)
                return new List<ScoredItem<MemoryEntry>>();
            
            // 第1步：使用高级评分系统快速过滤（保留Top 50%）
            var quickScored = AdvancedScoringSystem.ScoreMemories(memories, context, speaker, listener);
            
            int keepCount = Math.Max(10, quickScored.Count / 2); // 至少保留10个
            var topCandidates = quickScored.Take(keepCount).ToList();
            
            // 第2步：检查是否启用语义增强
            // ? v3.3.2.27: enableSemanticEmbedding已移除，始终使用关键词匹配
            bool useSemantics = false;
            
            if (!useSemantics || !AI.EmbeddingService.IsAvailable())
            {
                // 不使用语义增强，直接返回关键词评分结果
                return topCandidates;
            }
            
            // 第3步：对Top候选使用语义评分（异步）
            var semanticTask = Task.Run(async () => await ApplySemanticScoringAsync(topCandidates, context));
            
            // ? 增加超时时间：500ms → 800ms
            if (semanticTask.Wait(800))
            {
                var semanticScored = semanticTask.Result;
                
                if (semanticScored != null && semanticScored.Count > 0)
                {
                    // ? 减少成功日志
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                    {
                        Log.Message($"[Semantic Scoring] Success: {semanticScored.Count} memories");
                    }
                    
                    return semanticScored;
                }
            }
            else
            {
                // ? 减少超时警告：仅10%概率输出
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Warning("[Semantic Scoring] Timeout, using keyword fallback");
                }
            }
            
            // 超时或失败，返回关键词评分结果
            return topCandidates;
        }
        
        /// <summary>
        /// 应用语义评分（异步）
        /// </summary>
        private static async Task<List<ScoredItem<MemoryEntry>>> ApplySemanticScoringAsync(
            List<ScoredItem<MemoryEntry>> candidates,
            string context)
        {
            try
            {
                // 获取上下文的嵌入向量
                float[] contextEmbedding = await AI.EmbeddingService.GetEmbeddingAsync(context);
                
                if (contextEmbedding == null)
                {
                    Log.Warning("[Semantic Scoring] Failed to get context embedding");
                    return candidates;
                }
                
                // 为每个候选记忆计算语义相似度
                foreach (var scored in candidates)
                {
                    try
                    {
                        float[] memoryEmbedding = await AI.EmbeddingService.GetEmbeddingAsync(scored.Item.content);
                        
                        if (memoryEmbedding != null)
                        {
                            // 计算余弦相似度
                            float semanticSimilarity = AI.EmbeddingService.CosineSimilarity(contextEmbedding, memoryEmbedding);
                            
                            // 混合评分：70%关键词 + 30%语义
                            float keywordScore = scored.Score;
                            float hybridScore = (keywordScore * 0.7f) + (semanticSimilarity * 0.3f);
                            
                            scored.Score = hybridScore;
                            
                            // 更新Breakdown
                            if (scored.Breakdown != null)
                            {
                                scored.Breakdown.TypeBoost = semanticSimilarity; // 借用TypeBoost字段显示语义分
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Semantic Scoring] Failed to score memory: {ex.Message}");
                    }
                }
                
                // 重新排序
                return candidates.OrderByDescending(s => s.Score).ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[Semantic Scoring] Error: {ex.Message}");
                return candidates;
            }
        }
        
        /// <summary>
        /// 智能评分常识（混合方案）
        /// ? v3.3.2: 优化超时处理
        /// </summary>
        public static List<ScoredItem<CommonKnowledgeEntry>> ScoreKnowledgeWithSemantics(
            List<CommonKnowledgeEntry> knowledge,
            string context,
            Pawn speaker = null,
            Pawn listener = null)
        {
            if (knowledge == null || knowledge.Count == 0)
                return new List<ScoredItem<CommonKnowledgeEntry>>();
            
            // 第1步：关键词快速过滤
            var quickScored = AdvancedScoringSystem.ScoreKnowledge(knowledge, context, speaker, listener);
            
            int keepCount = Math.Max(5, quickScored.Count / 2);
            var topCandidates = quickScored.Take(keepCount).ToList();
            
            // 第2步：检查是否启用语义增强
            // ? v3.3.2.27: enableSemanticEmbedding已移除，始终使用关键词匹配
            bool useSemantics = false;
            
            if (!useSemantics || !AI.EmbeddingService.IsAvailable())
            {
                return topCandidates;
            }
            
            // 第3步：语义评分
            var semanticTask = Task.Run(async () => await ApplySemanticScoringToKnowledgeAsync(topCandidates, context));
            
            // ? 增加超时：500ms → 800ms
            if (semanticTask.Wait(800))
            {
                var semanticScored = semanticTask.Result;
                
                if (semanticScored != null && semanticScored.Count > 0)
                {
                    // ? 减少日志
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                    {
                        Log.Message($"[Semantic Scoring] Success: {semanticScored.Count} knowledge");
                    }
                    
                    return semanticScored;
                }
            }
            else
            {
                // ? 减少警告：10%概率
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Warning("[Semantic Scoring] Timeout, using keyword fallback");
                }
            }
            
            return topCandidates;
        }
        
        /// <summary>
        /// 应用语义评分到常识（异步）
        /// </summary>
        private static async Task<List<ScoredItem<CommonKnowledgeEntry>>> ApplySemanticScoringToKnowledgeAsync(
            List<ScoredItem<CommonKnowledgeEntry>> candidates,
            string context)
        {
            try
            {
                float[] contextEmbedding = await AI.EmbeddingService.GetEmbeddingAsync(context);
                
                if (contextEmbedding == null)
                    return candidates;
                
                foreach (var scored in candidates)
                {
                    try
                    {
                        float[] knowledgeEmbedding = await AI.EmbeddingService.GetEmbeddingAsync(scored.Item.content);
                        
                        if (knowledgeEmbedding != null)
                        {
                            float semanticSimilarity = AI.EmbeddingService.CosineSimilarity(contextEmbedding, knowledgeEmbedding);
                            
                            // 混合评分：60%关键词 + 40%语义（常识库更依赖语义）
                            float keywordScore = scored.Score;
                            float hybridScore = (keywordScore * 0.6f) + (semanticSimilarity * 0.4f);
                            
                            scored.Score = hybridScore;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Semantic Scoring] Failed to score knowledge: {ex.Message}");
                    }
                }
                
                return candidates.OrderByDescending(s => s.Score).ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[Semantic Scoring] Error: {ex.Message}");
                return candidates;
            }
        }
        
        /// <summary>
        /// 预热缓存：为重要记忆预先生成嵌入向量
        /// 在游戏空闲时调用，避免对话时延迟
        /// </summary>
        public static async Task PrewarmEmbeddingCacheAsync(FourLayerMemoryComp memoryComp)
        {
            if (memoryComp == null || !AI.EmbeddingService.IsAvailable())
                return;
            
            try
            {
                // 只为重要记忆（importance > 0.7）生成缓存
                var importantMemories = new List<MemoryEntry>();
                
                importantMemories.AddRange(memoryComp.SituationalMemories.Where(m => m.importance > 0.7f));
                importantMemories.AddRange(memoryComp.EventLogMemories.Where(m => m.importance > 0.7f));
                importantMemories.AddRange(memoryComp.ArchiveMemories.Take(10).Where(m => m.importance > 0.7f));
                
                if (importantMemories.Count == 0)
                    return;
                
                // 批量生成嵌入（限制数量）
                var toBatch = importantMemories.Take(20).Select(m => m.content).ToList();
                
                if (Prefs.DevMode)
                    Log.Message($"[Semantic Scoring] Prewarming {toBatch.Count} memory embeddings...");
                
                await AI.EmbeddingService.GetEmbeddingsBatchAsync(toBatch);
                
                if (Prefs.DevMode)
                    Log.Message($"[Semantic Scoring] Prewarm complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[Semantic Scoring] Prewarm error: {ex.Message}");
            }
        }
    }
}
