using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite; // ? 替换为Microsoft.Data.Sqlite
using Verse;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 轻量级向量数据库 - 基于SQLite
    /// v3.2.0 实验性功能
    /// ? v3.3.2: 切换到Microsoft.Data.Sqlite（微软官方，无本地DLL依赖）
    /// 
    /// 功能:
    /// - 持久化存储向量（跨重启保留）
    /// - 快速相似度搜索（暴力搜索，适合<10k向量）
    /// - 零依赖（仅SQLite）
    /// - 跨存档共享（可选）
    /// 
    /// 性能:
    /// - 插入: <1ms/条
    /// - 查询: <50ms/1000条（暴力搜索）
    /// - 存储: ~5MB/1000条向量
    /// 
    /// 限制:
    /// - 最大10,000条向量（暴力搜索）
    /// - 无高级索引（HNSW/IVF）
    /// - 不支持分布式
    /// </summary>
    public class VectorMemoryDatabase : IDisposable
    {
        private SqliteConnection connection; // ? 改为SqliteConnection
        private string dbPath;
        private bool isInitialized = false;
        
        // 配置
        private const int MAX_VECTORS = 10000; // 最多存储10k向量
        private const int VECTOR_DIM = 1024;   // DeepSeek维度
        
        /// <summary>
        /// 初始化向量数据库
        /// </summary>
        public VectorMemoryDatabase(string databasePath)
        {
            try
            {
                dbPath = databasePath;
                
                // 确保目录存在
                string directory = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // ? Microsoft.Data.Sqlite连接字符串格式
                connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();
                
                // 创建表结构
                CreateTables();
                
                isInitialized = true;
                Log.Message($"[VectorDB] Initialized with Microsoft.Data.Sqlite at: {dbPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Init failed: {ex.Message}");
                isInitialized = false;
            }
        }
        
        /// <summary>
        /// 创建数据库表
        /// </summary>
        private void CreateTables()
        {
            using (var cmd = connection.CreateCommand())
            {
                // 向量表（存储向量和元数据）
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS vectors (
                        id TEXT PRIMARY KEY,
                        vector BLOB NOT NULL,
                        dimension INTEGER NOT NULL,
                        content TEXT,
                        metadata TEXT,
                        created_at INTEGER NOT NULL,
                        updated_at INTEGER NOT NULL
                    )
                ";
                cmd.ExecuteNonQuery();
                
                // 创建索引
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_created_at ON vectors(created_at)";
                cmd.ExecuteNonQuery();
                
                // 统计表（用于监控）
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS stats (
                        key TEXT PRIMARY KEY,
                        value TEXT
                    )
                ";
                cmd.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// 插入或更新向量
        /// </summary>
        public bool UpsertVector(string id, float[] vector, string content = null, string metadata = null)
        {
            if (!isInitialized || vector == null)
                return false;
            
            try
            {
                // 检查容量
                if (GetVectorCount() >= MAX_VECTORS)
                {
                    // 清理最旧的10%
                    CleanupOldestVectors(MAX_VECTORS / 10);
                }
                
                // 序列化向量为BLOB
                byte[] vectorBlob = SerializeVector(vector);
                
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO vectors 
                        (id, vector, dimension, content, metadata, created_at, updated_at)
                        VALUES (@id, @vector, @dimension, @content, @metadata, @created_at, @updated_at)
                    ";
                    
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@vector", vectorBlob);
                    cmd.Parameters.AddWithValue("@dimension", vector.Length);
                    cmd.Parameters.AddWithValue("@content", content ?? "");
                    cmd.Parameters.AddWithValue("@metadata", metadata ?? "");
                    cmd.Parameters.AddWithValue("@created_at", currentTick);
                    cmd.Parameters.AddWithValue("@updated_at", currentTick);
                    
                    cmd.ExecuteNonQuery();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Insert failed for {id}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 搜索最相似的向量（暴力搜索）
        /// </summary>
        public List<VectorSearchResult> SearchSimilar(float[] queryVector, int topK = 10, float minSimilarity = 0.0f)
        {
            if (!isInitialized || queryVector == null)
                return new List<VectorSearchResult>();
            
            try
            {
                var results = new List<VectorSearchResult>();
                
                // 读取所有向量并计算相似度
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, vector, content, metadata FROM vectors";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.GetString(0);
                            byte[] vectorBlob = (byte[])reader.GetValue(1);
                            string content = reader.IsDBNull(2) ? null : reader.GetString(2);
                            string metadata = reader.IsDBNull(3) ? null : reader.GetString(3);
                            
                            // 反序列化向量
                            float[] storedVector = DeserializeVector(vectorBlob);
                            
                            if (storedVector != null && storedVector.Length == queryVector.Length)
                            {
                                // 计算余弦相似度
                                float similarity = AI.EmbeddingService.CosineSimilarity(queryVector, storedVector);
                                
                                if (similarity >= minSimilarity)
                                {
                                    results.Add(new VectorSearchResult
                                    {
                                        Id = id,
                                        Similarity = similarity,
                                        Content = content,
                                        Metadata = metadata
                                    });
                                }
                            }
                        }
                    }
                }
                
                // 排序并返回Top K
                return results
                    .OrderByDescending(r => r.Similarity)
                    .Take(topK)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Search failed: {ex.Message}");
                return new List<VectorSearchResult>();
            }
        }
        
        /// <summary>
        /// 批量插入向量
        /// </summary>
        public int BatchInsert(List<VectorInsertItem> items)
        {
            if (!isInitialized || items == null || items.Count == 0)
                return 0;
            
            int successCount = 0;
            
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var item in items)
                    {
                        if (UpsertVector(item.Id, item.Vector, item.Content, item.Metadata))
                        {
                            successCount++;
                        }
                    }
                    
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Log.Error($"[VectorDB] Batch insert failed: {ex.Message}");
                }
            }
            
            return successCount;
        }
        
        /// <summary>
        /// 获取向量数量
        /// </summary>
        public int GetVectorCount()
        {
            if (!isInitialized)
                return 0;
            
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM vectors";
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 清理最旧的向量
        /// </summary>
        private void CleanupOldestVectors(int count)
        {
            if (!isInitialized || count <= 0)
                return;
            
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        DELETE FROM vectors 
                        WHERE id IN (
                            SELECT id FROM vectors 
                            ORDER BY created_at ASC 
                            LIMIT @count
                        )
                    ";
                    cmd.Parameters.AddWithValue("@count", count);
                    int deleted = cmd.ExecuteNonQuery();
                    
                    if (Prefs.DevMode)
                        Log.Message($"[VectorDB] Cleaned up {deleted} oldest vectors");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Cleanup failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 删除向量
        /// </summary>
        public bool DeleteVector(string id)
        {
            if (!isInitialized || string.IsNullOrEmpty(id))
                return false;
            
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM vectors WHERE id = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Delete failed for {id}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 清空数据库
        /// </summary>
        public void ClearAll()
        {
            if (!isInitialized)
                return;
            
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM vectors";
                    int deleted = cmd.ExecuteNonQuery();
                    
                    Log.Message($"[VectorDB] Cleared {deleted} vectors");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Clear failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取数据库统计信息
        /// </summary>
        public VectorDBStats GetStats()
        {
            var stats = new VectorDBStats
            {
                VectorCount = GetVectorCount(),
                MaxVectors = MAX_VECTORS,
                VectorDimension = VECTOR_DIM,
                DatabasePath = dbPath,
                IsInitialized = isInitialized
            };
            
            if (isInitialized)
            {
                try
                {
                    // 计算数据库大小
                    if (File.Exists(dbPath))
                    {
                        stats.DatabaseSizeMB = new FileInfo(dbPath).Length / (1024.0 * 1024.0);
                    }
                }
                catch { }
            }
            
            return stats;
        }
        
        /// <summary>
        /// 序列化向量为BLOB
        /// </summary>
        private byte[] SerializeVector(float[] vector)
        {
            byte[] bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        
        /// <summary>
        /// 反序列化BLOB为向量
        /// </summary>
        private float[] DeserializeVector(byte[] bytes)
        {
            float[] vector = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
            return vector;
        }
        
        /// <summary>
        /// 压缩数据库
        /// </summary>
        public void Vacuum()
        {
            if (!isInitialized)
                return;
            
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "VACUUM";
                    cmd.ExecuteNonQuery();
                    
                    Log.Message("[VectorDB] Database vacuumed");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[VectorDB] Vacuum failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (connection != null)
            {
                try
                {
                    connection.Close();
                    connection.Dispose();
                    Log.Message("[VectorDB] Database connection closed");
                }
                catch (Exception ex)
                {
                    Log.Error($"[VectorDB] Dispose error: {ex.Message}");
                }
            }
        }
    }
    
    #region 数据结构
    
    /// <summary>
    /// 向量搜索结果
    /// </summary>
    public class VectorSearchResult
    {
        public string Id;
        public float Similarity;
        public string Content;
        public string Metadata;
    }
    
    /// <summary>
    /// 向量插入项
    /// </summary>
    public class VectorInsertItem
    {
        public string Id;
        public float[] Vector;
        public string Content;
        public string Metadata;
    }
    
    /// <summary>
    /// 向量数据库统计信息
    /// </summary>
    public class VectorDBStats
    {
        public int VectorCount;
        public int MaxVectors;
        public int VectorDimension;
        public string DatabasePath;
        public bool IsInitialized;
        public double DatabaseSizeMB;
    }
    
    #endregion
}
