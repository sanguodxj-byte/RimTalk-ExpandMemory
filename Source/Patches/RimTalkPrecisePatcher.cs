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
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkPrecisePatcher
    {
        private const string VERSION = "v7.FINAL"; // <-- 新增版本标记

        static RimTalkPrecisePatcher()
        {
            try
            {
                Log.Message($"[RimTalk Patcher {VERSION}] Starting..."); // <-- 修改日志
                
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
                    Log.Message($"[RimTalk Patcher {VERSION}] RimTalk not installed - running in standalone mode"); // <-- 修改日志
                    return;
                }
                
                Log.Message($"[RimTalk Patcher {VERSION}] Found RimTalk assembly: {rimTalkAssembly.FullName}"); // <-- 修改日志
                
                // 目标 1: PromptService.BuildContext
                bool patchedBuildContext = PatchBuildContext(harmony, rimTalkAssembly);
                
                // 目标 2: PromptService.DecoratePrompt  
                bool patchedDecoratePrompt = PatchDecoratePrompt(harmony, rimTalkAssembly);
                
                // 目标 3: TalkService.GenerateTalk (作为备用)
                bool patchedGenerateTalk = PatchGenerateTalk(harmony, rimTalkAssembly);
                
                int successCount = (patchedBuildContext ? 1 : 0) + 
                                  (patchedDecoratePrompt ? 1 : 0) + 
                                  (patchedGenerateTalk ? 1 : 0);
                
                if (successCount > 0)
                {
                    Log.Message($"[RimTalk Patcher {VERSION}] ✓ Successfully patched {successCount} method(s)!"); // <-- 修改日志
                    Log.Message($"[RimTalk Patcher {VERSION}] Memory context will be injected into AI conversations"); // <-- 修改日志
                }
                else
                {
                    Log.Error($"[RimTalk Patcher {VERSION}] ❌ CRITICAL: Failed to patch any methods!"); // <-- 修改日志
                    Log.Error($"[RimTalk Patcher {VERSION}] This is a critical error - memory integration will not work"); // <-- 修改日志
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Patcher {VERSION}] ❌ EXCEPTION: {ex}"); // <-- 修改日志
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
                if (promptServiceType == null)
                {
                    Log.Warning("[RimTalk Precise Patcher] PromptService type not found");
                    return false;
                }
                
                // BuildContext 方法
                var buildContextMethod = promptServiceType.GetMethod("BuildContext", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (buildContextMethod == null)
                {
                    Log.Warning("[RimTalk Precise Patcher] BuildContext method not found");
                    return false;
                }
                
                // 应用 Postfix
                var postfixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(BuildContext_Postfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(buildContextMethod, postfix: new HarmonyMethod(postfixMethod));
                
                Log.Message("[RimTalk Precise Patcher] ✓ Patched PromptService.BuildContext");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Precise Patcher] Failed to patch BuildContext: {ex.Message}");
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
                    Log.Warning("[RimTalk Precise Patcher] DecoratePrompt method not found");
                    return false;
                }
                
                // 应用 Postfix
                var postfixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(DecoratePrompt_Postfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(decoratePromptMethod, postfix: new HarmonyMethod(postfixMethod));
                
                Log.Message("[RimTalk Precise Patcher] ✓ Patched PromptService.DecoratePrompt");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Precise Patcher] Failed to patch DecoratePrompt: {ex.Message}");
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
                
                if (generateTalkMethod == null)
                {
                    Log.Warning("[RimTalk Precise Patcher] GenerateTalk method not found");
                    return false;
                }
                
                // 应用 Prefix (在方法执行前)
                var prefixMethod = typeof(RimTalkPrecisePatcher).GetMethod(
                    nameof(GenerateTalk_Prefix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(generateTalkMethod, prefix: new HarmonyMethod(prefixMethod));
                
                Log.Message("[RimTalk Precise Patcher] ✓ Patched TalkService.GenerateTalk");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Precise Patcher] Failed to patch GenerateTalk: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Postfix for BuildContext - 在构建上下文后添加记忆
        /// </summary>
        private static void BuildContext_Postfix(ref string __result, List<Pawn> pawns)
        {
            try
            {
                if (pawns == null || pawns.Count == 0 || string.IsNullOrEmpty(__result))
                    return;
                
                // 为主要的 Pawn（第一个）添加记忆上下文
                Pawn mainPawn = pawns[0];
                var memoryComp = mainPawn?.TryGetComp<FourLayerMemoryComp>();
                
                if (memoryComp == null)
                    return;
                
                string memoryContext = "";
                
                // 使用动态注入或静态注入
                if (RimTalkMemoryPatchMod.Settings.useDynamicInjection)
                {
                    // 动态注入：根据上下文智能选择记忆
                    memoryContext = DynamicMemoryInjection.InjectMemories(
                        mainPawn, 
                        __result, 
                        RimTalkMemoryPatchMod.Settings.maxInjectedMemories
                    );
                    
                    // 注入常识库
                    var memoryManager = Find.World?.GetComponent<MemoryManager>();
                    if (memoryManager != null)
                    {
                        string knowledgeContext = memoryManager.CommonKnowledge.InjectKnowledge(
                            __result,
                            RimTalkMemoryPatchMod.Settings.maxInjectedKnowledge
                        );
                        
                        if (!string.IsNullOrEmpty(knowledgeContext))
                        {
                            memoryContext = memoryContext + "\n\n" + knowledgeContext;
                        }
                    }
                }
                else
                {
                    // 静态注入：使用旧的GetMemoryContext方法
                    var pawnMemoryComp = mainPawn.TryGetComp<PawnMemoryComp>();
                    if (pawnMemoryComp != null)
                    {
                        memoryContext = pawnMemoryComp.GetMemoryContext();
                    }
                }
                
                if (!string.IsNullOrEmpty(memoryContext))
                {
                    // 将记忆上下文插入到原始上下文中
                    __result = __result + "\n\n" + memoryContext;
                    
                    if (Prefs.DevMode)
                        Log.Message($"[RimTalk Patcher {VERSION}] ✓ Injected memory for {mainPawn.LabelShort} into BuildContext");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Precise Patcher] Error in BuildContext_Postfix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Postfix for DecoratePrompt - 在装饰提示词后添加记忆
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
                
                // 获取主要 Pawn 的记忆
                Pawn mainPawn = pawns[0];
                var memoryComp = mainPawn?.TryGetComp<FourLayerMemoryComp>();
                
                if (memoryComp == null)
                    return;
                
                string memoryContext = "";
                
                // 使用动态注入或静态注入
                if (RimTalkMemoryPatchMod.Settings.useDynamicInjection)
                {
                    // 动态注入
                    memoryContext = DynamicMemoryInjection.InjectMemories(
                        mainPawn, 
                        currentPrompt, 
                        RimTalkMemoryPatchMod.Settings.maxInjectedMemories
                    );
                    
                    // 注入常识库
                    var memoryManager = Find.World?.GetComponent<MemoryManager>();
                    if (memoryManager != null)
                    {
                        string knowledgeContext = memoryManager.CommonKnowledge.InjectKnowledge(
                            currentPrompt,
                            RimTalkMemoryPatchMod.Settings.maxInjectedKnowledge
                        );
                        
                        if (!string.IsNullOrEmpty(knowledgeContext))
                        {
                            memoryContext = memoryContext + "\n\n" + knowledgeContext;
                        }
                    }
                }
                else
                {
                    // 静态注入
                    var pawnMemoryComp = mainPawn.TryGetComp<PawnMemoryComp>();
                    if (pawnMemoryComp != null)
                    {
                        memoryContext = pawnMemoryComp.GetMemoryContext();
                    }
                }
                
                if (!string.IsNullOrEmpty(memoryContext))
                {
                    // 更新提示词
                    string enhancedPrompt = currentPrompt + "\n\n" + memoryContext;
                    promptProperty.SetValue(talkRequest, enhancedPrompt);
                    
                    if (Prefs.DevMode)
                        Log.Message($"[RimTalk Patcher {VERSION}] ✓ Injected memory for {mainPawn.LabelShort} into DecoratePrompt");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Precise Patcher] Error in DecoratePrompt_Postfix: {ex.Message}");
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
                    Log.Message($"[RimTalk Patcher {VERSION}] Preparing memory context for {initiator.LabelShort}"); // <-- 修改日志
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Precise Patcher] Error in GenerateTalk_Prefix: {ex.Message}");
            }
        }
    }
}
