using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// 针对 RimTalk 的精确补丁 - 基于实际代码结构
    /// ⭐ v3.0: 集成高级评分系统（不修改RimTalk代码）
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkPrecisePatcher
    {
        private const string VERSION = "v3.0.SMART"; // <-- 版本标记

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
                bool patchedBuildContext = PatchBuildContext(harmony, rimTalkAssembly);
                bool patchedDecoratePrompt = PatchDecoratePrompt(harmony, rimTalkAssembly);
                bool patchedGenerateTalk = PatchGenerateTalk(harmony, rimTalkAssembly);
                
                int successCount = (patchedBuildContext ? 1 : 0) + 
                                  (patchedDecoratePrompt ? 1 : 0) + 
                                  (patchedGenerateTalk ? 1 : 0);
                
                if (successCount > 0)
                {
                    Log.Message($"[RimTalk Memory Patch v{VERSION}] Successfully patched {successCount}/3 methods");
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
        /// 补丁 PromptService.BuildContext
        /// </summary>
        private static bool PatchBuildContext(Harmony harmony, Assembly assembly)
        {
            try
            {
                // RimTalk.Service.PromptService
                var promptServiceType = assembly.GetType("RimTalk.Service.PromptService");
                if (promptServiceType == null) return false;
                
                // BuildContext 方法
                var buildContextMethod = promptServiceType.GetMethod("BuildContext", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (buildContextMethod == null) return false;
                
                // 应用 Postfix
                var postfixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(BuildContext_Postfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(buildContextMethod, postfix: new HarmonyMethod(postfixMethod));
                
                Log.Message("[RimTalk Memory Patch] ✓ Patched BuildContext");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Failed to patch BuildContext: {ex.Message}");
                return false;
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
        /// Postfix for BuildContext - 在构建上下文后添加记忆
        /// ⭐ v3.0: 使用智能注入管理器（高级评分系统）
        /// </summary>
        private static void BuildContext_Postfix(ref string __result, List<Pawn> pawns)
        {
            try
            {
                if (pawns == null || pawns.Count == 0 || string.IsNullOrEmpty(__result))
                    return;
                
                Pawn mainPawn = pawns[0];
                Pawn targetPawn = pawns.Count > 1 ? pawns[1] : null;
                
                // 缓存上下文到API（用于预览器）
                RimTalkMemoryAPI.CacheContext(mainPawn, __result);
                
                // ⭐ 使用智能注入管理器（新系统）
                string injectedContext = SmartInjectionManager.InjectSmartContext(
                    speaker: mainPawn,
                    listener: targetPawn,
                    context: __result,
                    maxMemories: RimTalkMemoryPatchMod.Settings.maxInjectedMemories,
                    maxKnowledge: RimTalkMemoryPatchMod.Settings.maxInjectedKnowledge
                );
                
                // ⭐ v3.0: 主动记忆召回（实验性功能）
                string proactiveRecall = ProactiveMemoryRecall.TryRecallMemory(mainPawn, __result, targetPawn);
                
                // 合并注入内容
                if (!string.IsNullOrEmpty(injectedContext))
                {
                    __result = __result + "\n\n" + injectedContext;
                }
                
                // 如果触发主动召回，追加到末尾
                if (!string.IsNullOrEmpty(proactiveRecall))
                {
                    __result = __result + "\n\n" + proactiveRecall;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Error in BuildContext_Postfix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Postfix for DecoratePrompt - 在装饰提示词后添加记忆
        /// ⭐ v3.0: 使用智能注入管理器（高级评分系统）
        /// </summary>
        private static void DecoratePrompt_Postfix(object talkRequest, List<Pawn> pawns)
        {
            try
            {
                if (talkRequest == null || pawns == null || pawns.Count == 0)
                    return;
                
                // 通过反射获取 TalkRequest.Prompt 属性
                var talkRequestType = talkRequest.GetType();
                var promptProperty = talkRequestType.GetProperty("Prompt");
                
                if (promptProperty == null)
                    return;
                
                string currentPrompt = promptProperty.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(currentPrompt))
                    return;
                
                // 获取主要 Pawn 和目标 Pawn
                Pawn mainPawn = pawns[0];
                Pawn targetPawn = pawns.Count > 1 ? pawns[1] : null;
                
                // 缓存上下文到API（用于预览器）
                RimTalkMemoryAPI.CacheContext(mainPawn, currentPrompt);
                
                // ⭐ 使用智能注入管理器（新系统）
                string injectedContext = SmartInjectionManager.InjectSmartContext(
                    speaker: mainPawn,
                    listener: targetPawn,
                    context: currentPrompt,
                    maxMemories: RimTalkMemoryPatchMod.Settings.maxInjectedMemories,
                    maxKnowledge: RimTalkMemoryPatchMod.Settings.maxInjectedKnowledge
                );
                
                // ⭐ v3.0: 主动记忆召回（实验性功能）
                string proactiveRecall = ProactiveMemoryRecall.TryRecallMemory(mainPawn, currentPrompt, targetPawn);
                
                // 合并注入内容
                string enhancedPrompt = currentPrompt;
                
                if (!string.IsNullOrEmpty(injectedContext))
                {
                    enhancedPrompt += "\n\n" + injectedContext;
                }
                
                if (!string.IsNullOrEmpty(proactiveRecall))
                {
                    enhancedPrompt += "\n\n" + proactiveRecall;
                }
                
                // 更新提示词
                if (enhancedPrompt != currentPrompt)
                {
                    promptProperty.SetValue(talkRequest, enhancedPrompt);
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
