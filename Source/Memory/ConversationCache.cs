using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 对话缓存系统 - 减少API调用，提升响应速度
    /// ⭐ v3.3.2.34: 重构为真正的 O(1) LRU 缓存
    /// 使用 LinkedList + Dictionary 组合结构实现
    /// </summary>
    public class ConversationCache : IExposable
    {
        /// <summary>
        /// 缓存条目
        /// </summary>
        public class CacheEntry : IExposable
        {
            public string cacheKey;           // ⭐ 新增：缓存键（用于反向查找）
            public string dialogue;           // 对话内容
            public int timestamp;             // 创建时间戳
            public int lastUsedTick;          // 最后使用时间
            public int useCount;              // 使用次数
            
            public CacheEntry()
            {
                // 无参构造函数（用于反序列化）
            }
            
            public CacheEntry(string cacheKey, string dialogue, int timestamp)
            {
                this.cacheKey = cacheKey;
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
                Scribe_Values.Look(ref cacheKey, "cacheKey");
                Scribe_Values.Look(ref dialogue, "dialogue");
                Scribe_Values.Look(ref timestamp, "timestamp");
                Scribe_Values.Look(ref lastUsedTick, "lastUsedTick");
                Scribe_Values.Look(ref useCount, "useCount");
            }
        }
        
        // ⭐ v3.3.2.34: O(1) LRU 缓存核心数据结构
        // 链表：维护访问顺序（头部=最近使用，尾部=最少使用）
        private LinkedList<CacheEntry> lruList = new LinkedList<CacheEntry>();
        
        // 字典：快速查找节点（O(1)）
        private Dictionary<string, LinkedListNode<CacheEntry>> cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>();
        
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
        /// ⭐ v3.3.2.34: O(1) TryGet - 命中时移动节点到链表头部
        /// </summary>
        public string TryGet(string cacheKey)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableConversationCache)
                return null;
            
            // ⭐ 延迟清理：只在必要时清理（避免每次都遍历）
            if (UnityEngine.Random.value < 0.1f) // 10% 概率清理过期条目
            {
                CleanExpiredEntries();
            }
            
            if (cacheMap.TryGetValue(cacheKey, out var node))
            {
                int currentTick = Find.TickManager.TicksGame;
                int expireTicks = ExpireDays * 60000; // 转换为ticks
                
                var entry = node.Value;
                
                if (!entry.IsExpired(currentTick, expireTicks))
                {
                    // ⭐ 缓存命中 - O(1) 操作
                    entry.lastUsedTick = currentTick;
                    entry.useCount++;
                    totalHits++;
                    
                    // ⭐ 移动到链表头部（表示最近使用）- O(1) 操作
                    lruList.Remove(node);
                    lruList.AddFirst(node);
                    
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                    {
                        Log.Message($"[Cache] 🎯 HIT: {cacheKey.Substring(0, Math.Min(30, cacheKey.Length))}... (uses: {entry.useCount})");
                    }
                    
                    return entry.dialogue;
                }
                else
                {
                    // 过期，移除 - O(1) 操作
                    lruList.Remove(node);
                    cacheMap.Remove(cacheKey);
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Cache] ⏰ EXPIRED: {cacheKey.Substring(0, Math.Min(30, cacheKey.Length))}...");
                    }
                }
            }
            
            totalMisses++;
            return null;
        }
        
        /// <summary>
        /// ⭐ v3.3.2.34: O(1) Add - 添加到链表头部，超容量时移除尾部
        /// </summary>
        public void Add(string cacheKey, string dialogue)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableConversationCache)
                return;
            
            if (string.IsNullOrEmpty(dialogue))
                return;
            
            int currentTick = Find.TickManager.TicksGame;
            
            if (cacheMap.TryGetValue(cacheKey, out var existingNode))
            {
                // ⭐ 更新现有条目 - O(1) 操作
                var entry = existingNode.Value;
                entry.dialogue = dialogue;
                entry.timestamp = currentTick;
                entry.lastUsedTick = currentTick;
                entry.useCount = 1; // 重置使用次数
                
                // 移动到链表头部
                lruList.Remove(existingNode);
                lruList.AddFirst(existingNode);
            }
            else
            {
                // ⭐ 新建条目 - O(1) 操作
                var newEntry = new CacheEntry(cacheKey, dialogue, currentTick);
                var newNode = new LinkedListNode<CacheEntry>(newEntry);
                
                // 添加到链表头部
                lruList.AddFirst(newNode);
                cacheMap[cacheKey] = newNode;
                
                // ⭐ LRU淘汰：超过容量时移除链表尾部（最少使用）- O(1) 操作
                if (cacheMap.Count > MaxCacheSize)
                {
                    EvictLRU_O1();
                }
            }
            
            if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
            {
                Log.Message($"[Cache] 💾 ADD: {cacheKey.Substring(0, Math.Min(30, cacheKey.Length))}... (total: {cacheMap.Count})");
            }
        }
        
        /// <summary>
        /// ⭐ v3.3.2.34: O(1) LRU淘汰 - 移除链表尾部节点（最少使用）
        /// </summary>
        private void EvictLRU_O1()
        {
            if (lruList.Count == 0) return;
            
            // ⭐ 移除链表尾部（最少使用的条目）- O(1) 操作
            var lruNode = lruList.Last;
            var lruEntry = lruNode.Value;
            
            lruList.RemoveLast();
            cacheMap.Remove(lruEntry.cacheKey);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Cache] 🗑️ EVICT: {lruEntry.cacheKey.Substring(0, Math.Min(30, lruEntry.cacheKey.Length))}... (uses: {lruEntry.useCount})");
            }
        }
        
        /// <summary>
        /// ⭐ v3.3.2.34: 优化的过期清理 - 只清理链表尾部的过期条目
        /// </summary>
        private void CleanExpiredEntries()
        {
            int currentTick = Find.TickManager.TicksGame;
            int expireTicks = ExpireDays * 60000;
            int cleanedCount = 0;
            
            // ⭐ 优化：从链表尾部开始清理（尾部的条目更可能过期）
            // 遇到未过期的条目时停止（因为头部的条目更新鲜）
            while (lruList.Count > 0)
            {
                var tailNode = lruList.Last;
                var tailEntry = tailNode.Value;
                
                if (tailEntry.IsExpired(currentTick, expireTicks))
                {
                    lruList.RemoveLast();
                    cacheMap.Remove(tailEntry.cacheKey);
                    cleanedCount++;
                }
                else
                {
                    // 遇到未过期的条目，停止清理
                    break;
                }
            }
            
            if (cleanedCount > 0 && Prefs.DevMode)
            {
                Log.Message($"[Cache] 🧹 Cleaned {cleanedCount} expired entries");
            }
        }
        
        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            int count = cacheMap.Count;
            lruList.Clear();
            cacheMap.Clear();
            totalHits = 0;
            totalMisses = 0;
            
            Log.Message($"[Cache] 🗑️ Cleared {count} cached conversations");
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            return $"Cached: {cacheMap.Count}/{MaxCacheSize}, Hits: {totalHits}, Misses: {totalMisses}, Hit Rate: {HitRate:P1}";
        }
        
        /// <summary>
        /// ⭐ v3.3.2.34: 序列化支持 - 从 LinkedList 重建
        /// </summary>
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 保存时：将 LinkedList 转换为 List
                var cacheList = new List<CacheEntry>(lruList);
                Scribe_Collections.Look(ref cacheList, "conversationCache", LookMode.Deep);
                Scribe_Values.Look(ref totalHits, "totalHits", 0);
                Scribe_Values.Look(ref totalMisses, "totalMisses", 0);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // 加载时：从 List 重建 LinkedList 和 Dictionary
                var cacheList = new List<CacheEntry>();
                Scribe_Collections.Look(ref cacheList, "conversationCache", LookMode.Deep);
                Scribe_Values.Look(ref totalHits, "totalHits", 0);
                Scribe_Values.Look(ref totalMisses, "totalMisses", 0);
                
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    // 重建数据结构
                    lruList = new LinkedList<CacheEntry>();
                    cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>();
                    
                    if (cacheList != null)
                    {
                        foreach (var entry in cacheList)
                        {
                            if (!string.IsNullOrEmpty(entry.cacheKey))
                            {
                                var node = new LinkedListNode<CacheEntry>(entry);
                                lruList.AddLast(node);
                                cacheMap[entry.cacheKey] = node;
                            }
                        }
                        
                        Log.Message($"[Cache] Loaded {cacheMap.Count} cached conversations");
                    }
                }
            }
            else if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 确保初始化
                if (lruList == null)
                    lruList = new LinkedList<CacheEntry>();
                if (cacheMap == null)
                    cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>();
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
