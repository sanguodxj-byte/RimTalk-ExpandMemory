using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimTalk.MemoryPatch;
using Verse;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// Patch for PromptService.BuildContext
    /// ⭐ v3.5: 新的 Context 注入方式，更稳定
    /// 
    /// 当 injectToContext = true 时，在 Context 构建阶段就注入记忆和常识
    /// 这比之前通过 AIService.UpdateContext 修改更稳定，因为：
    /// 1. 不依赖已移除的 API
    /// 2. 在 Context 构建阶段就完成注入
    /// 3. 与 RimTalk 的数据流更好地集成
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_BuildContext
    {
        private static bool _isPatched = false;
        
        static Patch_BuildContext()
        {
            try
            {
                var harmony = new Harmony("rimtalk.memory.buildcontext");
                
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
                    Log.Warning("[RimTalk Memory] RimTalk not found, BuildContext patch disabled");
                    return;
                }
                
                // 查找 PromptService.BuildContext 方法
                var promptServiceType = rimTalkAssembly.GetType("RimTalk.Service.PromptService");
                if (promptServiceType == null)
                {
                    Log.Warning("[RimTalk Memory] PromptService type not found");
                    return;
                }
                
                var buildContextMethod = promptServiceType.GetMethod("BuildContext", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (buildContextMethod == null)
                {
                    Log.Warning("[RimTalk Memory] BuildContext method not found");
                    return;
                }
                
                // 应用 Postfix
                var postfixMethod = typeof(Patch_BuildContext).GetMethod(
                    nameof(BuildContext_Postfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(buildContextMethod, postfix: new HarmonyMethod(postfixMethod));
                
                _isPatched = true;
                Log.Message("[RimTalk Memory Patch] ✓ Patched PromptService.BuildContext (Context injection mode)");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory Patch] Failed to patch BuildContext: {ex}");
            }
        }
        
        /// <summary>
        /// Postfix for PromptService.BuildContext
        /// 在 Context 构建完成后追加记忆和常识内容
        /// 
        /// ⭐ 仅当 injectToContext = true 时执行
        /// </summary>
        /// <param name="__result">BuildContext 的返回值（Context 字符串）</param>
        /// <param name="pawns">参与对话的 Pawn 列表</param>
        private static void BuildContext_Postfix(ref string __result, List<Pawn> pawns)
        {
            try
            {
                // 检查设置：仅当 injectToContext = true 时执行
                var settings = RimTalkMemoryPatchMod.Settings;
                if (!settings.injectToContext)
                {
                    return; // 使用 Prompt 注入模式，跳过此 Patch
                }
                
                if (pawns == null || pawns.Count == 0)
                {
                    return;
                }
                
                if (string.IsNullOrEmpty(__result))
                {
                    return;
                }
                
                // 获取主要 Pawn 和目标 Pawn
                Pawn mainPawn = pawns[0];
                Pawn targetPawn = pawns.Count > 1 ? pawns[1] : null;
                
                // 缓存上下文到 API（用于预览器）
                RimTalkMemoryAPI.CacheContext(mainPawn, __result);
                
                // ⭐ 使用智能注入管理器获取注入内容
                string injectedContent = SmartInjectionManager.InjectSmartContext(
                    speaker: mainPawn,
                    listener: targetPawn,
                    context: __result,
                    maxMemories: settings.maxInjectedMemories,
                    maxKnowledge: settings.maxInjectedKnowledge
                );
                
                // ⭐ 主动记忆召回（实验性功能）
                string proactiveRecall = ProactiveMemoryRecall.TryRecallMemory(mainPawn, __result, targetPawn);
                
                // 合并注入内容
                var sb = new StringBuilder();
                
                if (!string.IsNullOrEmpty(injectedContent))
                {
                    sb.Append(injectedContent);
                }
                
                if (!string.IsNullOrEmpty(proactiveRecall))
                {
                    if (sb.Length > 0)
                        sb.Append("\n\n");
                    sb.Append(proactiveRecall);
                }
                
                if (sb.Length == 0)
                {
                    return; // 没有内容需要注入
                }
                
                // ⭐ 追加到 Context
                // 格式：在 Context 末尾添加分隔符和注入内容
                __result = __result + "\n\n---\n\n" + "# Memory & Knowledge Context\n\n" + sb.ToString();
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[Patch_BuildContext] ✓ Injected to Context: {sb.Length} chars for {mainPawn.LabelShort}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Memory Patch] Error in BuildContext_Postfix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查此 Patch 是否已成功应用
        /// </summary>
        public static bool IsPatched => _isPatched;
    }
}