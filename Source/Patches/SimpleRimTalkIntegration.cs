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
    /// RimTalk AI 总结器 - 通过反射调用 RimTalk 的 AI API
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkAISummarizer
    {
        private static bool isAvailable = false;
        private static Type talkRequestType = null;
        private static Type aiServiceType = null;
        private static Type talkResponseType = null;
        private static MethodInfo chatMethod = null;
        private static Type settingsType = null;
        private static MethodInfo getSettingsMethod = null;

        static RimTalkAISummarizer()
        {
            try
            {
                // 查找 RimTalk 程序集
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");

                if (rimTalkAssembly == null)
                {
                    return;
                }

                // 查找类型
                talkRequestType = rimTalkAssembly.GetType("RimTalk.Data.TalkRequest");
                if (talkRequestType == null) return;

                aiServiceType = rimTalkAssembly.GetType("RimTalk.Service.AIService");
                if (aiServiceType == null) return;

                chatMethod = aiServiceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Chat" && 
                                       m.GetParameters().Length == 2 &&
                                       m.GetParameters()[0].ParameterType.Name == "TalkRequest");
                
                if (chatMethod == null) return;

                talkResponseType = rimTalkAssembly.GetType("RimTalk.Data.TalkResponse");
                if (talkResponseType == null) return;

                // 查找 Settings
                settingsType = rimTalkAssembly.GetType("RimTalk.Settings");
                if (settingsType != null)
                {
                    getSettingsMethod = settingsType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    
                    if (getSettingsMethod != null)
                    {
                        var settings = getSettingsMethod.Invoke(null, null);
                        var isEnabledProp = settingsType.GetProperty("IsEnabled");
                        if (isEnabledProp != null && settings != null)
                        {
                            bool isEnabled = (bool)isEnabledProp.GetValue(settings);
                            if (!isEnabled)
                            {
                                return;
                            }
                        }
                    }
                }

                isAvailable = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk AI Summarizer] Initialization failed: {ex.Message}");
                isAvailable = false;
            }
        }

        /// <summary>
        /// 检查 AI 总结是否可用
        /// </summary>
        public static bool IsAvailable()
        {
            return isAvailable;
        }

        /// <summary>
        /// 使用自定义提示词进行 AI 总结
        /// </summary>
        public static string SummarizeMemoriesWithPrompt(Pawn pawn, string customPrompt)
        {
            if (!isAvailable) return null;
            if (string.IsNullOrEmpty(customPrompt)) return null;

            try
            {
                // 获取 TalkType 枚举类型
                var talkTypeEnum = talkRequestType.Assembly.GetType("RimTalk.Source.Data.TalkType");
                if (talkTypeEnum == null)
                {
                    talkTypeEnum = talkRequestType.Assembly.GetType("RimTalk.Data.TalkType");
                }
                
                if (talkTypeEnum == null) return null;
                
                // 解析 TalkType.Other
                object otherValue;
                try
                {
                    otherValue = System.Enum.Parse(talkTypeEnum, "Other");
                }
                catch
                {
                    try
                    {
                        otherValue = System.Enum.Parse(talkTypeEnum, "User");
                    }
                    catch
                    {
                        var values = System.Enum.GetValues(talkTypeEnum);
                        if (values.Length == 0) return null;
                        otherValue = values.GetValue(0);
                    }
                }
                
                // 创建 TalkRequest 对象
                object talkRequest = null;
                try
                {
                    talkRequest = System.Activator.CreateInstance(
                        talkRequestType,
                        new object[] { customPrompt, pawn, null, otherValue }
                    );
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk AI Summarizer] Failed to create TalkRequest: {ex.Message}");
                    return null;
                }
                
                if (talkRequest == null) return null;

                // 创建空的消息历史
                var roleType = talkRequestType.Assembly.GetType("RimTalk.Data.Role");
                if (roleType == null) return null;
                
                var tupleType = typeof(System.ValueTuple<,>).MakeGenericType(roleType, typeof(string));
                var messagesType = typeof(System.Collections.Generic.List<>).MakeGenericType(tupleType);
                var messages = System.Activator.CreateInstance(messagesType);
                
                if (messages == null) return null;
                
                // 调用 AIService.Chat(TalkRequest, messages)
                object task = null;
                try
                {
                    task = chatMethod.Invoke(null, new object[] { talkRequest, messages });
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk AI Summarizer] Failed to invoke Chat method: {ex.Message}");
                    return null;
                }
                
                if (task == null) return null;
                
                // 等待 Task 完成
                var taskType = task.GetType();
                var isCompletedProp = taskType.GetProperty("IsCompleted");
                var resultProp = taskType.GetProperty("Result");
                
                if (isCompletedProp == null || resultProp == null) return null;
                
                // 简单的等待（最多10秒）
                int waitCount = 0;
                while (!(bool)isCompletedProp.GetValue(task) && waitCount < 100)
                {
                    System.Threading.Thread.Sleep(100);
                    waitCount++;
                }
                
                if (waitCount >= 100) return null;
                
                // 获取结果
                object responsesList = null;
                try
                {
                    responsesList = resultProp.GetValue(task);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk AI Summarizer] Failed to get task result: {ex.Message}");
                    return null;
                }
                
                if (responsesList == null) return null;
                
                // 从 List<TalkResponse> 中提取文本
                var listType = responsesList.GetType();
                var countProp = listType.GetProperty("Count");
                int count = (int)countProp.GetValue(responsesList);
                
                if (count == 0) return null;
                
                var getItemMethod = listType.GetProperty("Item");
                var firstResponse = getItemMethod.GetValue(responsesList, new object[] { 0 });
                
                if (firstResponse == null) return null;
                
                // 获取 TalkResponse.Text
                var textProp = talkResponseType.GetProperty("Text");
                if (textProp == null) return null;
                
                string summary = (string)textProp.GetValue(firstResponse);
                
                if (string.IsNullOrEmpty(summary)) return null;
                
                return summary;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                Log.Error($"[RimTalk AI Summarizer] API invocation failed: {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk AI Summarizer] Unexpected error: {ex.Message}");
            }

            return null;
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
