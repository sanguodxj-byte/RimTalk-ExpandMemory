using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// Simple integration that exposes memory data through a public static API
    /// RimTalk can call these methods directly
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SimpleRimTalkIntegration
    {
        static SimpleRimTalkIntegration()
        {
            // AI总结器会通过自己的静态构造函数自动初始化
        }
    }

    /// <summary>
    /// Public API for RimTalk to access memory system
    /// </summary>
    public static class RimTalkMemoryAPI
    {
        // ⭐ 新增：缓存最后一次RimTalk请求的上下文
        private static string lastRimTalkContext = "";
        private static Pawn lastRimTalkPawn = null;
        private static int lastRimTalkTick = 0;
        
        // ⭐ v3.5.1: 新增 Prompt 缓存（用于 BuildContext 阶段匹配）
        // 由 DecoratePrompt Prefix 缓存，供 BuildContext Postfix 使用
        private static string cachedPromptForMatching = "";
        private static Pawn cachedPromptPawn = null;
        private static Pawn cachedPromptTargetPawn = null;
        private static int cachedPromptTick = 0;
        
        static RimTalkMemoryAPI()
        {
            // API已加载
        }
        
        /// <summary>
        /// ⭐ 新增：缓存上下文（由RimTalkPrecisePatcher调用）
        /// </summary>
        public static void CacheContext(Pawn pawn, string context)
        {
            lastRimTalkContext = context ?? "";
            lastRimTalkPawn = pawn;
            lastRimTalkTick = Find.TickManager?.TicksGame ?? 0;
        }
        
        /// <summary>
        /// ⭐ 新增：获取最后一次RimTalk请求的上下文
        /// </summary>
        public static string GetLastRimTalkContext(out Pawn pawn, out int tick)
        {
            pawn = lastRimTalkPawn;
            tick = lastRimTalkTick;
            return lastRimTalkContext;
        }
        
        /// <summary>
        /// ⭐ v3.5.1: 缓存 Prompt 用于匹配（由 DecoratePrompt Prefix 调用）
        /// 这允许 BuildContext Postfix 使用 Prompt 而非 Context 进行常识匹配
        /// </summary>
        public static void CachePromptForMatching(Pawn speaker, Pawn listener, string prompt)
        {
            cachedPromptForMatching = prompt ?? "";
            cachedPromptPawn = speaker;
            cachedPromptTargetPawn = listener;
            cachedPromptTick = Find.TickManager?.TicksGame ?? 0;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[RimTalkMemoryAPI] Cached Prompt for matching: {prompt?.Substring(0, Math.Min(50, prompt?.Length ?? 0))}...");
            }
        }
        
        /// <summary>
        /// ⭐ v3.5.1: 获取缓存的 Prompt（供 BuildContext 使用）
        /// </summary>
        public static string GetCachedPromptForMatching(out Pawn speaker, out Pawn listener)
        {
            speaker = cachedPromptPawn;
            listener = cachedPromptTargetPawn;
            
            // 检查缓存是否过期（超过 60 ticks 视为过期）
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick - cachedPromptTick > 60)
            {
                // 缓存过期，返回空
                return null;
            }
            
            return cachedPromptForMatching;
        }
        
        /// <summary>
        /// ⭐ v3.5.1: 清除 Prompt 缓存（在注入完成后调用）
        /// </summary>
        public static void ClearCachedPrompt()
        {
            cachedPromptForMatching = "";
            cachedPromptPawn = null;
            cachedPromptTargetPawn = null;
        }
        
        /// <summary>
        /// Get conversation prompt enhanced with pawn's memories
        /// 支持动态注入和静态注入
        /// 返回包含system_rule和user_prompt的完整结构
        /// ✅ v2.4.4: 增加智能缓存，避免重复计算记忆和常识注入
        /// </summary>
        public static string GetMemoryPrompt(Pawn pawn, string basePrompt)
        {
            if (pawn == null) return basePrompt;

            // ⭐ 缓存这次请求的上下文
            lastRimTalkContext = basePrompt ?? "";
            lastRimTalkPawn = pawn;
            lastRimTalkTick = Find.TickManager?.TicksGame ?? 0;

            // ⭐ 新增：尝试从提示词缓存获取
            var promptCache = MemoryManager.GetPromptCache();
            var cachedEntry = promptCache.TryGet(pawn, basePrompt, out bool needsRegeneration);
            
            if (!needsRegeneration && cachedEntry != null)
            {
                // 缓存命中！直接返回
                return cachedEntry.fullPrompt;
            }

            // 缓存未命中或失效，重新生成
            string memoryContext = "";
            string knowledgeContext = "";
            
            // 使用动态注入或静态注入
            if (RimTalkMemoryPatchMod.Settings.useDynamicInjection)
            {
                var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
                if (memoryComp != null)
                {
                    // 动态注入记忆
                    memoryContext = DynamicMemoryInjection.InjectMemories(
                        pawn, 
                        basePrompt, 
                        RimTalkMemoryPatchMod.Settings.maxInjectedMemories
                    );
                    
                    // 注入常识库
                    var memoryManager = Find.World?.GetComponent<MemoryManager>();
                    if (memoryManager != null)
                    {
                        knowledgeContext = memoryManager.CommonKnowledge.InjectKnowledge(
                            basePrompt,
                            RimTalkMemoryPatchMod.Settings.maxInjectedKnowledge
                        );
                    }
                }
            }
            else
            {
                // 静态注入（兼容旧版）
                var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
                if (memoryComp == null)
                {
                    return basePrompt;
                }

                memoryContext = memoryComp.GetMemoryContext();
            }
            
            // 如果没有任何上下文，直接返回原始提示
            if (string.IsNullOrEmpty(memoryContext) && string.IsNullOrEmpty(knowledgeContext))
            {
                return basePrompt;
            }

            // 构建system_rule格式
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("## System Rule");
            sb.AppendLine();
            
            // 常识库部分（更通用的知识）
            if (!string.IsNullOrEmpty(knowledgeContext))
            {
                sb.AppendLine("### World Knowledge");
                sb.AppendLine(knowledgeContext);
                sb.AppendLine();
            }
            
            // 角色记忆部分（个人经历）
            if (!string.IsNullOrEmpty(memoryContext))
            {
                sb.AppendLine("### Character Memories");
                sb.AppendLine(memoryContext);
                sb.AppendLine();
            }
            
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## User Prompt");
            sb.AppendLine(basePrompt);

            string fullPrompt = sb.ToString();
            
            // ⭐ 新增：缓存生成的提示词
            promptCache.Add(pawn, basePrompt, memoryContext, knowledgeContext, fullPrompt);

            return fullPrompt;
        }

        /// <summary>
        /// Get recent memories for a pawn
        /// </summary>
        public static System.Collections.Generic.List<MemoryEntry> GetRecentMemories(Pawn pawn, int count = 5)
        {
            var memoryComp = pawn?.TryGetComp<PawnMemoryComp>();
            return memoryComp?.GetRelevantMemories(count) ?? new System.Collections.Generic.List<MemoryEntry>();
        }

        /// <summary>
        /// Record a conversation between two pawns
        /// </summary>
        public static void RecordConversation(Pawn speaker, Pawn listener, string content)
        {
            // 直接调用底层方法
            MemoryAIIntegration.RecordConversation(speaker, listener, content);
        }

        /// <summary>
        /// Check if a pawn has the memory component
        /// </summary>
        public static bool HasMemoryComponent(Pawn pawn)
        {
            return pawn?.TryGetComp<PawnMemoryComp>() != null;
        }

        /// <summary>
        /// Get memory summary for debugging
        /// </summary>
        public static string GetMemorySummary(Pawn pawn)
        {
            var memoryComp = pawn?.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null) return "No memory component";

            int shortTerm = memoryComp.ShortTermMemories.Count;
            int longTerm = memoryComp.LongTermMemories.Count;
            
            return $"{pawn.LabelShort}: {shortTerm} short-term, {longTerm} long-term memories";
        }
        
        /// <summary>
        /// 尝试从缓存获取对话（新增）
        /// </summary>
        public static string TryGetCachedDialogue(Pawn speaker, Pawn listener, string topic)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableConversationCache)
                return null;
            
            string cacheKey = CacheKeyGenerator.Generate(speaker, listener, topic);
            if (string.IsNullOrEmpty(cacheKey))
                return null;
            
            var cache = MemoryManager.GetConversationCache();
            return cache.TryGet(cacheKey);
        }
        
        /// <summary>
        /// 添加对话到缓存（新增）
        /// </summary>
        public static void CacheDialogue(Pawn speaker, Pawn listener, string topic, string dialogue)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableConversationCache)
                return;
            
            string cacheKey = CacheKeyGenerator.Generate(speaker, listener, topic);
            if (string.IsNullOrEmpty(cacheKey))
                return;
            
            var cache = MemoryManager.GetConversationCache();
            cache.Add(cacheKey, dialogue);
        }
        
        /// <summary>
        /// 获取缓存统计信息（新增）
        /// </summary>
        public static string GetCacheStats()
        {
            var cache = MemoryManager.GetConversationCache();
            return cache.GetStats();
        }
        
        /// <summary>
        /// 清空对话缓存（新增）
        /// </summary>
        public static void ClearConversationCache()
        {
            var cache = MemoryManager.GetConversationCache();
            cache.Clear();
        }
    }

    /// <summary>
    /// InteractionWorker patch - REMOVED
    /// 
    /// 互动记忆功能已完全移除，原因：
    /// 1. 互动记忆只有类型标签（如"闲聊"），无具体对话内容
    /// 2. RimTalk对话记忆已完整记录所有对话内容
    /// 3. 互动记忆与对话记忆冗余，无实际价值
    /// 4. 实现复杂，容易产生重复记录等bug
    /// 5. 不符合用户期望（用户需要的是对话内容，不是互动类型标签）
    /// 
    /// 现在只保留：
    /// - 对话记忆（Conversation）：RimTalk生成的完整对话内容
    /// - 行动记忆（Action）：工作、战斗等行为记录
    /// </summary>

    /// <summary>
    /// Helper to get private/public properties via reflection
    /// </summary>
    public static class ReflectionHelper
    {
        public static T GetProp<T>(this object obj, string propertyName) where T : class
        {
            try
            {
                var traverse = Traverse.Create(obj);
                return traverse.Field(propertyName).GetValue<T>() ?? 
                       traverse.Property(propertyName).GetValue<T>();
            }
            catch
            {
                return null;
            }
        }
    }
}
