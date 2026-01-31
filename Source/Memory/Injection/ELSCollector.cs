using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Injection
{
    /// <summary>
    /// ELS/CLPA 采集器
    /// 职责：关键词匹配 + 评分排序
    /// 复用 DynamicMemoryInjection 的评分逻辑
    /// </summary>
    public static class ELSCollector
    {
        /// <summary>
        /// 采集 ELS/CLPA 记忆
        /// 使用 DynamicMemoryInjection 的评分逻辑进行关键词匹配
        /// </summary>
        /// <param name="pawn">目标 Pawn</param>
        /// <param name="context">对话上下文（用于关键词匹配）</param>
        /// <param name="maxCount">最大采集数量</param>
        /// <returns>采集到的记忆列表（已按评分排序）</returns>
        public static List<MemoryEntry> Collect(Pawn pawn, string context, int maxCount)
        {
            var result = new List<MemoryEntry>();
            
            if (pawn == null || maxCount <= 0)
                return result;
            
            var comp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (comp == null)
                return result;
            
            // 复用 DynamicMemoryInjection 的评分逻辑
            // InjectMemoriesWithDetails 会返回按评分排序的结果
            DynamicMemoryInjection.InjectMemoriesWithDetails(
                comp,
                context,
                maxCount,
                out var scores
            );
            
            if (scores == null || scores.Count == 0)
                return result;
            
            // 提取记忆实体
            result = scores.Select(s => s.Memory).ToList();
            
            if (Prefs.DevMode && result.Count > 0)
            {
                Log.Message($"[ELSCollector] Collected {result.Count} memories for {pawn.LabelShort}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 采集 ELS 层记忆（只包含 EventLog 层）
        /// </summary>
        public static List<MemoryEntry> CollectELSOnly(Pawn pawn, string context, int maxCount)
        {
            var all = Collect(pawn, context, maxCount * 2); // 多取一些，再过滤
            return all
                .Where(m => m.layer == MemoryLayer.EventLog)
                .Take(maxCount)
                .ToList();
        }
        
        /// <summary>
        /// 采集 CLPA 层记忆（只包含 Archive 层）
        /// </summary>
        public static List<MemoryEntry> CollectCLPAOnly(Pawn pawn, string context, int maxCount)
        {
            var all = Collect(pawn, context, maxCount * 2); // 多取一些，再过滤
            return all
                .Where(m => m.layer == MemoryLayer.Archive)
                .Take(maxCount)
                .ToList();
        }
    }
}