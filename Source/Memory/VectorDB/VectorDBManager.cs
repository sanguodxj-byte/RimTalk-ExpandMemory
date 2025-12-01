using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 向量数据库管理器 - 集成层
    /// v3.3.4 - 改用纯C#内存存储（零Native依赖）
    /// 
    /// 功能:
    /// - 管理向量数据库生命周期
    /// - 自动同步记忆到数据库
    /// - 提供高级检索接口
    /// - SIMD加速检索
    /// </summary>
    public static class VectorDBManager
    {
        // ? v3.3.4: 改用纯C#内存存储
        private static InMemoryVectorStore vectorStore;
        private static bool isInitialized = false;
        
        /// <summary>
        /// 初始化向量存储
        /// ? v3.3.4: 零Native依赖，100%成功率
        /// </summary>
        public static void Initialize(bool useSharedDB = false)
        {
            if (isInitialized)
                return;
            
            try
            {
                // 检查是否启用
                var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
                if (settings == null || !settings.enableVectorDatabase)
                {
                    Log.Message("[VectorDB Manager] Vector database disabled in settings");
                    return;
                }
                
                // ? 创建纯C#内存存储（无任何异常风险）
                vectorStore = new InMemoryVectorStore();
                isInitialized = true;
                
                Log.Message("[VectorDB Manager] ? Initialized with InMemoryVectorStore (Zero Native Dependencies)");
                Log.Message("[VectorDB Manager] Using SIMD-accelerated cosine similarity");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] ? Unexpected init error: {ex.Message}");
                isInitialized = false;
                vectorStore = null;
            }
        }
        
        /// <summary>
        /// 检查是否可用
        /// </summary>
        public static bool IsAvailable()
        {
            return isInitialized && vectorStore != null;
        }
        
        /// <summary>
        /// 同步记忆到向量数据库
        /// </summary>
        public static async Task SyncMemoryAsync(MemoryEntry memory)
        {
            if (!IsAvailable() || memory == null)
                return;
            
            try
            {
                // 只同步重要记忆
                if (memory.importance < 0.7f)
                    return;
                
                // 获取向量
                float[] embedding = await AI.EmbeddingService.GetEmbeddingAsync(memory.content);
                
                if (embedding != null)
                {
                    // 构建元数据
                    string metadata = BuildMetadata(memory);
                    
                    // 存储到内存向量库
                    vectorStore.Upsert(
                        id: memory.id,
                        vector: embedding,
                        content: memory.content,
                        metadata: metadata
                    );
                    
                    if (Prefs.DevMode)
                        Log.Message($"[VectorDB Manager] Synced memory: {memory.id}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Sync failed for {memory.id}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 批量同步记忆
        /// </summary>
        public static async Task BatchSyncMemoriesAsync(List<MemoryEntry> memories)
        {
            if (!IsAvailable() || memories == null || memories.Count == 0)
                return;
            
            try
            {
                // 过滤重要记忆
                var importantMemories = memories.Where(m => m.importance > 0.7f).ToList();
                
                if (importantMemories.Count == 0)
                    return;
                
                Log.Message($"[VectorDB Manager] Batch syncing {importantMemories.Count} memories...");
                
                int synced = 0;
                
                // 获取所有向量并存储
                foreach (var memory in importantMemories)
                {
                    try
                    {
                        float[] embedding = await AI.EmbeddingService.GetEmbeddingAsync(memory.content);
                        
                        if (embedding != null)
                        {
                            vectorStore.Upsert(
                                id: memory.id,
                                vector: embedding,
                                content: memory.content,
                                metadata: BuildMetadata(memory)
                            );
                            synced++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[VectorDB Manager] Failed to get embedding for {memory.id}: {ex.Message}");
                    }
                }
                
                Log.Message($"[VectorDB Manager] Batch sync complete: {synced}/{importantMemories.Count}");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Batch sync failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 语义搜索记忆（使用InMemoryVectorStore）
        /// </summary>
        public static async Task<List<string>> SemanticSearchAsync(string query, int topK = 10, float minSimilarity = 0.5f)
        {
            if (!IsAvailable() || string.IsNullOrEmpty(query))
                return new List<string>();
            
            try
            {
                // 获取查询向量
                float[] queryVector = await AI.EmbeddingService.GetEmbeddingAsync(query);
                
                if (queryVector == null)
                    return new List<string>();
                
                // 使用SIMD加速搜索
                var results = vectorStore.Search(queryVector, topK, minSimilarity);
                
                // 返回记忆ID列表
                return results.Select(r => r.Id).ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Semantic search failed: {ex.Message}");
                return new List<string>();
            }
        }
        
        /// <summary>
        /// 删除记忆向量
        /// </summary>
        public static void DeleteMemoryVector(string memoryId)
        {
            if (!IsAvailable() || string.IsNullOrEmpty(memoryId))
                return;
            
            try
            {
                vectorStore.Remove(memoryId);
                
                if (Prefs.DevMode)
                    Log.Message($"[VectorDB Manager] Deleted vector: {memoryId}");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Delete failed for {memoryId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? 搜索常识向量
        /// </summary>
        public static async Task<List<string>> SearchKnowledgeAsync(string query, int topK = 5, float minSimilarity = 0.3f)
        {
            if (!IsAvailable() || string.IsNullOrEmpty(query))
                return new List<string>();
            
            try
            {
                // 获取查询向量
                float[] queryVector = await AI.EmbeddingService.GetEmbeddingAsync(query);
                
                if (queryVector == null)
                    return new List<string>();
                
                // 使用SIMD加速搜索
                var results = vectorStore.Search(queryVector, topK * 2, minSimilarity);
                
                // 过滤出常识类型的结果
                return results
                    .Where(r => r.Metadata != null && r.Metadata.Contains("\"type\":\"knowledge\""))
                    .Take(topK)
                    .Select(r => r.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Knowledge search failed: {ex.Message}");
                return new List<string>();
            }
        }
        
        /// <summary>
        /// ? v3.3.2.4: 存储常识向量（使用InMemoryVectorStore）
        /// </summary>
        public static void StoreKnowledgeVector(string knowledgeId, float[] vector)
        {
            if (!IsAvailable() || string.IsNullOrEmpty(knowledgeId) || vector == null || vector.Length == 0)
                return;
            
            try
            {
                // 构建元数据
                string content = $"Knowledge_{knowledgeId}";
                string metadata = $"{{\"type\":\"knowledge\",\"timestamp\":{Find.TickManager.TicksGame}}}";
                
                // 存储到内存向量库
                vectorStore.Upsert(knowledgeId, vector, content, metadata);
                
                if (Prefs.DevMode)
                    Log.Message($"[VectorDB Manager] ? Stored knowledge vector: {knowledgeId} ({vector.Length}D)");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Failed to store knowledge vector {knowledgeId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? v3.3.2.4: 删除常识向量
        /// </summary>
        public static void RemoveKnowledgeVector(string knowledgeId)
        {
            if (!IsAvailable() || string.IsNullOrEmpty(knowledgeId))
                return;
            
            try
            {
                vectorStore.Remove(knowledgeId);
                
                if (Prefs.DevMode)
                    Log.Message($"[VectorDB Manager] ??? Removed knowledge vector: {knowledgeId}");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Failed to remove knowledge vector {knowledgeId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? v3.3.2.4: 清空所有向量
        /// </summary>
        public static void ClearKnowledgeVectors()
        {
            if (!IsAvailable())
                return;
            
            try
            {
                vectorStore.Clear();
                Log.Message("[VectorDB Manager] Cleared all vectors");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Failed to clear vectors: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取数据库统计
        /// </summary>
        public static VectorDBStats GetStats()
        {
            if (!IsAvailable())
            {
                return new VectorDBStats
                {
                    IsInitialized = false
                };
            }
            
            var stats = vectorStore.GetStats();
            return new VectorDBStats
            {
                VectorCount = stats.VectorCount,
                VectorDimension = stats.VectorDimension,
                DatabaseSizeMB = stats.EstimatedMemoryMB,
                IsInitialized = stats.IsInitialized,
                UseHNSW = false // 内存存储不使用HNSW
            };
        }
        
        /// <summary>
        /// 清空数据库
        /// </summary>
        public static void ClearDatabase()
        {
            if (!IsAvailable())
                return;
            
            try
            {
                vectorStore.Clear();
                Log.Message("[VectorDB Manager] Database cleared");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Clear failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 压缩数据库（内存存储无需此操作）
        /// </summary>
        public static void CompactDatabase()
        {
            // 内存存储无需压缩
            if (Prefs.DevMode)
                Log.Message("[VectorDB Manager] Compact not needed for InMemoryVectorStore");
        }
        
        /// <summary>
        /// 关闭数据库
        /// </summary>
        public static void Shutdown()
        {
            if (vectorStore != null)
            {
                try
                {
                    vectorStore.Clear();
                    vectorStore = null;
                    isInitialized = false;
                    
                    Log.Message("[VectorDB Manager] Shut down");
                }
                catch (Exception ex)
                {
                    Log.Error($"[VectorDB Manager] Shutdown error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 构建元数据JSON
        /// </summary>
        private static string BuildMetadata(MemoryEntry memory)
        {
            // 简单的JSON构建（避免依赖JSON库）
            return $"{{" +
                   $"\"type\":\"{memory.type}\"," +
                   $"\"layer\":\"{memory.layer}\"," +
                   $"\"importance\":{memory.importance:F2}," +
                   $"\"timestamp\":{memory.timestamp}" +
                   $"}}";
        }
    }
}
