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
    /// 在异步方法中进行向量搜索，不会卡主线程
    /// ⭐ v3.4.2: 更新以匹配 RimTalk 新版本的方法签名
    /// ⭐ v3.4.3: 添加更强的错误处理和安全检查
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GenerateAndProcessTalkAsync
    {
        static MethodBase TargetMethod()
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
        /// Prefix: 在异步方法开始前进行向量搜索并注入到 context
        /// ⭐ v3.4.2: 更新参数以匹配新版本 RimTalk（只有一个 TalkRequest 参数）
        /// ⭐ v3.4.3: 添加更强的错误处理和安全检查
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(object talkRequest)
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                if (!settings.enableVectorEnhancement)
                    return;

                if (talkRequest == null)
                {
                    Log.Warning("[RimTalk Memory] talkRequest is null, skipping vector enhancement");
                    return;
                }

                // 通过反射获取 TalkRequest 的属性
                var talkRequestType = talkRequest.GetType();
                
                // ⭐ 获取 Prompt（用户消息）
                var promptProperty = talkRequestType.GetProperty("Prompt");
                if (promptProperty == null)
                {
                    Log.Warning("[RimTalk Memory] Prompt property not found in TalkRequest");
                    return;
                }

                string currentPrompt = promptProperty.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(currentPrompt))
                    return;

                // ⭐ 获取 Initiator 和 Recipient
                var initiatorProperty = talkRequestType.GetProperty("Initiator");
                var recipientProperty = talkRequestType.GetProperty("Recipient");
                
                Verse.Pawn initiator = initiatorProperty?.GetValue(talkRequest) as Verse.Pawn;
                Verse.Pawn recipient = recipientProperty?.GetValue(talkRequest) as Verse.Pawn;
                
                // 构建参与者列表
                var allInvolvedPawns = new List<Verse.Pawn>();
                if (initiator != null) allInvolvedPawns.Add(initiator);
                if (recipient != null && recipient != initiator) allInvolvedPawns.Add(recipient);

                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] Starting async vector search for prompt: {currentPrompt.Substring(0, Math.Min(50, currentPrompt.Length))}...");
                }

                // ⭐ 使用 ContextCleaner 清理上下文，去除 RimTalk 格式噪音
                string cleanedContext = ContextCleaner.CleanForVectorMatching(currentPrompt);
                
                if (string.IsNullOrEmpty(cleanedContext))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[RimTalk Memory] Context cleaned to empty, using original prompt for vector search");
                    }
                    cleanedContext = currentPrompt; // 回退到原始 prompt
                }
                else if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] Cleaned context: {cleanedContext.Substring(0, Math.Min(100, cleanedContext.Length))}...");
                }

                // 异步向量搜索并同步等待结果
                // ⭐ v3.4.3: 添加超时保护
                var vectorTask = VectorService.Instance.FindBestLoreIdsAsync(
                    cleanedContext,
                    settings.maxVectorResults,
                    settings.vectorSimilarityThreshold
                );
                
                // ⚠️ 等待最多 5 秒
                if (!vectorTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    Log.Warning("[RimTalk Memory] Vector search timed out after 5 seconds");
                    return;
                }
                
                var vectorResults = vectorTask.Result;

                if (vectorResults == null || vectorResults.Count == 0)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message("[RimTalk Memory] No vector results found");
                    }
                    return;
                }

                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] Found {vectorResults.Count} vector knowledge entries");
                }

                // 构建向量常识文本
                var memoryManager = Find.World?.GetComponent<MemoryManager>();
                if (memoryManager == null)
                {
                    Log.Warning("[RimTalk Memory] MemoryManager not found");
                    return;
                }

                // ⭐ 去重逻辑：先获取关键词匹配的条目ID
                var keywordMatchedIds = new HashSet<string>();
                try
                {
                    // 调用关键词匹配获取已匹配的条目
                    memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
                        cleanedContext,
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
                        
                        if (keywordMatchedIds.Count > 0 && Prefs.DevMode)
                        {
                            Log.Message($"[RimTalk Memory] Found {keywordMatchedIds.Count} keyword-matched entries, will exclude from vector results");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk Memory] Failed to get keyword matches for deduplication: {ex.Message}");
                }

                var sb = new StringBuilder();
                sb.AppendLine("\n\n---\n\n");
                sb.AppendLine("# Vector Enhanced Knowledge");
                sb.AppendLine();

                // ⭐ 线程安全：创建集合快照
                var entriesSnapshot = memoryManager.CommonKnowledge.Entries?.ToList();
                if (entriesSnapshot == null || entriesSnapshot.Count == 0)
                {
                    Log.Warning("[RimTalk Memory] Common knowledge entries is null or empty");
                    return;
                }

                // ⭐ 综合排序：结合相似度和重要性
                var scoredResults = new List<(CommonKnowledgeEntry Entry, float Similarity, float Score)>();

                foreach (var (id, similarity) in vectorResults)
                {
                    // ⭐ 去重：跳过已被关键词匹配的条目
                    if (keywordMatchedIds.Contains(id))
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[RimTalk Memory] Skipping vector result '{id}' (already matched by keyword)");
                        }
                        continue;
                    }
                    
                    var entry = entriesSnapshot.FirstOrDefault(e => e.id == id);
                    if (entry != null)
                    {
                        float score = similarity + (entry.importance * 0.2f);
                        scoredResults.Add((entry, similarity, score));
                    }
                }
                
                // 按综合得分排序
                var finalResults = scoredResults.OrderByDescending(x => x.Score).ToList();

                if (finalResults.Count == 0)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message("[RimTalk Memory] No unique vector results after deduplication");
                    }
                    return;
                }

                int index = 1;
                foreach (var item in finalResults)
                {
                    sb.AppendLine($"{index}. [{item.Entry.tag}] {item.Entry.content} (similarity: {item.Similarity:F2})");
                    index++;
                }

                // ⭐ v3.4.2: 根据设置选择注入位置
                var vectorInjection = sb.ToString();
                
                if (settings.injectToContext)
                {
                    // 注入到 Context (TalkRequest.Context 属性)
                    InjectVectorToTalkRequestContext(talkRequest, talkRequestType, vectorInjection);
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[RimTalk Memory] Successfully injected {finalResults.Count} unique vector knowledge entries to TalkRequest.Context (excluded {keywordMatchedIds.Count} keyword-matched entries)");
                    }
                }
                else
                {
                    // 注入到 Prompt (User Message) - 默认行为
                    string enhancedPrompt = currentPrompt + "\n\n" + vectorInjection;
                    promptProperty.SetValue(talkRequest, enhancedPrompt);
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[RimTalk Memory] Successfully injected {finalResults.Count} unique vector knowledge entries to Prompt (excluded {keywordMatchedIds.Count} keyword-matched entries)");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in GenerateAndProcessTalkAsync Prefix: {ex}");
            }
        }
        
        /// <summary>
        /// ⭐ v3.4.2: 新方法 - 将向量搜索结果注入到 TalkRequest.Context
        /// </summary>
        private static void InjectVectorToTalkRequestContext(object talkRequest, Type talkRequestType, string injectionContent)
        {
            try
            {
                var contextProperty = talkRequestType.GetProperty("Context");
                if (contextProperty == null)
                {
                    Log.Warning("[RimTalk Memory] Context property not found in TalkRequest");
                    return;
                }
                
                string currentContext = contextProperty.GetValue(talkRequest) as string ?? "";
                
                // 追加注入内容到现有 Context
                string enhancedContext = currentContext + "\n\n" + injectionContent;
                
                // 设置新的 Context
                contextProperty.SetValue(talkRequest, enhancedContext);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] Successfully injected vector results to TalkRequest.Context");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Failed to inject vector results to TalkRequest.Context: {ex.Message}");
            }
        }
    }
}
