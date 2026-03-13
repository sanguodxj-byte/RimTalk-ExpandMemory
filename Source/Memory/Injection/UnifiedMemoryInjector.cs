using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Injection
{
    /// <summary>
    /// 统一记忆注入调度器
    /// 职责：配额管理 + 管线调度
    /// 
    /// 解决的问题：
    /// 1. 对话记忆优先（ABMCollector 实现）
    /// 2. ABM 占用总配额（本类实现）
    /// 3. 序号连续（MemoryFormatter 实现）
    /// </summary>
    public static class UnifiedMemoryInjector
    {
        /// <summary>
        /// 注入记忆主入口
        /// 
        /// 流程：
        /// 1. 采集 ABM（对话优先 + 行为补位）- 使用 maxABMInjectionRounds
        /// 2. 计算剩余配额 = maxTotalMemories - ABM实际数
        /// 3. 采集 ELS/CLPA（关键词匹配）- 使用剩余配额
        /// 4. 统一编号输出
        /// </summary>
        /// <param name="pawn">目标 Pawn</param>
        /// <param name="dialogueContext">对话上下文（用于 ELS/CLPA 匹配）</param>
        /// <returns>格式化的记忆文本，序号连续</returns>
        public static string Inject(Pawn pawn, string dialogueContext)
        {
            if (pawn == null)
                return string.Empty;
            
            var settings = RimTalkMemoryPatchMod.Settings;
            int maxABMRounds = settings?.maxABMInjectionRounds ?? 3;
            int maxTotalMemories = settings?.maxInjectedMemories ?? 10;

            // Step 1: 采集 ABM（对话优先 + 行为补位）
            // 【ABM全额注入】开关的作用由 maxABMInjectionRounds 配置项接管
            var abmList = ABMCollector.Collect(pawn, maxABMRounds);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[UnifiedMemoryInjector] ABM collected: {abmList.Count}/{maxABMRounds} for {pawn.LabelShort}");
            }
            
            // Step 2: 计算剩余配额
            int remainingQuota = maxTotalMemories - abmList.Count;
            
            // Step 3: 采集 ELS/CLPA（如果有剩余配额）
            var elsList = new List<MemoryEntry>();
            if (remainingQuota > 0)
            {
                elsList = ELSCollector.Collect(pawn, dialogueContext, remainingQuota);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[UnifiedMemoryInjector] ELS/CLPA collected: {elsList.Count}/{remainingQuota} for {pawn.LabelShort}");
                }
            }
            
            // Step 4: 合并记忆列表
            var allMemories = new List<MemoryEntry>();
            allMemories.AddRange(abmList);
            allMemories.AddRange(elsList);
            
            if (allMemories.Count == 0)
            {
                return string.Empty;
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[UnifiedMemoryInjector] Total memories: {allMemories.Count}/{maxTotalMemories} for {pawn.LabelShort}");
            }
            
            // Step 5: 统一编号格式化
            return MemoryFormatter.Format(allMemories, startIndex: 1);
        }
        
        /// <summary>
        /// 注入记忆（带详细信息返回）
        /// 用于调试和预览
        /// </summary>
        public static string InjectWithDetails(
            Pawn pawn, 
            string dialogueContext,
            out int abmCount,
            out int elsCount,
            out int totalCount)
        {
            abmCount = 0;
            elsCount = 0;
            totalCount = 0;
            
            if (pawn == null)
                return string.Empty;
            
            var settings = RimTalkMemoryPatchMod.Settings;
            int maxABMRounds = settings?.maxABMInjectionRounds ?? 3;
            int maxTotalMemories = settings?.maxInjectedMemories ?? 10;
            
            // 采集 ABM
            var abmList = ABMCollector.Collect(pawn, maxABMRounds);
            abmCount = abmList.Count;
            
            // 计算剩余配额
            int remainingQuota = maxTotalMemories - abmList.Count;
            
            // 采集 ELS/CLPA
            var elsList = new List<MemoryEntry>();
            if (remainingQuota > 0)
            {
                elsList = ELSCollector.Collect(pawn, dialogueContext, remainingQuota);
            }
            elsCount = elsList.Count;
            
            // 合并
            var allMemories = new List<MemoryEntry>();
            allMemories.AddRange(abmList);
            allMemories.AddRange(elsList);
            totalCount = allMemories.Count;
            
            if (allMemories.Count == 0)
            {
                return string.Empty;
            }
            
            return MemoryFormatter.Format(allMemories, startIndex: 1);
        }
        
        /// <summary>
        /// 仅采集 ABM（用于独立的 {{pawn.ABM}} 变量）
        /// 向后兼容：保留原有的 ABM 独立变量功能
        /// </summary>
        public static string InjectABMOnly(Pawn pawn)
        {
            if (pawn == null)
                return string.Empty;
            
            var settings = RimTalkMemoryPatchMod.Settings;
            int maxABMRounds = settings?.maxABMInjectionRounds ?? 3;
            
            var abmList = ABMCollector.Collect(pawn, maxABMRounds);
            
            if (abmList.Count == 0)
            {
                return "(No ABM memories)";
            }
            
            return MemoryFormatter.Format(abmList, startIndex: 1);
        }
    }
}