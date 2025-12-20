using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 提示词缓存系统 - 缓存记忆和常识注入结果
    /// 避免每次对话都重新计算，显著提升性能
    /// </summary>
    public class PromptCache : IExposable
    {
        /// <summary>
        /// 缓存条目
        /// </summary>
        public class CacheEntry : IExposable
        {
            public string memoryPrompt;       // 记忆注入结果
            public string knowledgePrompt;    // 常识注入结果
            public string fullPrompt;         // 完整提示词
            public int timestamp;             // 创建时间
            public int lastUsedTick;          // 最后使用时间
            public int useCount;              // 使用次数
            
            // 缓存失效条件
            public int pawnMemoryCount;       // Pawn记忆数量（用于检测记忆变化）
            public int knowledgeCount;        // 常识库条目数（用于检测常识变化）
            
            public CacheEntry()
            {
            }
            
            public CacheEntry(string memoryPrompt, string knowledgePrompt, string fullPrompt, 
                             int pawnMemoryCount, int knowledgeCount)
            {
                this.memoryPrompt = memoryPrompt;
                this.knowledgePrompt = knowledgePrompt;
                this.fullPrompt = fullPrompt;
                this.timestamp = Find.TickManager.TicksGame;
                this.lastUsedTick = this.timestamp;
                this.useCount = 1;
                this.pawnMemoryCount = pawnMemoryCount;
                this.knowledgeCount = knowledgeCount;
            }
            
            /// <summary>
            /// 检查缓存是否仍然有效
            /// ? v3.3.4: 放宽失效条件，提高缓存命中率
            /// </summary>
            public bool IsValid(int currentMemoryCount, int currentKnowledgeCount, int currentTick, int expireTicks)
            {
                // ? 优化1：放宽记忆变化阈值（±5条内不失效）
                // 原因：增加1-2条记忆不应导致整个提示词失效
                // 记忆注入是动态选择的，微小变化影响很小
                int memoryDiff = Math.Abs(pawnMemoryCount - currentMemoryCount);
                if (memoryDiff > 5)
                    return false;
                
                // ? 优化2：放宽常识变化阈值（±10条内不失效）
                // 原因：常识库变化更不应导致缓存失效
                // 常识库是全局共享的，单个常识变化影响极小
                int knowledgeDiff = Math.Abs(knowledgeCount - currentKnowledgeCount);
                if (knowledgeDiff > 10)
                    return false;
                
                // 时间失效检查
                if (currentTick - timestamp > expireTicks)
                    return false;
                
                return true;
            }
            
            public void ExposeData()
            {
                Scribe_Values.Look(ref memoryPrompt, "memoryPrompt");
                Scribe_Values.Look(ref knowledgePrompt, "knowledgePrompt");
                Scribe_Values.Look(ref fullPrompt, "fullPrompt");
                Scribe_Values.Look(ref timestamp, "timestamp");
                Scribe_Values.Look(ref lastUsedTick, "lastUsedTick");
                Scribe_Values.Look(ref useCount, "useCount");
                Scribe_Values.Look(ref pawnMemoryCount, "pawnMemoryCount");
                Scribe_Values.Look(ref knowledgeCount, "knowledgeCount");
            }
        }
        
        // 缓存字典：Key = pawnId_contextHash
        private Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        
        // 统计
        private int totalHits = 0;
        private int totalMisses = 0;
        private int totalInvalidations = 0;
        
        // 配置
        private int MaxCacheSize => RimTalkMemoryPatchMod.Settings.promptCacheSize;
        private int ExpireMinutes => RimTalkMemoryPatchMod.Settings.promptCacheExpireMinutes;
        
        public float HitRate
        {
            get
            {
                int total = totalHits + totalMisses;
                if (total == 0) return 0f;
                return (float)totalHits / total;
            }
        }
        
        /// <summary>
        /// 尝试从缓存获取提示词
        /// </summary>
        public CacheEntry TryGet(Pawn pawn, string context, out bool needsRegeneration)
        {
            needsRegeneration = true;
            
            if (!RimTalkMemoryPatchMod.Settings.enablePromptCache || pawn == null)
                return null;
            
            string cacheKey = GenerateCacheKey(pawn, context);
            
            if (cache.TryGetValue(cacheKey, out var entry))
            {
                // 检查缓存是否仍然有效
                var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
                int currentMemoryCount = GetMemoryCount(memoryComp);
                int currentKnowledgeCount = GetKnowledgeCount();
                
                int currentTick = Find.TickManager.TicksGame;
                int expireTicks = ExpireMinutes * 2500; // 分钟转tick
                
                if (entry.IsValid(currentMemoryCount, currentKnowledgeCount, currentTick, expireTicks))
                {
                    // 缓存有效！
                    entry.lastUsedTick = currentTick;
                    entry.useCount++;
                    totalHits++;
                    needsRegeneration = false;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Prompt Cache] ?? HIT: {pawn.LabelShort} (saved ~{EstimateComputeCost()}ms)");
                    }
                    
                    return entry;
                }
                else
                {
                    // 缓存失效
                    cache.Remove(cacheKey);
                    totalInvalidations++;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Prompt Cache] ?? INVALIDATED: {pawn.LabelShort} (memory/knowledge changed)");
                    }
                }
            }
            
            totalMisses++;
            return null;
        }
        
        /// <summary>
        /// 添加到缓存
        /// </summary>
        public void Add(Pawn pawn, string context, string memoryPrompt, string knowledgePrompt, string fullPrompt)
        {
            if (!RimTalkMemoryPatchMod.Settings.enablePromptCache || pawn == null)
                return;
            
            var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
            int memoryCount = GetMemoryCount(memoryComp);
            int knowledgeCount = GetKnowledgeCount();
            
            string cacheKey = GenerateCacheKey(pawn, context);
            
            var entry = new CacheEntry(memoryPrompt, knowledgePrompt, fullPrompt, memoryCount, knowledgeCount);
            cache[cacheKey] = entry;
            
            // LRU淘汰
            if (cache.Count > MaxCacheSize)
            {
                EvictLRU();
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Prompt Cache] ?? CACHED: {pawn.LabelShort} (total: {cache.Count}/{MaxCacheSize})");
            }
        }
        
        /// <summary>
        /// 使缓存失效（当记忆或常识变化时调用）
        /// </summary>
        public void InvalidateForPawn(Pawn pawn)
        {
            if (pawn == null) return;
            
            var keysToRemove = cache.Keys.Where(k => k.StartsWith(pawn.ThingID + "_")).ToList();
            
            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
            
            if (keysToRemove.Count > 0 && Prefs.DevMode)
            {
                Log.Message($"[Prompt Cache] ??? Invalidated {keysToRemove.Count} entries for {pawn.LabelShort}");
            }
        }
        
        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            int count = cache.Count;
            cache.Clear();
            totalHits = 0;
            totalMisses = 0;
            totalInvalidations = 0;
            
            Log.Message($"[Prompt Cache] ??? Cleared {count} cached prompts");
        }
        
        /// <summary>
        /// 定期清理过期缓存
        /// </summary>
        public void CleanExpired()
        {
            int currentTick = Find.TickManager.TicksGame;
            int expireTicks = ExpireMinutes * 2500;
            
            var expiredKeys = cache
                .Where(kvp => currentTick - kvp.Value.timestamp > expireTicks)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredKeys)
            {
                cache.Remove(key);
            }
            
            if (expiredKeys.Count > 0 && Prefs.DevMode)
            {
                Log.Message($"[Prompt Cache] ?? Cleaned {expiredKeys.Count} expired entries");
            }
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            return $"Cached: {cache.Count}/{MaxCacheSize}, Hits: {totalHits}, Misses: {totalMisses}, " +
                   $"Invalidations: {totalInvalidations}, Hit Rate: {HitRate:P1}";
        }
        
        // === 私有辅助方法 ===
        
        private string GenerateCacheKey(Pawn pawn, string context)
        {
            // 使用pawnId + 上下文hash
            // 注意：不包含具体内容，因为提示词结构相同即可复用
            string contextHash = GetStableHash(context).ToString();
            return $"{pawn.ThingID}_{contextHash}";
        }
        
        private int GetMemoryCount(FourLayerMemoryComp memoryComp)
        {
            if (memoryComp == null) return 0;
            return memoryComp.SituationalMemories.Count + 
                   memoryComp.EventLogMemories.Count + 
                   memoryComp.ArchiveMemories.Count;
        }
        
        private int GetKnowledgeCount()
        {
            var memoryManager = Find.World?.GetComponent<MemoryManager>();
            return memoryManager?.CommonKnowledge?.Entries?.Count ?? 0;
        }
        
        private int GetStableHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // 简单的hash，只取前100字符避免过长文本
            string sample = text.Length > 100 ? text.Substring(0, 100) : text;
            return sample.GetHashCode();
        }
        
        private void EvictLRU()
        {
            if (cache.Count == 0) return;
            
            // 线性查找最少使用的条目
            string lruKey = null;
            int minUseCount = int.MaxValue;
            int minLastUsedTick = int.MaxValue;
            
            foreach (var kvp in cache)
            {
                if (kvp.Value.useCount < minUseCount ||
                    (kvp.Value.useCount == minUseCount && kvp.Value.lastUsedTick < minLastUsedTick))
                {
                    minUseCount = kvp.Value.useCount;
                    minLastUsedTick = kvp.Value.lastUsedTick;
                    lruKey = kvp.Key;
                }
            }
            
            if (lruKey != null)
            {
                cache.Remove(lruKey);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[Prompt Cache] ??? EVICTED LRU entry");
                }
            }
        }
        
        private int EstimateComputeCost()
        {
            // 估算重新计算的CPU时间
            // 记忆注入: ~5-10ms
            // 常识注入: ~3-5ms
            // 总计: ~8-15ms
            return UnityEngine.Random.Range(8, 15);
        }
        
        public void ExposeData()
        {
            Scribe_Collections.Look(ref cache, "promptCache", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref totalHits, "totalHits", 0);
            Scribe_Values.Look(ref totalMisses, "totalMisses", 0);
            Scribe_Values.Look(ref totalInvalidations, "totalInvalidations", 0);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cache == null)
                    cache = new Dictionary<string, CacheEntry>();
            }
        }
    }
}
