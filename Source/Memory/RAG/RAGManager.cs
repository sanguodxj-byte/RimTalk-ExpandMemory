using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.RAG
{
    /// <summary>
    /// RAG管理器 - 统一检索接口
    /// v3.3.0
    /// 
    /// 提供:
    /// - 同步/异步检索
    /// - 缓存管理
    /// - 性能监控
    /// - 降级策略
    /// </summary>
    public static class RAGManager
    {
        // 缓存
        private static Dictionary<string, CachedRAGResult> resultCache = new Dictionary<string, CachedRAGResult>();
        private const int MAX_CACHE_SIZE = 100;
        private const int CACHE_TTL_TICKS = 2500; // 缓存生存时间（约100秒）
        
        // 统计
        private static int totalQueries = 0;
        private static int cacheHits = 0;
        private static int fallbackCount = 0; // ? 新增：降级次数统计
        private static int backgroundCompletions = 0; // ? 新增：后台完成次数

        /// <summary>
        /// 检索（异步）
        /// </summary>
        public static async Task<RAGResult> RetrieveAsync(
            string query,
            Pawn speaker = null,
            Pawn listener = null,
            RAGConfig config = null)
        {
            if (string.IsNullOrEmpty(query))
                return RAGResult.Empty();
            
            totalQueries++;
            
            // 检查缓存
            string cacheKey = GenerateCacheKey(query, speaker, listener);
            
            if (TryGetCached(cacheKey, out RAGResult cached))
            {
                cacheHits++;
                
                if (Prefs.DevMode)
                    Log.Message($"[RAG Manager] Cache hit: {query.Substring(0, Math.Min(30, query.Length))}...");
                
                return cached;
            }
            
            try
            {
                // 执行检索
                var result = await RAGRetriever.RetrieveAsync(query, speaker, listener, config);
                
                // 缓存结果
                CacheResult(cacheKey, result);
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[RAG Manager] Retrieval failed: {ex.Message}");
                return RAGResult.Empty();
            }
        }
        
        /// <summary>
        /// 检索（同步，立即降级策略）
        /// ? v3.3.2.1: 完全移除主线程阻塞，改用立即降级+后台异步
        /// </summary>
        public static RAGResult Retrieve(
            string query,
            Pawn speaker = null,
            Pawn listener = null,
            RAGConfig config = null,
            int timeoutMs = 500)
        {
            // ? 立即使用降级检索（关键词匹配），避免任何等待
            // 同时在后台启动异步检索，结果将被缓存供下次使用
            
            // 检查缓存
            string cacheKey = GenerateCacheKey(query, speaker, listener);
            
            if (TryGetCached(cacheKey, out RAGResult cached))
            {
                cacheHits++;
                return cached;
            }
            
            // ? 立即返回降级结果（0ms延迟）
            var fallbackResult = FallbackRetrieve(query, speaker, listener, config);
            fallbackCount++; // ? 统计降级次数
            
            // ? 后台启动完整异步检索，结果缓存后下次可用
            Task.Run(async () =>
            {
                try
                {
                    var fullResult = await RetrieveAsync(query, speaker, listener, config);
                    CacheResult(cacheKey, fullResult);
                    backgroundCompletions++; // ? 统计后台完成
                    
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                        Log.Message($"[RAG Manager] Background retrieval completed and cached");
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[RAG Manager] Background retrieval failed: {ex.Message}");
                }
            });
            
            return fallbackResult;
        }
        
        /// <summary>
        /// 降级检索（仅关键词，无向量）
        /// ? v3.3.2.1: 优化性能，减少日志输出
        /// </summary>
        private static RAGResult FallbackRetrieve(
            string query,
            Pawn speaker,
            Pawn listener,
            RAGConfig config)
        {
            try
            {
                config = config ?? new RAGConfig();
                config.UseSemanticScoring = false; // 禁用语义评分
                
                var result = new RAGResult
                {
                    Query = query,
                    Speaker = speaker?.LabelShort,
                    Listener = listener?.LabelShort,
                    VectorMatches = new List<RAGMatch>(),
                    HybridMatches = new List<RAGMatch>()
                };
                
                // 仅使用关键词检索（快速路径）
                var memoryComp = speaker?.TryGetComp<FourLayerMemoryComp>();
                if (memoryComp != null)
                {
                    var memories = new List<MemoryEntry>();
                    memories.AddRange(memoryComp.SituationalMemories);
                    
                    // ? 优化：仅检索最近的ELS记忆，避免全量扫描
                    memories.AddRange(memoryComp.EventLogMemories.Take(20));
                    
                    var scored = AdvancedScoringSystem.ScoreMemories(
                        memories, query, speaker, listener
                    );
                    
                    foreach (var item in scored.Take(config.MaxResults))
                    {
                        result.HybridMatches.Add(new RAGMatch
                        {
                            Source = RAGMatchSource.Memory,
                            Memory = item.Item,
                            Score = item.Score,
                            FinalScore = item.Score,
                            MatchType = "Keyword-Fallback"
                        });
                    }
                }
                
                result.RerankedMatches = result.HybridMatches;
                result.TotalMatches = result.HybridMatches.Count;
                
                // ? 减少日志输出频率
                if (Prefs.DevMode && UnityEngine.Random.value < 0.05f)
                    Log.Message($"[RAG Manager] Fallback retrieval: {result.TotalMatches} matches");
                
                fallbackCount++; // ? 增加降级统计
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[RAG Manager] Fallback failed: {ex.Message}");
                return RAGResult.Empty();
            }
        }
        
        /// <summary>
        /// 生成缓存键
        /// </summary>
        private static string GenerateCacheKey(string query, Pawn speaker, Pawn listener)
        {
            int hash = query.GetHashCode();
            if (speaker != null) hash ^= speaker.thingIDNumber;
            if (listener != null) hash ^= listener.thingIDNumber;
            
            return $"rag_{hash}";
        }
        
        /// <summary>
        /// 尝试获取缓存
        /// </summary>
        private static bool TryGetCached(string key, out RAGResult result)
        {
            result = null;
            
            lock (resultCache)
            {
                if (resultCache.TryGetValue(key, out CachedRAGResult cached))
                {
                    // 检查是否过期
                    int age = Find.TickManager.TicksGame - cached.CachedAt;
                    
                    if (age < CACHE_TTL_TICKS)
                    {
                        result = cached.Result;
                        return true;
                    }
                    else
                    {
                        // 过期，移除
                        resultCache.Remove(key);
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 缓存结果
        /// </summary>
        private static void CacheResult(string key, RAGResult result)
        {
            lock (resultCache)
            {
                // 限制缓存大小
                if (resultCache.Count >= MAX_CACHE_SIZE)
                {
                    // 移除最旧的50%
                    var toRemove = resultCache.Keys.Take(MAX_CACHE_SIZE / 2).ToList();
                    foreach (var k in toRemove)
                    {
                        resultCache.Remove(k);
                    }
                }
                
                resultCache[key] = new CachedRAGResult
                {
                    Result = result,
                    CachedAt = Find.TickManager.TicksGame
                };
            }
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            lock (resultCache)
            {
                int count = resultCache.Count;
                resultCache.Clear();
                
                if (Prefs.DevMode)
                    Log.Message($"[RAG Manager] Cleared {count} cached results");
            }
        }
        
        /// <summary>
        /// 获取统计信息
        /// ? v3.3.2.1: 添加降级和后台统计
        /// </summary>
        public static RAGStats GetStats()
        {
            lock (resultCache)
            {
                return new RAGStats
                {
                    TotalQueries = totalQueries,
                    CacheHits = cacheHits,
                    CacheSize = resultCache.Count,
                    CacheHitRate = totalQueries > 0 ? (float)cacheHits / totalQueries : 0f,
                    FallbackCount = fallbackCount,
                    BackgroundCompletions = backgroundCompletions
                };
            }
        }
        
        /// <summary>
        /// 重置统计
        /// </summary>
        public static void ResetStats()
        {
            totalQueries = 0;
            cacheHits = 0;
            fallbackCount = 0;
            backgroundCompletions = 0;
        }
    }
    
    #region 数据结构
    
    /// <summary>
    /// 缓存的RAG结果
    /// </summary>
    internal class CachedRAGResult
    {
        public RAGResult Result;
        public int CachedAt;
    }
    
    /// <summary>
    /// RAG统计信息
    /// ? v3.3.2.1: 添加降级和后台统计
    /// </summary>
    public class RAGStats
    {
        public int TotalQueries;
        public int CacheHits;
        public int CacheSize;
        public float CacheHitRate;
        public int FallbackCount; // ? 新增
        public int BackgroundCompletions; // ? 新增
        
        public override string ToString()
        {
            return $"Queries: {TotalQueries}, Cache: {CacheHits}/{TotalQueries} ({CacheHitRate:P0}), " +
                   $"Fallback: {FallbackCount}, Background: {BackgroundCompletions}, Size: {CacheSize}";
        }
    }
    
    #endregion
}
