using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Verse;
using RimTalk.Memory;
using RimTalk.Memory.AIDatabase;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// AI响应后处理补丁
    /// v3.3.1
    /// 
    /// 拦截AI响应，处理其中的数据库查询命令
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AIResponsePostProcessor
    {
        static AIResponsePostProcessor()
        {
            try
            {
                var harmony = new Harmony("rimtalk.memory.airesponse");
                
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
                    Log.Warning("[AI Response Processor] RimTalk not found");
                    return;
                }
                
                // 尝试补丁响应处理方法
                bool patched = PatchResponseHandler(harmony, rimTalkAssembly);
                
                if (patched)
                {
                    Log.Message("[AI Response Processor] Successfully patched response handler");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AI Response Processor] Init failed: {ex}");
            }
        }
        
        /// <summary>
        /// 补丁响应处理器
        /// </summary>
        private static bool PatchResponseHandler(Harmony harmony, Assembly assembly)
        {
            try
            {
                // 查找AIService或TalkService
                var aiServiceType = assembly.GetType("RimTalk.Service.AIService");
                if (aiServiceType == null)
                {
                    Log.Warning("[AI Response Processor] AIService not found");
                    return false;
                }
                
                // 查找Chat方法（返回响应的方法）
                var chatMethods = aiServiceType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                
                foreach (var method in chatMethods)
                {
                    if (method.Name == "Chat" && 
                        method.ReturnType.Name.Contains("Task"))
                    {
                        // 应用Postfix到异步Chat方法
                        var postfixMethod = typeof(AIResponsePostProcessor).GetMethod(
                            nameof(ChatAsync_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        
                        if (postfixMethod != null)
                        {
                            harmony.Patch(method, postfix: new HarmonyMethod(postfixMethod));
                            Log.Message($"[AI Response Processor] Patched {method.Name}");
                            return true;
                        }
                    }
                }
                
                Log.Warning("[AI Response Processor] Chat method not found");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[AI Response Processor] Patch failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Postfix for AIService.Chat (异步方法)
        /// ? v3.3.2: 优化为非阻塞处理，避免卡顿
        /// ? v3.3.2: 修复参数名变化（talkRequest → request）
        /// </summary>
        private static void ChatAsync_Postfix(object request, ref object __result)
        {
            try
            {
                if (__result == null || request == null)
                    return;
                
                // 获取Task类型
                var taskType = __result.GetType();
                if (!taskType.Name.Contains("Task"))
                    return;
                
                // 获取Initiator (speaker)
                var requestType = request.GetType();
                var initiatorProp = requestType.GetProperty("Initiator");
                if (initiatorProp == null)
                    return;
                
                Pawn speaker = initiatorProp.GetValue(request) as Pawn;
                if (speaker == null)
                    return;
                
                // ? 优化：注册异步处理，不阻塞主线程
                var task = __result as System.Threading.Tasks.Task;
                if (task != null)
                {
                    // 使用ConfigureAwait(false)避免阻塞UI
                    task.ContinueWith(t =>
                    {
                        try
                        {
                            // 使用反射获取Task.Result
                            var resultProperty = t.GetType().GetProperty("Result");
                            if (resultProperty != null)
                            {
                                var taskResult = resultProperty.GetValue(t);
                                if (taskResult != null)
                                {
                                    // ? 在后台线程处理，不阻塞UI
                                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                                    {
                                        ProcessResponse(taskResult, speaker);
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 静默失败，不影响主流程
                            if (Prefs.DevMode)
                            {
                                Log.Warning($"[AI Response Processor] Background processing failed: {ex.Message}");
                            }
                        }
                    }, System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            catch (Exception ex)
            {
                // 静默失败，不影响主流程
                if (Prefs.DevMode)
                {
                    Log.Warning($"[AI Response Processor] Postfix error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 处理AI响应
        /// ? v3.3.2: 优化token消耗
        /// </summary>
        private static void ProcessResponse(object result, Pawn speaker)
        {
            try
            {
                // 使用反射获取response字段
                var resultType = result.GetType();
                var responseField = resultType.GetField("response", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (responseField == null)
                    return;
                
                string originalResponse = responseField.GetValue(result) as string;
                
                if (string.IsNullOrEmpty(originalResponse))
                    return;
                
                // ? 处理数据库命令，获取处理后的结果
                var processed = AIDatabase.AIDatabaseCommands.ProcessDatabaseCommands(originalResponse, speaker);
                
                // 更新response为用户可见文本（命令已隐藏）
                responseField.SetValue(result, processed.UserVisibleText);
                
                // ? 存储内部上下文，供下次对话使用
                if (!string.IsNullOrEmpty(processed.InternalContext))
                {
                    StoreInternalContext(speaker, processed.InternalContext);
                }
            }
            catch (Exception ex)
            {
                // 静默失败
                if (Prefs.DevMode)
                {
                    Log.Warning($"[AI Response Processor] Failed to process: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 存储内部上下文（查询结果）
        /// 下次对话时自动注入
        /// </summary>
        private static void StoreInternalContext(Pawn speaker, string context)
        {
            try
            {
                var memoryComp = speaker?.TryGetComp<FourLayerMemoryComp>();
                if (memoryComp == null)
                    return;
                
                // ? 存储为临时记忆（ABM层级，自动过期）
                var contextMemory = new MemoryEntry(
                    $"[查询上下文] {context.Substring(0, Math.Min(50, context.Length))}...",
                    MemoryType.Internal,
                    MemoryLayer.Active,
                    importance: 0.9f
                );
                
                // 设置短期过期（ABM会自动清理旧内容）
                // 无需手动设置过期时间
                
                // 添加到ABM
                if (memoryComp.ActiveMemories.Count >= 6)
                {
                    // ABM满了，移除最旧的非重要记忆
                    var toRemove = memoryComp.ActiveMemories
                        .Where(m => m.type == MemoryType.Internal)
                        .OrderBy(m => m.timestamp)
                        .FirstOrDefault();
                    
                    if (toRemove != null)
                    {
                        memoryComp.ActiveMemories.Remove(toRemove);
                      }
                }
                
                memoryComp.ActiveMemories.Add(contextMemory);
                
                if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                {
                    Log.Message($"[AI Response Processor] Stored context for next conversation");
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[AI Response Processor] Failed to store context: {ex.Message}");
                }
            }
        }
    }
}
