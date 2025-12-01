using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimTalk.Memory.AI;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 常识向量同步管理器
    /// 负责将CommonKnowledgeEntry转换为向量并存储到VectorDB
    /// v3.3.2.3
    /// </summary>
    public static class KnowledgeVectorSyncManager
    {
        private static HashSet<string> pendingKnowledgeIds = new HashSet<string>();
        private static HashSet<string> syncedKnowledgeIds = new HashSet<string>();
        
        private static int totalSyncRequests = 0;
        private static int successfulSyncs = 0;
        private static int failedSyncs = 0;
        
        /// <summary>
        /// 同步单个常识条目到向量数据库
        /// </summary>
        public static void SyncKnowledge(CommonKnowledgeEntry knowledge)
        {
            if (knowledge == null || string.IsNullOrEmpty(knowledge.id))
                return;
            
            // 检查是否已同步
            if (syncedKnowledgeIds.Contains(knowledge.id))
            {
                if (Prefs.DevMode)
                    Log.Message($"[Knowledge Vector] Already synced: {knowledge.id}");
                return;
            }
            
            // 检查是否正在同步
            if (pendingKnowledgeIds.Contains(knowledge.id))
            {
                if (Prefs.DevMode)
                    Log.Message($"[Knowledge Vector] Already pending: {knowledge.id}");
                return;
            }
            
            pendingKnowledgeIds.Add(knowledge.id);
            totalSyncRequests++;
            
            // 异步向量化
            Task.Run(async () =>
            {
                try
                {
                    // 构建文本：标签 + 内容
                    string text = $"[{knowledge.tag}] {knowledge.content}";
                    
                    // 调用语义嵌入服务
                    float[] vector = await EmbeddingService.GetEmbeddingAsync(text);
                    
                    if (vector != null && vector.Length > 0)
                    {
                        // 存储到向量数据库（直接调用静态方法）
                        if (VectorDBManager.IsAvailable())
                        {
                            VectorDBManager.StoreKnowledgeVector(knowledge.id, vector);
                            
                            // 标记为已同步
                            lock (syncedKnowledgeIds)
                            {
                                syncedKnowledgeIds.Add(knowledge.id);
                            }
                            
                            successfulSyncs++;
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[Knowledge Vector] ? Synced: {knowledge.tag} ({vector.Length}D)");
                            }
                        }
                        else
                        {
                            failedSyncs++;
                            Log.Warning($"[Knowledge Vector] VectorDB not available for: {knowledge.id}");
                        }
                    }
                    else
                    {
                        failedSyncs++;
                        Log.Warning($"[Knowledge Vector] Failed to generate vector for: {knowledge.id}");
                    }
                }
                catch (Exception ex)
                {
                    failedSyncs++;
                    Log.Error($"[Knowledge Vector] Sync error for {knowledge.id}: {ex.Message}");
                }
                finally
                {
                    lock (pendingKnowledgeIds)
                    {
                        pendingKnowledgeIds.Remove(knowledge.id);
                    }
                }
            });
        }
        
        /// <summary>
        /// 批量同步常识库
        /// </summary>
        public static void SyncAllKnowledge(CommonKnowledgeLibrary library, int maxConcurrent = 5)
        {
            if (library == null || library.Entries.Count == 0)
                return;
            
            Log.Message($"[Knowledge Vector] ?? Starting batch sync: {library.Entries.Count} entries");
            
            var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
            if (!settings.enableSemanticEmbedding)
            {
                Log.Message("[Knowledge Vector] ?? Semantic embedding disabled in settings");
                return;
            }
            
            int syncedCount = 0;
            int skippedCount = 0;
            
            foreach (var knowledge in library.Entries)
            {
                if (!knowledge.isEnabled)
                {
                    skippedCount++;
                    continue;
                }
                
                // 已同步则跳过
                if (syncedKnowledgeIds.Contains(knowledge.id))
                {
                    skippedCount++;
                    continue;
                }
                
                // 限制并发数
                while (pendingKnowledgeIds.Count >= maxConcurrent)
                {
                    System.Threading.Thread.Sleep(100);
                }
                
                SyncKnowledge(knowledge);
                syncedCount++;
            }
            
            Log.Message($"[Knowledge Vector] ?? Batch sync initiated: {syncedCount} queued, {skippedCount} skipped");
        }
        
        /// <summary>
        /// 删除常识向量
        /// </summary>
        public static void RemoveKnowledgeVector(string knowledgeId)
        {
            if (string.IsNullOrEmpty(knowledgeId))
                return;
            
            if (VectorDBManager.IsAvailable())
            {
                VectorDBManager.RemoveKnowledgeVector(knowledgeId);
                
                lock (syncedKnowledgeIds)
                {
                    syncedKnowledgeIds.Remove(knowledgeId);
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[Knowledge Vector] ??? Removed: {knowledgeId}");
                }
            }
        }
        
        /// <summary>
        /// 清空所有常识向量
        /// </summary>
        public static void ClearAllKnowledgeVectors()
        {
            if (VectorDBManager.IsAvailable())
            {
                VectorDBManager.ClearKnowledgeVectors();
                
                lock (syncedKnowledgeIds)
                {
                    syncedKnowledgeIds.Clear();
                }
                
                lock (pendingKnowledgeIds)
                {
                    pendingKnowledgeIds.Clear();
                }
                
                Log.Message("[Knowledge Vector] ?? All knowledge vectors cleared");
            }
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public static string GetStats()
        {
            return $"Knowledge Vectors: {syncedKnowledgeIds.Count} synced, " +
                   $"{pendingKnowledgeIds.Count} pending, " +
                   $"{totalSyncRequests} total requests, " +
                   $"{successfulSyncs} successful, " +
                   $"{failedSyncs} failed";
        }
        
        /// <summary>
        /// 检查常识是否已向量化
        /// </summary>
        public static bool IsVectorized(string knowledgeId)
        {
            return syncedKnowledgeIds.Contains(knowledgeId);
        }
        
        /// <summary>
        /// 重置统计信息
        /// </summary>
        public static void ResetStats()
        {
            totalSyncRequests = 0;
            successfulSyncs = 0;
            failedSyncs = 0;
        }
    }
}
