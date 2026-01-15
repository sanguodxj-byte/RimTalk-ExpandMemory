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
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GenerateAndProcessTalkAsync
    {
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
            return method;
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
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(object talkRequest)
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;

                // 通过反射获取 TalkRequest 属性
                var talkRequestType = talkRequest.GetType();
                var promptProperty = talkRequestType.GetProperty("Prompt");
                var contextProperty = talkRequestType.GetProperty("Context");
                
                // 获取 Initiator 和 Recipient
                var initiatorProperty = talkRequestType.GetProperty("Initiator");
                var recipientProperty = talkRequestType.GetProperty("Recipient");
                Pawn initiator = initiatorProperty?.GetValue(talkRequest) as Pawn;
                Pawn recipient = recipientProperty?.GetValue(talkRequest) as Pawn;
                
                if (promptProperty == null)
                {
                    Log.Warning("[RimTalk Memory] Prompt property not found in TalkRequest");
                    return;
                }

                string currentPrompt = promptProperty.GetValue(talkRequest) as string;
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
                // ==========================================
                if (settings.enableVectorEnhancement)
                {
                    try
                    {
                        // 异步向量搜索并同步等待结果
                        var vectorResults = VectorService.Instance.FindBestLoreIdsAsync(
                            cleanedPrompt,  // ⬅️ 使用清理后的 Prompt
                            settings.maxVectorResults,
                            settings.vectorSimilarityThreshold
                        ).Result;

                        if (vectorResults != null && vectorResults.Count > 0)
                        {
                            var memoryManager = Find.World?.GetComponent<MemoryManager>();
                            if (memoryManager != null)
                            {
                                // 获取关键词匹配的条目ID用于去重
                                var keywordMatchedIds = new HashSet<string>();
                                try
                                {
                                    memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
                                        cleanedPrompt,
                                        settings.maxVectorResults,
                                        out var keywordScores,
                                        initiator,
                                        recipient
                                    );
                                    
                                    if (keywordScores != null)
                                    {
                                        foreach (var score in keywordScores)
                                        {
                                            keywordMatchedIds.Add(score.Entry.id);
                                        }
                                    }
                                }
                                catch { }

                                // 构建向量常识文本
                                var entriesSnapshot = memoryManager.CommonKnowledge.Entries.ToList();
                                var scoredResults = new List<(CommonKnowledgeEntry Entry, float Similarity, float Score)>();

                                foreach (var (id, similarity) in vectorResults)
                                {
                                    if (keywordMatchedIds.Contains(id))
                                        continue;
                                    
                                    var entry = entriesSnapshot.FirstOrDefault(e => e.id == id);
                                    if (entry != null)
                                    {
                                        float score = similarity + (entry.importance * 0.2f);
                                        scoredResults.Add((entry, similarity, score));
                                    }
                                }
                                
                                var finalResults = scoredResults.OrderByDescending(x => x.Score).ToList();

                                if (finalResults.Count > 0)
                                {
                                    var vectorSb = new StringBuilder();
                                    vectorSb.AppendLine("## Vector Enhanced Knowledge");
                                    vectorSb.AppendLine();
                                    
                                    int index = 1;
                                    foreach (var item in finalResults)
                                    {
                                        vectorSb.AppendLine($"{index}. [{item.Entry.tag}] {item.Entry.content} (similarity: {item.Similarity:F2})");
                                        index++;
                                    }
                                    
                                    if (allInjections.Length > 0)
                                        allInjections.Append("\n\n");
                                    allInjections.Append(vectorSb.ToString());
                                    
                                    if (Prefs.DevMode)
                                    {
                                        Log.Message($"[RimTalk Memory] ✓ Vector: {finalResults.Count} entries");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk Memory] Vector search error: {ex.Message}");
                    }
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
                
                if (settings.injectToContext && contextProperty != null)
                {
                    // 注入到 Context
                    try
                    {
                        string currentContext = contextProperty.GetValue(talkRequest) as string;
                        string enhancedContext = currentContext + injectionText;
                        contextProperty.SetValue(talkRequest, enhancedContext);
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

                // 回退到注入 Prompt
                if (!injectionSuccess)
                {
                    string enhancedPrompt = currentPrompt + injectionText;
                    promptProperty.SetValue(talkRequest, enhancedPrompt);
                    
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
