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
        /// </summary>
        public static string Generate(Pawn speaker, Pawn listener, string topic)
        {
            if (speaker == null || listener == null)
                return null;
            
            // 基础信息
            string speakerName = speaker.LabelShort;
            string listenerName = listener.LabelShort;
            
            // 情绪等级（模糊化）
            string moodLevel = GetMoodLevel(speaker.needs?.mood?.CurLevel ?? 0.5f);
            
            // 关系等级（模糊化）
            string relationLevel = GetRelationLevel(speaker, listener);
            
            // 话题（如果有）
            string topicHash = string.IsNullOrEmpty(topic) ? "general" : topic.GetHashCode().ToString();
            
            // 组合缓存键
            return $"{speakerName}_{listenerName}_{moodLevel}_{relationLevel}_{topicHash}";
        }
        
        /// <summary>
        /// 获取情绪等级（模糊化避免过度细分）
        /// </summary>
        private static string GetMoodLevel(float mood)
        {
            if (mood > 0.7f) return "happy";
            if (mood > 0.4f) return "neutral";
            if (mood > 0.2f) return "sad";
            return "miserable";
        }
        
        /// <summary>
        /// 获取关系等级（模糊化）
        /// </summary>
        private static string GetRelationLevel(Pawn speaker, Pawn listener)
        {
            if (speaker.relations == null || listener.relations == null)
                return "neutral";
            
            int opinion = speaker.relations.OpinionOf(listener);
            
            if (opinion > 50) return "friend";
            if (opinion > 0) return "friendly";
            if (opinion > -20) return "neutral";
            if (opinion > -50) return "unfriendly";
            return "hostile";
        }
    }
}
