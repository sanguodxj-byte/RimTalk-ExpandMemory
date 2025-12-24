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
        /// Prefix: 在异步方法开始前进行向量搜索并注入到 Prompt
        /// 注意：Harmony 不支持 async Prefix，所以我们使用同步方法但在内部启动异步任务
        /// 由于 GenerateAndProcessTalkAsync 本身在 Task.Run 中，所以这里的同步等待不会卡主线程
        /// </summary>
        static void Prefix(object talkRequest, List<Pawn> allInvolvedPawns)
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                if (!settings.enableVectorEnhancement)
                    return;

                // 通过反射获取 TalkRequest.Prompt
                var talkRequestType = talkRequest.GetType();
                var promptProperty = talkRequestType.GetProperty("Prompt");
                
                if (promptProperty == null)
                {
                    Log.Warning("[RimTalk Memory] Prompt property not found in TalkRequest");
                    return;
                }

                string currentPrompt = promptProperty.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(currentPrompt))
                    return;

                Log.Message($"[RimTalk Memory] Starting async vector search for prompt: {currentPrompt.Substring(0, Math.Min(50, currentPrompt.Length))}...");

                // ⭐ 使用 ContextCleaner 清理上下文，去除 RimTalk 格式噪音
                string cleanedContext = ContextCleaner.CleanForVectorMatching(currentPrompt);
                
                if (string.IsNullOrEmpty(cleanedContext))
                {
                    Log.Warning($"[RimTalk Memory] Context cleaned to empty, using original prompt for vector search");
                    cleanedContext = currentPrompt; // 回退到原始 prompt
                }
                else
                {
                    Log.Message($"[RimTalk Memory] Cleaned context: {cleanedContext.Substring(0, Math.Min(100, cleanedContext.Length))}...");
                }

                // 异步向量搜索并同步等待结果
                // 因为这个 Prefix 本身就在 Task.Run 的后台线程中执行，所以 .Result 不会卡主线程
                var vectorResults = VectorService.Instance.FindBestLoreIdsAsync(
                    cleanedContext,  // ⬅️ 使用清理后的上下文
                    settings.maxVectorResults,
                    settings.vectorSimilarityThreshold
                ).Result;

                if (vectorResults == null || vectorResults.Count == 0)
                {
                    Log.Message("[RimTalk Memory] No vector results found");
                    return;
                }

                Log.Message($"[RimTalk Memory] Found {vectorResults.Count} vector knowledge entries");

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
                        allInvolvedPawns?.FirstOrDefault(),
                        allInvolvedPawns?.Skip(1).FirstOrDefault()
                    );
                    
                    if (keywordScores != null)
                    {
                        foreach (var score in keywordScores)
                        {
                            keywordMatchedIds.Add(score.Entry.id);
                        }
                        
                        if (keywordMatchedIds.Count > 0)
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
                sb.AppendLine("## World Knowledge (Vector Enhanced)");

                // ⭐ 线程安全：创建集合快照，避免 "Collection was modified" 异常
                var entriesSnapshot = memoryManager.CommonKnowledge.Entries.ToList();

                // ⭐ 综合排序：结合相似度和重要性
                // Score = Similarity + (Importance * 0.2)
                var scoredResults = new List<(CommonKnowledgeEntry Entry, float Similarity, float Score)>();

                foreach (var (id, similarity) in vectorResults)
                {
                    // ⭐ 去重：跳过已被关键词匹配的条目
                    if (keywordMatchedIds.Contains(id))
                    {
                        Log.Message($"[RimTalk Memory] Skipping vector result '{id}' (already matched by keyword)");
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
                    Log.Message("[RimTalk Memory] No unique vector results after deduplication");
                    return;
                }

                foreach (var item in finalResults)
                {
                    sb.AppendLine($"[{item.Entry.tag}|{item.Similarity:F2}] {item.Entry.content}");
                }

                // 注入到 Prompt
                string enhancedPrompt = currentPrompt + "\n\n" + sb.ToString();
                promptProperty.SetValue(talkRequest, enhancedPrompt);

                Log.Message($"[RimTalk Memory] Successfully injected {finalResults.Count} unique vector knowledge entries into prompt (excluded {keywordMatchedIds.Count} keyword-matched entries)");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in GenerateAndProcessTalkAsync Prefix: {ex}");
            }
        }
    }
}
