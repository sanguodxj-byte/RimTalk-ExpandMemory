using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// 针对 RimTalk 的精确补丁 - 基于实际代码结构
    /// ⭐ v3.0: 集成高级评分系统（不修改RimTalk代码）
    /// ⭐ v3.5: 适配 RimTalk API 变更，移除已废弃的 AIService.GetContext/UpdateContext
    /// ⭐ v3.5.1: 注入逻辑重构
    ///
    /// 注入模式：
    /// - injectToContext = true: 由 Patch_GenerateAndProcessTalkAsync 处理所有注入
    ///   （使用 Prompt 匹配，注入到 Context）
    /// - injectToContext = false: 由此 Patch 在 DecoratePrompt 阶段注入到 Prompt
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkPrecisePatcher
    {
        private const string VERSION = "v3.5.0"; // <-- 版本标记：适配 RimTalk API 变更

        static RimTalkPrecisePatcher()
        {
            try
            {
                var harmony = new Harmony("rimtalk.memory.precise");
                
                // 查找 RimTalk 程序集
                Assembly rimTalkAssembly = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "RimTalk")
                    {
                        rimTalkAssembly = assembly;
                        break;
                    }
                }
                
                if (rimTalkAssembly == null)
                {
                    Log.Warning("[RimTalk Memory] RimTalk not found, memory injection disabled");
                    return;
                }
                
                // 应用补丁（Harmony不修改原代码，只是拦截调用）
                // bool patchedBuildContext = PatchBuildContext(harmony, rimTalkAssembly); // 已移除：避免重复注入
                bool patchedDecoratePrompt = PatchDecoratePrompt(harmony, rimTalkAssembly);
                bool patchedGenerateTalk = PatchGenerateTalk(harmony, rimTalkAssembly);
                
                int successCount = (patchedDecoratePrompt ? 1 : 0) + 
                                  (patchedGenerateTalk ? 1 : 0);
                
                if (successCount > 0)
                {
                    Log.Message($"[RimTalk Memory Patch v{VERSION}] Successfully patched {successCount}/2 methods");
                }
                else
                {
                    Log.Error($"[RimTalk Memory Patch] Failed to patch any methods");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory Patch] Exception: {ex}");
            }
        }
        
        /// <summary>
        /// 补丁 PromptService.DecoratePrompt
        /// ⭐ v3.5.1: 添加 Prefix 缓存 Prompt，供 BuildContext 使用
        /// </summary>
        private static bool PatchDecoratePrompt(Harmony harmony, Assembly assembly)
        {
            try
            {
                var promptServiceType = assembly.GetType("RimTalk.Service.PromptService");
                if (promptServiceType == null) return false;
                
                // DecoratePrompt 方法
                var decoratePromptMethod = promptServiceType.GetMethod("DecoratePrompt",
                    BindingFlags.Public | BindingFlags.Static);
                
                if (decoratePromptMethod == null)
                {
                    Log.Warning("[RimTalk Memory Patch] DecoratePrompt method not found");
                    return false;
                }
                
                // ⭐ v3.5.1: 应用 Prefix（用于缓存 Prompt，供 BuildContext 使用）
                var prefixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(DecoratePrompt_Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                // 应用 Postfix
                var postfixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(DecoratePrompt_Postfix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(decoratePromptMethod,
                    prefix: new HarmonyMethod(prefixMethod),
                    postfix: new HarmonyMethod(postfixMethod));
                
                Log.Message("[RimTalk Memory Patch] ✓ Patched DecoratePrompt (with Prefix for Prompt caching)");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Failed to patch DecoratePrompt: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 补丁 TalkService.GenerateTalk (备用方案)
        /// </summary>
        private static bool PatchGenerateTalk(Harmony harmony, Assembly assembly)
        {
            try
            {
                var talkServiceType = assembly.GetType("RimTalk.Service.TalkService");
                if (talkServiceType == null) return false;
                
                // GenerateTalk 方法
                var generateTalkMethod = talkServiceType.GetMethod("GenerateTalk", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (generateTalkMethod == null) return false;
                
                // 应用 Prefix (在方法执行前)
                var prefixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(GenerateTalk_Prefix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(generateTalkMethod, prefix: new HarmonyMethod(prefixMethod));
                
                Log.Message("[RimTalk Memory Patch] ✓ Patched GenerateTalk");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Failed to patch GenerateTalk: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// ⭐ v3.5.1: Prefix for DecoratePrompt - 缓存 Prompt 用于 BuildContext 匹配
        ///
        /// 时序说明：
        /// 1. DecoratePrompt_Prefix（这里）- 缓存 Prompt
        /// 2. DecoratePrompt 原方法
        /// 3. DecoratePrompt_Postfix
        /// 4. BuildContext（使用缓存的 Prompt 进行匹配）
        /// 5. GenerateAndProcessTalkAsync
        /// </summary>
        private static void DecoratePrompt_Prefix(object talkRequest, List<Pawn> pawns)
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // 只在 Context 注入模式下缓存 Prompt
                if (!settings.injectToContext)
                    return;
                
                if (talkRequest == null || pawns == null || pawns.Count == 0)
                    return;
                
                // 通过反射获取 TalkRequest 的 Prompt 属性
                var talkRequestType = talkRequest.GetType();
                var promptProperty = talkRequestType.GetProperty("Prompt");
                
                if (promptProperty == null)
                    return;
                
                string prompt = promptProperty.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(prompt))
                    return;
                
                // 获取 Speaker 和 Listener
                Pawn speaker = pawns[0];
                Pawn listener = pawns.Count > 1 ? pawns[1] : null;
                
                // ⭐ 缓存 Prompt，供 Patch_BuildContext 使用
                RimTalkMemoryAPI.CachePromptForMatching(speaker, listener, prompt);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[DecoratePrompt_Prefix] Cached Prompt for BuildContext: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Error in DecoratePrompt_Prefix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Postfix for DecoratePrompt - 注入记忆和常识到 Prompt（用户消息）
        /// ⭐ v3.0: 使用智能注入管理器（高级评分系统）
        /// ⭐ v3.5: 当 injectToContext = true 时跳过，由 Patch_BuildContext 处理
        /// </summary>
        private static void DecoratePrompt_Postfix(object talkRequest, List<Pawn> pawns)
        {
            try
            {
                // ⭐ v3.5: 检查注入模式
                // 如果启用了 Context 注入，则跳过此 Patch（由 Patch_BuildContext 处理）
                var settings = RimTalkMemoryPatchMod.Settings;
                if (settings.injectToContext)
                {
                    // Context 注入模式：由 Patch_BuildContext 在 BuildContext 阶段处理
                    // 这里仅缓存上下文用于预览器
                    if (talkRequest != null && pawns != null && pawns.Count > 0)
                    {
                        var talkRequestType = talkRequest.GetType();
                        var promptProperty = talkRequestType.GetProperty("Prompt");
                        if (promptProperty != null)
                        {
                            string currentPrompt = promptProperty.GetValue(talkRequest) as string;
                            if (!string.IsNullOrEmpty(currentPrompt))
                            {
                                RimTalkMemoryAPI.CacheContext(pawns[0], currentPrompt);
                            }
                        }
                    }
                    return;
                }
                
                // === Prompt 注入模式（默认） ===
                
                if (talkRequest == null || pawns == null || pawns.Count == 0)
                    return;
                
                // 通过反射获取 TalkRequest 的属性
                var talkReqType = talkRequest.GetType();
                var promptProp = talkReqType.GetProperty("Prompt");  // 用户消息
                
                if (promptProp == null)
                    return;
                
                string currentPromptValue = promptProp.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(currentPromptValue))
                    return;
                
                // 获取主要 Pawn 和目标 Pawn
                Pawn mainPawn = pawns[0];
                Pawn targetPawn = pawns.Count > 1 ? pawns[1] : null;
                
                // 缓存上下文到API（用于预览器）
                RimTalkMemoryAPI.CacheContext(mainPawn, currentPromptValue);
                
                // ⭐ 使用智能注入管理器（新系统）
                string injectedContext = SmartInjectionManager.InjectSmartContext(
                    speaker: mainPawn,
                    listener: targetPawn,
                    context: currentPromptValue,
                    maxMemories: settings.maxInjectedMemories,
                    maxKnowledge: settings.maxInjectedKnowledge
                );
                
                // ⭐ 主动记忆召回（实验性功能）
                string proactiveRecall = ProactiveMemoryRecall.TryRecallMemory(mainPawn, currentPromptValue, targetPawn);
                
                // 合并注入内容
                var sb = new StringBuilder();
                
                if (!string.IsNullOrEmpty(injectedContext))
                {
                    sb.Append(injectedContext);
                }
                
                if (!string.IsNullOrEmpty(proactiveRecall))
                {
                    if (sb.Length > 0)
                        sb.Append("\n\n");
                    sb.Append(proactiveRecall);
                }
                
                if (sb.Length == 0)
                    return;
                
                // 注入到 Prompt (User Message)
                string enhancedPrompt = currentPromptValue + "\n\n---\n\n# Memory & Knowledge Context\n\n" + sb.ToString();
                promptProp.SetValue(talkRequest, enhancedPrompt);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[DecoratePrompt_Postfix] ✓ Injected to Prompt: {sb.Length} chars for {mainPawn.LabelShort}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Error in DecoratePrompt_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix for GenerateTalk - 在生成对话前准备记忆上下文
        /// ⭐ v3.5.1: 在这里缓存 Prompt，因为 GenerateTalk 是整个对话流程的起点
        /// 时序：GenerateTalk → DecoratePrompt → BuildContext → GenerateAndProcessTalkAsync
        /// </summary>
        private static void GenerateTalk_Prefix(object talkRequest)
        {
            try
            {
                if (talkRequest == null)
                    return;
                
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // 通过反射获取 TalkRequest 属性
                var talkRequestType = talkRequest.GetType();
                var initiatorProperty = talkRequestType.GetProperty("Initiator");
                var recipientProperty = talkRequestType.GetProperty("Recipient");
                var promptProperty = talkRequestType.GetProperty("Prompt");
                
                if (initiatorProperty == null)
                    return;
                
                Pawn initiator = initiatorProperty.GetValue(talkRequest) as Pawn;
                if (initiator == null)
                    return;
                
                Pawn recipient = recipientProperty?.GetValue(talkRequest) as Pawn;
                
                // ⭐ v3.5.1: 在 GenerateTalk 阶段就缓存 Prompt
                // 这确保在 BuildContext 执行时缓存已经就绪
                if (settings.injectToContext && promptProperty != null)
                {
                    string prompt = promptProperty.GetValue(talkRequest) as string;
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        RimTalkMemoryAPI.CachePromptForMatching(initiator, recipient, prompt);
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[GenerateTalk_Prefix] ⭐ Cached Prompt for BuildContext: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");
                        }
                    }
                }
                
                var memoryComp = initiator.TryGetComp<PawnMemoryComp>();
                if (memoryComp != null)
                {
                    // 备用方案，目前不需要额外处理
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Error in GenerateTalk_Prefix: {ex.Message}");
            }
        }
    }
}
