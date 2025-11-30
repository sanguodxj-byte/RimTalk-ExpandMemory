using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 异步向量化同步管理器
    /// 负责后台异步地将重要记忆和常识同步到向量数据库
    /// </summary>
    public static class AsyncVectorSyncManager
    {
        // 同步队列
        private static Queue<SyncTask> syncQueue = new Queue<SyncTask>();
        private static HashSet<string> processingIds = new HashSet<string>();
        
        // 性能统计
        private static int totalSynced = 0;
        private static int totalFailed = 0;
        private static int lastSyncTick = 0;
        
        // 配置参数
        private const int MAX_QUEUE_SIZE = 100;           // 最大队列大小
        private const int BATCH_SIZE = 5;                 // 每批处理数量
        private const int SYNC_INTERVAL_TICKS = 150;      // 同步间隔（~2.5秒）
        private const float IMPORTANCE_THRESHOLD = 0.7f;  // 重要性阈值
        
        /// <summary>
        /// 同步任务
        /// </summary>
        private class SyncTask
        {
            public string Id;
            public string Content;
            public SyncType Type;
            public float Importance;
            public int QueuedTick;
            public Pawn Pawn; // 关联的Pawn（用于记忆）
        }
        
        private enum SyncType
        {
            Memory,
            Knowledge
        }
        
        /// <summary>
        /// 将记忆加入同步队列
        /// </summary>
        public static void QueueMemorySync(MemoryEntry memory, Pawn pawn)
        {
            if (!ShouldSync()) return;
            if (memory == null || string.IsNullOrEmpty(memory.content)) return;
            
            // 检查重要性阈值
            if (memory.importance < IMPORTANCE_THRESHOLD)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[AsyncVectorSync] Memory importance too low: {memory.importance:F2} < {IMPORTANCE_THRESHOLD:F2}");
                }
                return;
            }
            
            // 检查是否已在队列中
            if (processingIds.Contains(memory.id))
            {
                return;
            }
            
            // 检查队列大小
            if (syncQueue.Count >= MAX_QUEUE_SIZE)
            {
                Log.Warning($"[AsyncVectorSync] Queue full ({MAX_QUEUE_SIZE}), dropping oldest task");
                var oldest = syncQueue.Dequeue();
                processingIds.Remove(oldest.Id);
            }
            
            var task = new SyncTask
            {
                Id = memory.id,
                Content = memory.content,
                Type = SyncType.Memory,
                Importance = memory.importance,
                QueuedTick = Find.TickManager.TicksGame,
                Pawn = pawn
            };
            
            syncQueue.Enqueue(task);
            processingIds.Add(memory.id);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[AsyncVectorSync] Queued memory: {memory.content.Substring(0, Math.Min(40, memory.content.Length))}... (queue: {syncQueue.Count})");
            }
        }
        
        /// <summary>
        /// 将常识加入同步队列
        /// </summary>
        public static void QueueKnowledgeSync(CommonKnowledgeEntry knowledge)
        {
            if (!ShouldSync()) return;
            if (knowledge == null || string.IsNullOrEmpty(knowledge.content)) return;
            
            // 检查重要性阈值
            if (knowledge.importance < IMPORTANCE_THRESHOLD)
            {
                return;
            }
            
            // 检查是否已在队列中
            if (processingIds.Contains(knowledge.id))
            {
                return;
            }
            
            // 检查队列大小
            if (syncQueue.Count >= MAX_QUEUE_SIZE)
            {
                var oldest = syncQueue.Dequeue();
                processingIds.Remove(oldest.Id);
            }
            
            var task = new SyncTask
            {
                Id = knowledge.id,
                Content = knowledge.content,
                Type = SyncType.Knowledge,
                Importance = knowledge.importance,
                QueuedTick = Find.TickManager.TicksGame,
                Pawn = null
            };
            
            syncQueue.Enqueue(task);
            processingIds.Add(knowledge.id);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[AsyncVectorSync] Queued knowledge: {knowledge.content.Substring(0, Math.Min(40, knowledge.content.Length))}... (queue: {syncQueue.Count})");
            }
        }
        
        /// <summary>
        /// 定期处理同步队列（从MemoryManager.WorldComponentTick调用）
        /// </summary>
        public static void ProcessSyncQueue()
        {
            if (!ShouldSync()) return;
            if (syncQueue.Count == 0) return;
            
            int currentTick = Find.TickManager.TicksGame;
            
            // 检查同步间隔
            if (currentTick - lastSyncTick < SYNC_INTERVAL_TICKS)
            {
                return;
            }
            
            lastSyncTick = currentTick;
            
            // 批量处理
            int processed = 0;
            var tasks = new List<SyncTask>();
            
            while (syncQueue.Count > 0 && processed < BATCH_SIZE)
            {
                tasks.Add(syncQueue.Dequeue());
                processed++;
            }
            
            if (tasks.Count == 0) return;
            
            // 异步处理批量任务
            Task.Run(async () =>
            {
                await ProcessBatchAsync(tasks);
            });
        }
        
        /// <summary>
        /// 异步处理一批同步任务
        /// </summary>
        private static async Task ProcessBatchAsync(List<SyncTask> tasks)
        {
            foreach (var task in tasks)
            {
                try
                {
                    await ProcessSingleTaskAsync(task);
                    
                    // 成功后从处理集合中移除
                    lock (processingIds)
                    {
                        processingIds.Remove(task.Id);
                    }
                    
                    totalSynced++;
                }
                catch (Exception ex)
                {
                    Log.Error($"[AsyncVectorSync] Failed to sync {task.Type} '{task.Id}': {ex.Message}");
                    
                    lock (processingIds)
                    {
                        processingIds.Remove(task.Id);
                    }
                    
                    totalFailed++;
                }
                
                // 小延迟，避免API频率限制
                await Task.Delay(100);
            }
            
            // 批量完成日志
            if (Prefs.DevMode && tasks.Count > 0)
            {
                Log.Message($"[AsyncVectorSync] Batch complete: {tasks.Count} tasks processed (success: {totalSynced}, failed: {totalFailed}, queue: {syncQueue.Count})");
            }
        }
        
        /// <summary>
        /// 异步处理单个同步任务
        /// </summary>
        private static async Task ProcessSingleTaskAsync(SyncTask task)
        {
            var settings = RimTalkMemoryPatchMod.Settings;
            
            // 获取向量
            float[] vector;
            
            if (settings.enableSemanticEmbedding)
            {
                // 使用语义嵌入（API调用）
                vector = await AI.EmbeddingService.GetEmbeddingAsync(task.Content);
                
                if (vector == null)
                {
                    // API失败，降级到哈希向量
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[AsyncVectorSync] Embedding API failed, using fallback hash vector");
                    }
                    vector = GenerateFallbackVector(task.Content);
                }
            }
            else
            {
                // 直接使用哈希向量
                vector = GenerateFallbackVector(task.Content);
            }
            
            // 存储到向量数据库
            if (task.Type == SyncType.Memory)
            {
                // 记忆类型：使用StoreKnowledgeVector方法（重用现有方法）
                VectorDBManager.StoreKnowledgeVector(
                    task.Id,
                    $"memory_{task.Pawn?.LabelShort ?? "unknown"}", // 标签包含Pawn信息
                    task.Content,
                    vector,
                    task.Importance
                );
            }
            else
            {
                // 常识类型
                VectorDBManager.StoreKnowledgeVector(
                    task.Id,
                    "knowledge", // 标签
                    task.Content,
                    vector,
                    task.Importance
                );
            }
        }
        
        /// <summary>
        /// 生成降级哈希向量
        /// </summary>
        private static float[] GenerateFallbackVector(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new float[1024];
            
            var vector = new float[1024];
            int hash = text.GetHashCode();
            var random = new System.Random(hash);
            
            for (int i = 0; i < 1024; i++)
            {
                vector[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }
            
            // 归一化
            float magnitude = 0f;
            for (int i = 0; i < vector.Length; i++)
            {
                magnitude += vector[i] * vector[i];
            }
            magnitude = (float)Math.Sqrt(magnitude);
            
            if (magnitude > 0)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= magnitude;
                }
            }
            
            return vector;
        }
        
        /// <summary>
        /// 检查是否应该同步
        /// </summary>
        private static bool ShouldSync()
        {
            var settings = RimTalkMemoryPatchMod.Settings;
            return settings != null &&
                   settings.enableVectorDatabase &&
                   settings.autoSyncToVectorDB;
        }
        
        /// <summary>
        /// 获取同步统计信息
        /// </summary>
        public static string GetStats()
        {
            return $"Queue: {syncQueue.Count}/{MAX_QUEUE_SIZE} | Synced: {totalSynced} | Failed: {totalFailed}";
        }
        
        /// <summary>
        /// 清空队列（用于调试或重置）
        /// </summary>
        public static void ClearQueue()
        {
            syncQueue.Clear();
            processingIds.Clear();
            Log.Message($"[AsyncVectorSync] Queue cleared");
        }
        
        /// <summary>
        /// 重置统计信息
        /// </summary>
        public static void ResetStats()
        {
            totalSynced = 0;
            totalFailed = 0;
            Log.Message($"[AsyncVectorSync] Stats reset");
        }
    }
}
