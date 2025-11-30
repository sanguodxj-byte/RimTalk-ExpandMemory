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
    /// v3.2.0
    /// 
    /// 功能:
    /// - 管理向量数据库生命周期
    /// - 自动同步记忆到数据库
    /// - 提供高级检索接口
    /// - 跨存档共享（可选）
    /// </summary>
    public static class VectorDBManager
    {
        private static VectorMemoryDatabase database;
        private static bool isInitialized = false;
        private static string currentDBPath;
        
        // 配置
        private const string DB_FILENAME = "MemoryVectors.db";
        private const string SHARED_DB_FOLDER = "SharedVectors";
        
        /// <summary>
        /// 初始化向量数据库
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
                
                // 确定数据库路径
                string dbPath = GetDatabasePath(useSharedDB);
                
                // 创建数据库实例
                database = new VectorMemoryDatabase(dbPath);
                currentDBPath = dbPath;
                isInitialized = true;
                
                Log.Message($"[VectorDB Manager] Initialized: {dbPath}");
                
                // 显示统计
                var stats = database.GetStats();
                Log.Message($"[VectorDB Manager] Loaded {stats.VectorCount} vectors ({stats.DatabaseSizeMB:F2} MB)");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Init failed: {ex.Message}");
                isInitialized = false;
            }
        }
        
        /// <summary>
        /// 检查是否可用
        /// </summary>
        public static bool IsAvailable()
        {
            return isInitialized && database != null;
        }
        
        /// <summary>
        /// 获取数据库路径
        /// </summary>
        private static string GetDatabasePath(bool useShared)
        {
            if (useShared)
            {
                // 共享数据库（所有存档共用）
                string sharedPath = Path.Combine(GenFilePaths.SaveDataFolderPath, SHARED_DB_FOLDER);
                if (!Directory.Exists(sharedPath))
                {
                    Directory.CreateDirectory(sharedPath);
                }
                return Path.Combine(sharedPath, DB_FILENAME);
            }
            else
            {
                // 存档专属数据库
                string saveName = Find.World?.info?.name ?? "Unknown";
                string safeName = string.Concat(saveName.Split(Path.GetInvalidFileNameChars()));
                return Path.Combine(GenFilePaths.SaveDataFolderPath, $"{safeName}_{DB_FILENAME}");
            }
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
                    
                    // 插入数据库
                    database.UpsertVector(
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
                
                var insertItems = new List<VectorInsertItem>();
                
                // 获取所有向量
                foreach (var memory in importantMemories)
                {
                    try
                    {
                        float[] embedding = await AI.EmbeddingService.GetEmbeddingAsync(memory.content);
                        
                        if (embedding != null)
                        {
                            insertItems.Add(new VectorInsertItem
                            {
                                Id = memory.id,
                                Vector = embedding,
                                Content = memory.content,
                                Metadata = BuildMetadata(memory)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[VectorDB Manager] Failed to get embedding for {memory.id}: {ex.Message}");
                    }
                }
                
                // 批量插入
                if (insertItems.Count > 0)
                {
                    int synced = database.BatchInsert(insertItems);
                    Log.Message($"[VectorDB Manager] Batch sync complete: {synced}/{insertItems.Count}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Batch sync failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 语义搜索记忆
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
                
                // 搜索相似向量
                var results = database.SearchSimilar(queryVector, topK, minSimilarity);
                
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
                database.DeleteVector(memoryId);
                
                if (Prefs.DevMode)
                    Log.Message($"[VectorDB Manager] Deleted vector: {memoryId}");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Delete failed for {memoryId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? 存储常识向量到数据库
        /// </summary>
        public static void StoreKnowledgeVector(string knowledgeId, string tag, string content, float[] vector, float importance)
        {
            if (!IsAvailable() || string.IsNullOrEmpty(knowledgeId) || vector == null || vector.Length == 0)
                return;
            
            try
            {
                // 构建常识元数据
                string metadata = BuildKnowledgeMetadata(tag, importance);
                
                // 存储到数据库
                database.UpsertVector(
                    id: knowledgeId,
                    vector: vector,
                    content: content,
                    metadata: metadata
                );
                
                if (Prefs.DevMode)
                    Log.Message($"[VectorDB Manager] Stored knowledge vector: {knowledgeId} [{tag}]");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Failed to store knowledge vector {knowledgeId}: {ex.Message}");
                throw; // 重新抛出异常以便调用者处理
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
        
        /// <summary>
        /// ? 构建常识元数据JSON
        /// </summary>
        private static string BuildKnowledgeMetadata(string tag, float importance)
        {
            // 简单的JSON构建（避免依赖JSON库）
            return $"{{" +
                   $"\"type\":\"knowledge\"," +
                   $"\"tag\":\"{tag}\"," +
                   $"\"importance\":{importance:F2}," +
                   $"\"timestamp\":{Find.TickManager.TicksGame}" +
                   $"}}";
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
                
                // 搜索相似向量（只返回常识类型）
                var results = database.SearchSimilar(queryVector, topK * 2, minSimilarity); // 取多一些，然后过滤
                
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
            
            return database.GetStats();
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
                database.ClearAll();
                Log.Message("[VectorDB Manager] Database cleared");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Clear failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 压缩数据库
        /// </summary>
        public static void CompactDatabase()
        {
            if (!IsAvailable())
                return;
            
            try
            {
                database.Vacuum();
                Log.Message("[VectorDB Manager] Database compacted");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB Manager] Compact failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 关闭数据库
        /// </summary>
        public static void Shutdown()
        {
            if (database != null)
            {
                try
                {
                    database.Dispose();
                    database = null;
                    isInitialized = false;
                    
                    Log.Message("[VectorDB Manager] Shut down");
                }
                catch (Exception ex)
                {
                    Log.Error($"[VectorDB Manager] Shutdown error: {ex.Message}");
                }
            }
        }
    }
}
