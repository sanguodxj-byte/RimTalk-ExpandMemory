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
    ///
    /// 注入模式：
    /// - injectToContext = true: 由 Patch_BuildContext 在 Context 构建阶段注入
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
                
                // 应用 Postfix
                var postfixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(DecoratePrompt_Postfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(decoratePromptMethod, postfix: new HarmonyMethod(postfixMethod));
                
                Log.Message("[RimTalk Memory Patch] ✓ Patched DecoratePrompt");
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
        /// </summary>
        private static void GenerateTalk_Prefix(object talkRequest)
        {
            try
            {
                if (talkRequest == null)
                    return;
                
                // 通过反射获取 Initiator
                var talkRequestType = talkRequest.GetType();
                var initiatorProperty = talkRequestType.GetProperty("Initiator");
                
                if (initiatorProperty == null)
                    return;
                
                Pawn initiator = initiatorProperty.GetValue(talkRequest) as Pawn;
                if (initiator == null)
                    return;
                
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
