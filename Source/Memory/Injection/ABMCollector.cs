using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalk.Data;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Injection
{
    /// <summary>
    /// ABM 采集器
    /// 职责：对话优先 + 行为补位
    /// 去重逻辑复用 RoundMemoryManager.RoundMemoryCache
    /// </summary>
    public static class ABMCollector
    {
        /// <summary>
        /// 采集 ABM 记忆
        /// 规则：
        /// 1. 对话记忆优先（先排序，对话类型排在前面）
        /// 2. 行为记忆补位
        /// 3. 只对 RoundMemory 类型做跨 Pawn 去重（与旧代码一致）
        /// </summary>
        /// <param name="pawn">目标 Pawn</param>
        /// <param name="maxRounds">最大采集轮数</param>
        /// <returns>采集到的记忆列表</returns>
        public static bool IsRoundMemoryEnabled => RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? false; // 不启用轮次记忆时不注入轮次记忆
        public static List<MemoryEntry> Collect(Pawn pawn, int maxRounds)
        {
            var result = new List<MemoryEntry>();
            
            if (pawn == null || maxRounds <= 0)
                return result;
            
            var comp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (comp == null || comp.ActiveMemories == null || comp.ActiveMemories.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[ABMCollector] {pawn?.LabelShort}: No ActiveMemories found (comp={comp != null}, count={comp?.ActiveMemories?.Count ?? 0})");
                }
                return result;
            }

            // 触发 RoundMemoryManager 的去重缓存自动重置
            // 改为patch scriban渲染方法，在每次渲染前重置
            // RoundMemoryManager.AutoReset();

            // ⭐ v5.0: 对话优先排序
            // 先按类型排序（Conversation 在前），再按时间降序
            var sortedList = comp.ActiveMemories
                .OrderBy(m => m.type == MemoryType.Conversation ? 0 : 1)  // 对话优先
                .ThenByDescending(m => m.timestamp)  // 然后按时间降序
                .ToList();
            
            var cache = RoundMemoryManager.Instance?.RoundMemoryCache;
            int stackedLength = 0;
            int stackedCount = 0;
            int skippedDuplicate = 0;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[ABMCollector] {pawn.LabelShort}: Processing {sortedList.Count} memories, cache size={cache?.Count ?? 0}");
            }
            
            foreach (var entry in sortedList)
            {
                // 达到数量上限
                if (stackedCount >= maxRounds)
                    break;
                
                // 达到长度上限
                if (stackedLength > RoundMemoryManager.MaxInjectedLength)
                    break;
                
                // ⭐ 关键：只对 RoundMemory 类型做去重（与旧代码逻辑一致）
                if (entry is RoundMemory roundMemory)
                {
                    if (!IsRoundMemoryEnabled) continue;
                    // 跨 Pawn 去重
                    if (cache != null && cache.Contains(roundMemory))
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Message("[ABMCollector] Skipped duplicate RoundMemory");
                        }
                        continue;
                    }
                    
                    // 添加到去重缓存
                    cache?.Add(roundMemory);
                }
                // 非 RoundMemory 不做去重，直接添加（与旧代码一致）
                
                // 文本长度处理
                int contentLength = entry.content?.Length ?? 0;
                stackedLength += contentLength;
                stackedCount++;
                
                result.Add(entry);
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取采集到的 ABM 数量（用于计算剩余配额）
        /// 这是一个便捷方法，实际采集在 Collect 中完成
        /// </summary>
        public static int GetCollectedCount(List<MemoryEntry> collected)
        {
            return collected?.Count ?? 0;
        }
    }
}