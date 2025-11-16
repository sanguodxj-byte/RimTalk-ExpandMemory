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
            Log.Message("[RimTalk Memory] Simple integration initialized");
            Log.Message("[RimTalk Memory] Call RimTalkMemoryAPI.GetMemoryPrompt(pawn, basePrompt) to use memories");
            
            // AI总结器会通过自己的静态构造函数自动初始化
        }
    }

    /// <summary>
    /// Public API for RimTalk to access memory system
    /// </summary>
    public static class RimTalkMemoryAPI
    {
        static RimTalkMemoryAPI()
        {
            Log.Message("[RimTalk Memory API] 🚀 RimTalkMemoryAPI static constructor called - API is LOADED!");
        }
        
        /// <summary>
        /// Get conversation prompt enhanced with pawn's memories
        /// </summary>
        public static string GetMemoryPrompt(Pawn pawn, string basePrompt)
        {
            if (pawn == null) return basePrompt;

            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null)
            {
                Log.Warning($"[RimTalk Memory API] {pawn.LabelShort} has no memory component");
                return basePrompt;
            }

            string memoryContext = memoryComp.GetMemoryContext();
            
            if (string.IsNullOrEmpty(memoryContext))
            {
                return basePrompt;
            }

            Log.Message($"[RimTalk Memory API] Adding memory context for {pawn.LabelShort}");
            return memoryContext + "\n\n" + basePrompt;
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
            // === API入口日志 ===
            Log.Message($"[RimTalk Memory API] 🎯 API CALLED! RecordConversation entry point");
            Log.Message($"[RimTalk Memory API]    speaker: {speaker?.LabelShort ?? "NULL"}");
            Log.Message($"[RimTalk Memory API]    listener: {listener?.LabelShort ?? "NULL"}");
            Log.Message($"[RimTalk Memory API]    content: {(content?.Length > 0 ? content.Substring(0, System.Math.Min(50, content.Length)) : "NULL")}...");
            
            // 直接调用底层方法，由底层统一输出日志
            MemoryAIIntegration.RecordConversation(speaker, listener, content);
            // 不在这里输出日志，避免重复
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
                Log.Message("[RimTalk AI Summarizer] Initializing...");
                
                // 查找 RimTalk 程序集
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");

                if (rimTalkAssembly == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ RimTalk not found - AI summarization DISABLED");
                    return;
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✓ Found RimTalk assembly");

                // 查找 TalkRequest 类型
                talkRequestType = rimTalkAssembly.GetType("RimTalk.Data.TalkRequest");
                if (talkRequestType == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ TalkRequest type not found");
                    return;
                }
                Log.Message("[RimTalk AI Summarizer] ✓ Found TalkRequest type");

                // 查找 AIService 类型
                aiServiceType = rimTalkAssembly.GetType("RimTalk.Service.AIService");
                if (aiServiceType == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ AIService type not found");
                    return;
                }
                Log.Message("[RimTalk AI Summarizer] ✓ Found AIService type");

                // 查找 Chat 方法
                chatMethod = aiServiceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Chat" && 
                                       m.GetParameters().Length == 2 &&
                                       m.GetParameters()[0].ParameterType.Name == "TalkRequest");
                
                if (chatMethod == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ AIService.Chat method not found");
                    return;
                }
                Log.Message("[RimTalk AI Summarizer] ✓ Found AIService.Chat method");

                // 查找 TalkResponse 类型
                talkResponseType = rimTalkAssembly.GetType("RimTalk.Data.TalkResponse");
                if (talkResponseType == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ TalkResponse type not found");
                    return;
                }

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
                                Log.Warning("[RimTalk AI Summarizer] ⚠️ RimTalk is DISABLED in settings");
                                Log.Message("[RimTalk AI Summarizer] AI summarization will be skipped");
                                return;
                            }
                        }
                    }
                }

                isAvailable = true;
                Log.Message("[RimTalk AI Summarizer] ✅ AI summarization ENABLED!");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk AI Summarizer] Initialization failed: {ex.Message}");
                if (Prefs.DevMode)
                    Log.Error($"[RimTalk AI Summarizer] Stack trace: {ex.StackTrace}");
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
            if (!isAvailable)
            {
                if (Prefs.DevMode)
                    Log.Message($"[RimTalk AI Summarizer] AI not available, using simple summary");
                return null;
            }
            
            if (string.IsNullOrEmpty(customPrompt))
            {
                Log.Warning($"[RimTalk AI Summarizer] Empty prompt provided");
                return null;
            }

            try
            {
                Log.Message($"[RimTalk AI Summarizer] 🔄 Calling RimTalk AI for {pawn.LabelShort}...");
                
                // 获取 TalkType 枚举类型 - 注意命名空间是 RimTalk.Source.Data
                var talkTypeEnum = talkRequestType.Assembly.GetType("RimTalk.Source.Data.TalkType");
                if (talkTypeEnum == null)
                {
                    // 尝试旧命名空间
                    talkTypeEnum = talkRequestType.Assembly.GetType("RimTalk.Data.TalkType");
                }
                
                if (talkTypeEnum == null)
                {
                    Log.Error("[RimTalk AI Summarizer] ❌ TalkType enum not found");
                    Log.Error("[RimTalk AI Summarizer] Tried: RimTalk.Source.Data.TalkType and RimTalk.Data.TalkType");
                    
                    // 列出可用的类型（调试）
                    if (Prefs.DevMode)
                    {
                        Log.Message("[RimTalk AI Summarizer] Available types in RimTalk.Source.Data:");
                        foreach (var type in talkRequestType.Assembly.GetTypes().Where(t => t.Namespace == "RimTalk.Source.Data"))
                        {
                            Log.Message($"  - {type.FullName}");
                        }
                    }
                    
                    return null;
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✓ Found TalkType enum: {talkTypeEnum.FullName}");
                
                // 解析 TalkType.Other
                object otherValue;
                try
                {
                    otherValue = System.Enum.Parse(talkTypeEnum, "Other");
                }
                catch
                {
                    // 如果 "Other" 不存在，尝试 "User" 或使用第一个值
                    try
                    {
                        otherValue = System.Enum.Parse(talkTypeEnum, "User");
                    }
                    catch
                    {
                        // 使用枚举的第一个值
                        var values = System.Enum.GetValues(talkTypeEnum);
                        if (values.Length == 0)
                        {
                            Log.Error("[RimTalk AI Summarizer] ❌ TalkType enum has no values");
                            return null;
                        }
                        otherValue = values.GetValue(0);
                    }
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✓ TalkType value: {otherValue}");
                
                // 创建 TalkRequest 对象
                // TalkRequest(string prompt, Pawn initiator, Pawn recipient = null, TalkType talkType = TalkType.Other)
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
                    Log.Error($"[RimTalk AI Summarizer] ❌ Failed to create TalkRequest: {ex.Message}");
                    if (Prefs.DevMode)
                        Log.Error($"  InnerException: {ex.InnerException?.Message}");
                    return null;
                }
                
                if (talkRequest == null)
                {
                    Log.Error("[RimTalk AI Summarizer] ❌ TalkRequest is null");
                    return null;
                }
                
                Log.Message("[RimTalk AI Summarizer] ✓ TalkRequest created");

                // 创建空的消息历史 List<(Role, string)>
                var roleType = talkRequestType.Assembly.GetType("RimTalk.Data.Role");
                if (roleType == null)
                {
                    Log.Error("[RimTalk AI Summarizer] ❌ Role enum not found");
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[RimTalk AI Summarizer] Available types in RimTalk.Data:");
                        foreach (var type in talkRequestType.Assembly.GetTypes().Where(t => t.Namespace == "RimTalk.Data").Take(20))
                        {
                            Log.Message($"  - {type.FullName}");
                        }
                    }
                    
                    return null;
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✓ Found Role enum: {roleType.FullName}");
                
                var tupleType = typeof(System.ValueTuple<,>).MakeGenericType(roleType, typeof(string));
                var messagesType = typeof(System.Collections.Generic.List<>).MakeGenericType(tupleType);
                var messages = System.Activator.CreateInstance(messagesType);
                
                if (messages == null)
                {
                    Log.Error("[RimTalk AI Summarizer] ❌ Failed to create messages list");
                    return null;
                }
                
                Log.Message("[RimTalk AI Summarizer] ✓ Messages list created");
                
                // 调用 AIService.Chat(TalkRequest, messages)
                Log.Message("[RimTalk AI Summarizer] 🌐 Calling AI API (this may take a few seconds)...");
                
                object task = null;
                try
                {
                    task = chatMethod.Invoke(null, new object[] { talkRequest, messages });
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk AI Summarizer] ❌ Failed to invoke Chat method: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Log.Error($"  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }
                    return null;
                }
                
                if (task == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ AI API returned null task");
                    return null;
                }
                
                Log.Message("[RimTalk AI Summarizer] ✓ Task created, waiting for completion...");
                
                // 等待 Task 完成
                var taskType = task.GetType();
                var isCompletedProp = taskType.GetProperty("IsCompleted");
                var resultProp = taskType.GetProperty("Result");
                
                if (isCompletedProp == null || resultProp == null)
                {
                    Log.Error("[RimTalk AI Summarizer] ❌ Task properties not found");
                    return null;
                }
                
                // 简单的等待（最多10秒）
                int waitCount = 0;
                while (!(bool)isCompletedProp.GetValue(task) && waitCount < 100)
                {
                    System.Threading.Thread.Sleep(100);
                    waitCount++;
                }
                
                if (waitCount >= 100)
                {
                    Log.Warning("[RimTalk AI Summarizer] ⏱️ AI request timed out (10 seconds)");
                    return null;
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✓ Task completed in {waitCount * 100}ms");
                
                // 获取结果 List<TalkResponse>
                object responsesList = null;
                try
                {
                    responsesList = resultProp.GetValue(task);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk AI Summarizer] ❌ Failed to get task result: {ex.Message}");
                    if (ex.InnerException != null)
                        Log.Error($"  InnerException: {ex.InnerException.Message}");
                    return null;
                }
                
                if (responsesList == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ AI returned null response");
                    return null;
                }
                
                // 从 List<TalkResponse> 中提取文本
                var listType = responsesList.GetType();
                var countProp = listType.GetProperty("Count");
                int count = (int)countProp.GetValue(responsesList);
                
                if (count == 0)
                {
                    Log.Warning("[RimTalk AI Summarizer] ⚠️ AI returned empty response list");
                    return null;
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✓ Got {count} response(s)");
                
                var getItemMethod = listType.GetProperty("Item");
                var firstResponse = getItemMethod.GetValue(responsesList, new object[] { 0 });
                
                if (firstResponse == null)
                {
                    Log.Warning("[RimTalk AI Summarizer] ❌ First response is null");
                    return null;
                }
                
                // 获取 TalkResponse.Text
                var textProp = talkResponseType.GetProperty("Text");
                if (textProp == null)
                {
                    Log.Error("[RimTalk AI Summarizer] ❌ TalkResponse.Text property not found");
                    return null;
                }
                
                string summary = (string)textProp.GetValue(firstResponse);
                
                if (string.IsNullOrEmpty(summary))
                {
                    Log.Warning("[RimTalk AI Summarizer] ⚠️ AI returned empty summary text");
                    return null;
                }
                
                Log.Message($"[RimTalk AI Summarizer] ✅ AI summary generated: {summary.Substring(0, System.Math.Min(60, summary.Length))}...");
                return summary;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                Log.Error($"[RimTalk AI Summarizer] ❌ API invocation failed:");
                Log.Error($"  Inner Exception: {ex.InnerException?.GetType().FullName}");
                Log.Error($"  Message: {ex.InnerException?.Message}");
                if (Prefs.DevMode && ex.InnerException != null)
                    Log.Error($"  StackTrace: {ex.InnerException.StackTrace}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk AI Summarizer] ❌ Unexpected error:");
                Log.Error($"  Exception: {ex.GetType().FullName}");
                Log.Error($"  Message: {ex.Message}");
                if (Prefs.DevMode)
                    Log.Error($"  StackTrace: {ex.StackTrace}");
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
