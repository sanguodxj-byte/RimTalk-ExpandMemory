using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimTalk.Memory.VectorDB;
using RimTalk.MemoryPatch;
using Verse;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// Patch for TalkService.GenerateAndProcessTalkAsync
    ///
    /// ⭐ v3.5.1: 统一注入入口
    /// - 当 injectToContext = true 时，在这里处理所有注入（关键词匹配 + 向量增强）
    /// - 使用 Prompt 进行匹配，注入到 Context
    /// - 这解决了之前 Patch_BuildContext 无法访问 Prompt 的问题
    /// 
    /// ⚠️ v3.4.10: 性能优化 - 缓存反射属性，避免高频反射查找
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GenerateAndProcessTalkAsync
    {
        // ⚠️ v3.4.10: 性能优化 - 静态缓存 PropertyInfo，避免每次对话都反射查找
        private static Type cachedTalkRequestType = null;
        private static PropertyInfo cachedPromptProperty = null;
        private static PropertyInfo cachedContextProperty = null;
        private static PropertyInfo cachedInitiatorProperty = null;
        private static PropertyInfo cachedRecipientProperty = null;
        private static bool reflectionInitialized = false;
        private static readonly object reflectionLock = new object();
        
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // 查找 RimTalk.Service.TalkService.GenerateAndProcessTalkAsync
            var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "RimTalk");
            
            if (rimTalkAssembly == null)
            {
                Log.Warning("[RimTalk Memory] RimTalk assembly not found for GenerateAndProcessTalkAsync patch");
                return null;
            }

            var talkServiceType = rimTalkAssembly.GetType("RimTalk.Service.TalkService");
            if (talkServiceType == null)
            {
                Log.Warning("[RimTalk Memory] TalkService type not found");
                return null;
            }

            var method = talkServiceType.GetMethod("GenerateAndProcessTalkAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (method == null)
            {
                Log.Warning("[RimTalk Memory] GenerateAndProcessTalkAsync method not found");
                return null;
            }

            Log.Message("[RimTalk Memory] ✓ Found GenerateAndProcessTalkAsync for patching");
            
            // ⚠️ v3.4.10: 在找到目标方法时，预初始化反射缓存
            InitializeReflectionCache(rimTalkAssembly);
            
            return method;
        }
        
        /// <summary>
        /// ⚠️ v3.4.10: 预初始化反射缓存（线程安全）
        /// </summary>
        private static void InitializeReflectionCache(Assembly rimTalkAssembly)
        {
            if (reflectionInitialized) return;
            
            lock (reflectionLock)
            {
                if (reflectionInitialized) return; // Double-check
                
                try
                {
                    cachedTalkRequestType = rimTalkAssembly.GetType("RimTalk.Data.TalkRequest");
                    
                    if (cachedTalkRequestType != null)
                    {
                        cachedPromptProperty = cachedTalkRequestType.GetProperty("Prompt");
                        cachedContextProperty = cachedTalkRequestType.GetProperty("Context");
                        cachedInitiatorProperty = cachedTalkRequestType.GetProperty("Initiator");
                        cachedRecipientProperty = cachedTalkRequestType.GetProperty("Recipient");
                        
                        reflectionInitialized = true;
                        Log.Message("[RimTalk Memory] ✓ Reflection cache initialized successfully");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk Memory] Failed to initialize reflection cache: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Prefix: 统一处理所有记忆和常识注入
        ///
        /// ⭐ v3.5.1: 当 injectToContext = true 时：
        /// 1. 使用 Prompt 进行关键词匹配（SmartInjectionManager）
        /// 2. 使用 Prompt 进行向量增强（如果启用）
        /// 3. 将结果注入到 Context
        ///
        /// 这解决了 Patch_BuildContext 用 Context 匹配常识导致的问题
        /// 
        /// ⚠️ v3.4.10: 性能优化 - 使用缓存的 PropertyInfo，避免高频反射
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(object talkRequest)
        {
            try
            {
                // ⚠️ v3.4.10: 快速路径 - 如果反射未初始化，延迟初始化（懒加载）
                if (!reflectionInitialized)
                {
                    // 尝试从 talkRequest 的类型推断并初始化
                    if (talkRequest != null)
                    {
                        var requestType = talkRequest.GetType();
                        var assembly = requestType.Assembly;
                        InitializeReflectionCache(assembly);
                    }
                    
                    // 如果仍然失败，直接返回
                    if (!reflectionInitialized)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Warning("[RimTalk Memory] Reflection not initialized, skipping injection");
                        }
                        return;
                    }
                }
                
                var settings = RimTalkMemoryPatchMod.Settings;

                // ⚠️ v3.4.10: 使用缓存的 PropertyInfo（性能提升 10-100 倍）
                if (cachedPromptProperty == null || cachedContextProperty == null)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[RimTalk Memory] Cached properties are null, skipping injection");
                    }
                    return;
                }
                
                // 获取 Initiator 和 Recipient（使用缓存）
                Pawn initiator = cachedInitiatorProperty?.GetValue(talkRequest) as Pawn;
                Pawn recipient = cachedRecipientProperty?.GetValue(talkRequest) as Pawn;

                string currentPrompt = cachedPromptProperty.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(currentPrompt))
                    return;

                // ⭐ v3.5.1: 缓存 Prompt 用于预览器
                RimTalkMemoryAPI.CacheContext(initiator, currentPrompt);

                // ⭐ 使用 ContextCleaner 清理上下文，去除 RimTalk 格式噪音
                string cleanedPrompt = ContextCleaner.CleanForVectorMatching(currentPrompt);
                
                if (string.IsNullOrEmpty(cleanedPrompt))
                {
                    cleanedPrompt = currentPrompt; // 回退到原始 prompt
                }

                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] Processing prompt: {cleanedPrompt.Substring(0, Math.Min(50, cleanedPrompt.Length))}...");
                }

                var allInjections = new StringBuilder();

                // ==========================================
                // ⭐ 第一部分：关键词匹配的记忆和常识
                // ⭐ v3.5.1: 当 injectToContext = true 时，在这里处理
                // ==========================================
                if (settings.injectToContext)
                {
                    // 使用 SmartInjectionManager 进行关键词匹配
                    string smartInjection = SmartInjectionManager.InjectSmartContext(
                        speaker: initiator,
                        listener: recipient,
                        context: cleanedPrompt,  // ⬅️ 使用清理后的 Prompt 进行匹配
                        maxMemories: settings.maxInjectedMemories,
                        maxKnowledge: settings.maxInjectedKnowledge
                    );
                    
                    if (!string.IsNullOrEmpty(smartInjection))
                    {
                        allInjections.Append(smartInjection);
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[RimTalk Memory] ✓ SmartInjection: {smartInjection.Length} chars");
                        }
                    }
                    
                    // 主动记忆召回
                    string proactiveRecall = ProactiveMemoryRecall.TryRecallMemory(initiator, cleanedPrompt, recipient);
                    if (!string.IsNullOrEmpty(proactiveRecall))
                    {
                        if (allInjections.Length > 0)
                            allInjections.Append("\n\n");
                        allInjections.Append(proactiveRecall);
                    }
                }

                // ==========================================
                // ⭐ 第二部分：向量增强（如果启用）
                // ⚠️ v3.4.9: 移除同步阻塞 - 向量增强仅用于后台同步，不在实时对话中使用
                // ==========================================
                if (settings.enableVectorEnhancement)
                {
                    // ⚠️ 架构警告：向量增强需要网络请求（2-5秒），在 Harmony Patch Prefix 中同步等待会导致游戏卡死！
                    // 
                    // 原代码问题：
                    // var vectorResults = VectorService.Instance.FindBestLoreIdsAsync(...).Result; // ❌ 死锁炸弹！
                    //
                    // 正确做法：
                    // 1. 向量增强应该在后台预加载（通过 SyncKnowledgeLibrary）
                    // 2. 实时对话只使用关键词匹配（无网络请求，<1ms）
                    // 3. 预览器中可以手动测试向量匹配
                    //
                    // 因此，我们在这里只记录警告日志，不执行向量搜索。
                    
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[RimTalk Memory] ⚠️ Vector enhancement is enabled but DISABLED in real-time dialogue to prevent freezing. " +
                                   "Vector search requires 2-5s network request which would block game thread. " +
                                   "Use '测试向量匹配' button in preview window for manual testing.");
                    }
                    
                    // 向量增强已禁用，不再执行异步网络请求
                }

                // ==========================================
                // ⭐ 第三部分：执行注入
                // ==========================================
                if (allInjections.Length == 0)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message("[RimTalk Memory] No content to inject");
                    }
                    return;
                }

                string injectionText = "\n\n---\n\n# Memory & Knowledge Context\n\n" + allInjections.ToString();
                bool injectionSuccess = false;
                
                if (settings.injectToContext && cachedContextProperty != null)
                {
                    // 注入到 Context（使用缓存的 PropertyInfo）
                    try
                    {
                        string currentContext = cachedContextProperty.GetValue(talkRequest) as string;
                        string enhancedContext = currentContext + injectionText;
                        cachedContextProperty.SetValue(talkRequest, enhancedContext);
                        injectionSuccess = true;
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[RimTalk Memory] ✓ Injected to Context: {allInjections.Length} chars");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk Memory] Failed to inject to Context: {ex.Message}");
                    }
                }

                // 回退到注入 Prompt（使用缓存的 PropertyInfo）
                if (!injectionSuccess)
                {
                    string enhancedPrompt = currentPrompt + injectionText;
                    cachedPromptProperty.SetValue(talkRequest, enhancedPrompt);
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[RimTalk Memory] ✓ Injected to Prompt: {allInjections.Length} chars");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in GenerateAndProcessTalkAsync Prefix: {ex}");
            }
        }
    }
}
