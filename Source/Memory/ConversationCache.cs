using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 对话缓存系统 - 减少API调用，提升响应速度
    /// Phase 1: 简单实现，基于场景的缓存
    /// </summary>
    public class ConversationCache : IExposable
    {
        /// <summary>
        /// 缓存条目
        /// </summary>
        public class CacheEntry : IExposable
        {
            public string dialogue;           // 对话内容
            public int timestamp;             // 创建时间戳
            public int lastUsedTick;          // 最后使用时间
            public int useCount;              // 使用次数
            
            public CacheEntry()
            {
                // 无参构造函数（用于反序列化）
            }
            
            public CacheEntry(string dialogue, int timestamp)
            {
                this.dialogue = dialogue;
                this.timestamp = timestamp;
                this.lastUsedTick = timestamp;
                this.useCount = 1;
            }
            
            /// <summary>
            /// 检查是否过期
            /// </summary>
            public bool IsExpired(int currentTick, int expireTicks)
            {
                return (currentTick - timestamp) > expireTicks;
            }
            
            public void ExposeData()
            {
                Scribe_Values.Look(ref dialogue, "dialogue");
                Scribe_Values.Look(ref timestamp, "timestamp");
                Scribe_Values.Look(ref lastUsedTick, "lastUsedTick");
                Scribe_Values.Look(ref useCount, "useCount");
            }
        }
        
        // 缓存字典
        private Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        
        // 统计数据
        private int totalHits = 0;
        private int totalMisses = 0;
        
        // 配置（从设置读取）
        private int MaxCacheSize => RimTalkMemoryPatchMod.Settings.conversationCacheSize;
        private int ExpireDays => RimTalkMemoryPatchMod.Settings.conversationCacheExpireDays;
        
        /// <summary>
        /// 缓存命中率
        /// </summary>
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
        /// 尝试从缓存获取对话
        /// </summary>
        public string TryGet(string cacheKey)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableConversationCache)
                return null;
            
            CleanExpiredEntries();
            
            if (cache.TryGetValue(cacheKey, out var entry))
            {
                int currentTick = Find.TickManager.TicksGame;
                int expireTicks = ExpireDays * 60000; // 转换为ticks
                
                if (!entry.IsExpired(currentTick, expireTicks))
                {
                    // 缓存命中
                    entry.lastUsedTick = currentTick;
                    entry.useCount++;
                    totalHits++;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Conversation Cache] 🎯 HIT: {cacheKey.Substring(0, Math.Min(30, cacheKey.Length))}...");
                    }
                    
                    return entry.dialogue;
                }
                else
                {
                    // 过期，移除
                    cache.Remove(cacheKey);
                }
            }
            
            totalMisses++;
            return null;
        }
        
        /// <summary>
        /// 添加对话到缓存
        /// </summary>
        public void Add(string cacheKey, string dialogue)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableConversationCache)
                return;
            
            if (string.IsNullOrEmpty(dialogue))
                return;
            
            int currentTick = Find.TickManager.TicksGame;
            
            if (cache.ContainsKey(cacheKey))
            {
                // 更新现有条目
                cache[cacheKey] = new CacheEntry(dialogue, currentTick);
            }
            else
            {
                // 新建条目
                cache[cacheKey] = new CacheEntry(dialogue, currentTick);
                
                // LRU淘汰
                if (cache.Count > MaxCacheSize)
                {
                    EvictLRU();
                }
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Conversation Cache] 💾 ADD: {cacheKey.Substring(0, Math.Min(30, cacheKey.Length))}... (total: {cache.Count})");
            }
        }
        
        /// <summary>
        /// LRU淘汰策略 - 移除最少使用的条目
        /// </summary>
        private void EvictLRU()
        {
            if (cache.Count == 0) return;
            
            // 找到最少使用的条目（综合考虑使用次数和最后使用时间）
            var lruEntry = cache
                .OrderBy(kvp => kvp.Value.useCount)
                .ThenBy(kvp => kvp.Value.lastUsedTick)
                .First();
            
            cache.Remove(lruEntry.Key);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Conversation Cache] 🗑️ EVICT: {lruEntry.Key.Substring(0, Math.Min(30, lruEntry.Key.Length))}...");
            }
        }
        
        /// <summary>
        /// 清理过期条目
        /// </summary>
        private void CleanExpiredEntries()
        {
            int currentTick = Find.TickManager.TicksGame;
            int expireTicks = ExpireDays * 60000;
            
            var expiredKeys = cache
                .Where(kvp => kvp.Value.IsExpired(currentTick, expireTicks))
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredKeys)
            {
                cache.Remove(key);
            }
            
            if (expiredKeys.Count > 0 && Prefs.DevMode)
            {
                Log.Message($"[Conversation Cache] 🧹 Cleaned {expiredKeys.Count} expired entries");
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
            
            Log.Message($"[Conversation Cache] 🗑️ Cleared {count} cached conversations");
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            return $"Cached: {cache.Count}/{MaxCacheSize}, Hits: {totalHits}, Misses: {totalMisses}, Hit Rate: {HitRate:P1}";
        }
        
        public void ExposeData()
        {
            Scribe_Collections.Look(ref cache, "conversationCache", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref totalHits, "totalHits", 0);
            Scribe_Values.Look(ref totalMisses, "totalMisses", 0);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cache == null)
                    cache = new Dictionary<string, CacheEntry>();
            }
        }
    }
    
    /// <summary>
    /// 缓存键生成器
    /// </summary>
    public static class CacheKeyGenerator
    {
        /// <summary>
        /// 生成对话缓存键
        /// ⭐ v3.3.4: 优化为更粗粒度的键，提高缓存命中率
        /// </summary>
        public static string Generate(Pawn speaker, Pawn listener, string topic)
        {
            if (speaker == null || listener == null)
                return null;
            
            // 基础信息
            string speakerName = speaker.LabelShort;
            string listenerName = listener.LabelShort;
            
            // ⭐ 优化1：简化情绪等级（4级→2级）
            string moodLevel = GetMoodLevel(speaker.needs?.mood?.CurLevel ?? 0.5f);
            
            // ⭐ 优化2：简化关系等级（5级→2级）
            string relationLevel = GetRelationLevel(speaker, listener);
            
            // ⭐ 优化3：移除topic hash - 话题变化不应导致缓存失效
            // 大多数对话内容相似，话题只是细微差别
            
            // 组合缓存键 - 更简单=更高命中率
            return $"{speakerName}_{listenerName}_{moodLevel}_{relationLevel}";
        }
        
        /// <summary>
        /// 获取情绪等级（粗粒度分级，提高缓存复用）
        /// ⭐ v3.3.4: 4级→2级，命中率提升约2倍
        /// </summary>
        private static string GetMoodLevel(float mood)
        {
            // 之前：happy(>0.7), neutral(0.4-0.7), sad(0.2-0.4), miserable(<0.2) = 4种状态
            // 现在：positive(>0.4), negative(≤0.4) = 2种状态
            // 理由：情绪微小波动不应导致对话内容巨大变化
            if (mood > 0.4f) return "positive";
            return "negative";
        }
        
        /// <summary>
        /// 获取关系等级（粗粒度分级，提高缓存复用）
        /// ⭐ v3.3.4: 5级→2级，命中率提升约2.5倍
        /// </summary>
        private static string GetRelationLevel(Pawn speaker, Pawn listener)
        {
            if (speaker.relations == null || listener.relations == null)
                return "neutral";
            
            int opinion = speaker.relations.OpinionOf(listener);
            
            // 之前：friend(>50), friendly(0-50), neutral(-20-0), unfriendly(-50--20), hostile(<-50) = 5种状态
            // 现在：positive(>0), negative(≤0) = 2种状态
            // 理由：opinion在±20内的波动是正常的，不应导致对话截然不同
            if (opinion > 0) return "positive";
            return "negative";
        }
    }
}
