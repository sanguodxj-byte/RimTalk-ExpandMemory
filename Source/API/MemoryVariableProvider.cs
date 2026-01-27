using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimTalk.MemoryPatch;
using RimTalkHistoryPlus;

namespace RimTalk.Memory.API
{
    /// <summary>
    /// 为 {{pawn.memory}} Mustache 变量提供内容
    /// 
    /// 当 RimTalk 解析模板时遇到 {{pawn1.memory}}，
    /// 会调用此 Provider 获取 pawn1 的记忆内容
    /// </summary>
    public static class MemoryVariableProvider
    {
        /// <summary>
        /// 获取 Pawn 的记忆内容
        /// 由 RimTalk Mustache Parser 在解析 {{pawnN.memory}} 时调用
        /// </summary>
        /// <param name="pawn">目标 Pawn（由 RimTalk 传入）</param>
        /// <returns>格式化的记忆文本</returns>
        public static string GetPawnMemory(Pawn pawn)
        {
            if (pawn == null) 
            {
                return "";
            }
            
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // 优先使用四层记忆系统
                var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
                if (fourLayerComp != null)
                {
                    return GetFourLayerMemories(pawn, fourLayerComp, settings);
                }
                
                // 回退到旧的记忆组件
                var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
                if (memoryComp != null)
                {
                    return GetLegacyMemories(memoryComp, settings);
                }
                
                return "(No memory component)";
            }
            catch (Exception ex)
            {
                Log.Warning($"[MemoryPatch] Error getting pawn memory for {pawn?.LabelShort}: {ex.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// ⭐ v4.2: 缓存每个 Pawn 的记忆结果，避免重复计算
        /// Key: Pawn.ThingID, Value: 记忆文本
        /// </summary>
        [ThreadStatic]
        private static Dictionary<string, string> _pawnMemoryCache;
        
        /// <summary>
        /// ⭐ v4.2: 上次缓存的时间戳
        /// </summary>
        [ThreadStatic]
        private static int _memoryCacheTick;
        
        /// <summary>
        /// ⭐ v4.2: 缓存有效期（2秒 = 120 ticks）
        /// </summary>
        private const int MEMORY_CACHE_EXPIRE_TICKS = 120;
        
        /// <summary>
        /// ⭐ v4.2: 获取四层记忆系统的记忆
        /// 结构：ABM（最近记忆）+ ELS/CLPA（总结后的记忆）
        /// 格式统一：序号. [类型] 内容 (时间)
        /// </summary>
        private static string GetFourLayerMemories(Pawn pawn, FourLayerMemoryComp comp, RimTalkMemoryPatchSettings settings)
        {
            string pawnId = pawn.ThingID;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            
            // ⭐ v4.2: 检查缓存是否有效
            if (_pawnMemoryCache == null || currentTick - _memoryCacheTick > MEMORY_CACHE_EXPIRE_TICKS)
            {
                _pawnMemoryCache = new Dictionary<string, string>();
                _memoryCacheTick = currentTick;
            }
            
            // ⭐ v4.2: 如果缓存中有这个 Pawn 的结果，直接返回
            if (_pawnMemoryCache.TryGetValue(pawnId, out string cachedResult))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[Memory] Using cached result for {pawn.LabelShort}");
                }
                return cachedResult;
            }
            
            var sb = new StringBuilder();
            
            // ⭐ v4.2: 第一部分 - ABM（最近记忆，支持跨 Pawn 去重）
            string abmContent = HistoryManager.InjectABM(pawn);
            
            if (!string.IsNullOrEmpty(abmContent))
            {
                sb.AppendLine(abmContent);
            }
            
            // ⭐ v4.0: 第二部分 - ELS/CLPA（总结后的记忆，通过关键词匹配）
            string dialogueContext = GetCurrentDialogueContext();
            string elsMemories = DynamicMemoryInjection.InjectMemories(
                pawn,
                dialogueContext,
                settings.maxInjectedMemories
            );
            
            if (!string.IsNullOrEmpty(elsMemories))
            {
                // 如果 ABM 有内容，加空行分隔
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.AppendLine(elsMemories);
            }
            
            // 如果都为空，返回最近记忆
            string result;
            if (sb.Length == 0)
            {
                result = GetRecentMemories(comp, settings.maxInjectedMemories);
            }
            else
            {
                result = sb.ToString().TrimEnd();
            }
            
            // ⭐ v4.2: 缓存结果
            _pawnMemoryCache[pawnId] = result;
            
            return result;
        }
        
        /// <summary>
        /// 获取最近的记忆（无匹配时的回退）
        /// </summary>
        private static string GetRecentMemories(FourLayerMemoryComp comp, int maxCount)
        {
            var recentMemories = new List<MemoryEntry>();
            
            // 从各层收集最近的记忆
            recentMemories.AddRange(comp.SituationalMemories.Take(maxCount / 2));
            recentMemories.AddRange(comp.EventLogMemories.Take(maxCount / 2));
            
            if (recentMemories.Count == 0)
            {
                return "(No memories yet)";
            }
            
            // 按时间排序
            var sortedMemories = recentMemories
                .OrderByDescending(m => m.timestamp)
                .Take(maxCount);
            
            return FormatMemories(sortedMemories);
        }
        
        /// <summary>
        /// 获取旧版记忆组件的记忆
        /// </summary>
        private static string GetLegacyMemories(PawnMemoryComp comp, RimTalkMemoryPatchSettings settings)
        {
            var memories = comp.GetRelevantMemories(settings.maxInjectedMemories);
            
            if (memories == null || memories.Count == 0)
            {
                return "(No memories yet)";
            }
            
            var sb = new StringBuilder();
            int index = 1;
            
            foreach (var memory in memories)
            {
                sb.AppendLine($"{index}. {memory.content} ({memory.TimeAgoString})");
                index++;
            }
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 格式化记忆列表
        /// </summary>
        private static string FormatMemories(IEnumerable<MemoryEntry> memories)
        {
            var sb = new StringBuilder();
            int index = 1;
            
            foreach (var memory in memories)
            {
                string typeTag = GetMemoryTypeTag(memory.type);
                sb.AppendLine($"{index}. [{typeTag}] {memory.content} ({memory.TimeAgoString})");
                index++;
            }
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 获取记忆类型标签
        /// </summary>
        private static string GetMemoryTypeTag(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Conversation:
                    return "Conversation";
                case MemoryType.Action:
                    return "Action";
                case MemoryType.Observation:
                    return "Observation";
                case MemoryType.Event:
                    return "Event";
                case MemoryType.Emotion:
                    return "Emotion";
                case MemoryType.Relationship:
                    return "Relationship";
                default:
                    return "Memory";
            }
        }
        
        /// <summary>
        /// 获取当前对话上下文（用于关键词匹配）
        /// 从 RimTalkMemoryAPI 获取缓存的上下文
        /// </summary>
        private static string GetCurrentDialogueContext()
        {
            try
            {
                // 从 RimTalkMemoryAPI 获取缓存的上下文
                var context = Patches.RimTalkMemoryAPI.GetLastRimTalkContext(out _, out int tick);
                
                // 检查缓存是否过期（60 ticks 内有效）
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                if (currentTick - tick > 60)
                {
                    return "";
                }
                
                return context ?? "";
            }
            catch
            {
                return "";
            }
        }
        
        // 注意：固定记忆(isPinned)不需要单独处理
        // DynamicMemoryInjection 已经给 isPinned 的记忆加了 0.5 的评分加成
        // 它们会自然地排在 {{memory}} 输出的前面
    }
}