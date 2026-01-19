using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimTalk.Memory.API;
using RimTalk.Memory.VectorDB;
using RimTalk.MemoryPatch;
using Verse;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// Patch for TalkService.GenerateAndProcessTalkAsync
    ///
    /// ⭐ v4.1: 向量增强注入入口
    /// - 此 Patch 在 Task.Run() 的后台线程中执行
    /// - 可以安全地执行网络请求（向量搜索）而不阻塞主线程
    /// - 直接修改 PromptMessages（因为 Prompt/Context 在主线程已解析完毕）
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
        /// Prefix: 向量增强注入
        ///
        /// ⭐ v4.1 重构：
        /// - 关键词匹配的记忆和常识已通过 Mustache API（{{memory}}, {{knowledge}}）在主线程注入
        /// - 此 Prefix 仅处理向量增强（需要网络请求，不能在主线程执行）
        /// - 使用 KnowledgeVariableProvider 缓存的上下文信息定位注入位置
        /// - 在 {{knowledge}} 输出位置追加向量结果
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(object talkRequest)
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // 如果向量增强未启用，直接返回
                if (!settings.enableVectorEnhancement)
                {
                    return;
                }

                // 获取主线程缓存的 knowledge 上下文
                var knowledgeContext = KnowledgeVariableProvider.GetLastContext();
                if (knowledgeContext == null)
                {
                    return;
                }

                // 通过反射获取 TalkRequest 属性
                var talkRequestType = talkRequest.GetType();
                var promptMessagesProperty = talkRequestType.GetProperty("PromptMessages");
                var contextProperty = talkRequestType.GetProperty("Context");
                
                // 向量搜索固定使用 dialogue.type
                string vectorSearchText = knowledgeContext.DialogueType;
                string matchText = knowledgeContext.MatchText;
                Pawn initiator = knowledgeContext.Speaker;
                Pawn recipient = knowledgeContext.Listener;
                string keywordKnowledge = knowledgeContext.KeywordKnowledge;

                if (string.IsNullOrEmpty(vectorSearchText))
                {
                    return;
                }

                // ==========================================
                // 向量增强搜索（使用 dialogue.type 内容）
                // ==========================================
                string vectorContent = null;
                try
                {
                    // 在后台线程中执行向量搜索（安全的 .Result 调用）
                    var vectorResults = VectorService.Instance.FindBestLoreIdsAsync(
                        vectorSearchText,
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
                                    matchText,
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
                                vectorSb.AppendLine();
                                vectorSb.AppendLine("#### Vector Enhanced:");
                                
                                int index = 1;
                                foreach (var item in finalResults)
                                {
                                    vectorSb.AppendLine($"{index}. [{item.Entry.tag}] {item.Entry.content}");
                                    index++;
                                }
                                
                                vectorContent = vectorSb.ToString();
                                
                                // ⭐ 输出匹配结果日志（包含余弦相似度）
                                var similarities = string.Join(", ", finalResults.Select(r => $"{r.Entry.tag}:{r.Similarity:F3}"));
                                Log.Message($"[RimTalk Memory] Vector matched {finalResults.Count} knowledge entries. Similarities: {similarities}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk Memory] Vector search error: {ex.Message}");
                }

                // ==========================================
                // ⭐ 注入向量结果
                // 优先注入到 Context (system_instruction)，回退到 PromptMessages
                // ==========================================
                if (string.IsNullOrEmpty(vectorContent))
                {
                    return;
                }

                const string NO_MATCH_MARKER = "(No matching knowledge found)";
                bool injected = false;
                
                // 策略 1: 尝试注入到 Context（用于某些 API 配置）
                if (contextProperty != null && !injected)
                {
                    try
                    {
                        string context = contextProperty.GetValue(talkRequest) as string;
                        
                        if (!string.IsNullOrEmpty(context) && context.Contains(NO_MATCH_MARKER))
                        {
                            string newContext = context.Replace(NO_MATCH_MARKER, NO_MATCH_MARKER + vectorContent);
                            contextProperty.SetValue(talkRequest, newContext);
                            injected = true;
                        }
                        else if (!string.IsNullOrEmpty(context) && !string.IsNullOrEmpty(keywordKnowledge) && context.Contains(keywordKnowledge))
                        {
                            int idx = context.IndexOf(keywordKnowledge) + keywordKnowledge.Length;
                            string newContext = context.Insert(idx, vectorContent);
                            contextProperty.SetValue(talkRequest, newContext);
                            injected = true;
                        }
                    }
                    catch { }
                }

                // 策略 2: 注入到 PromptMessages
                if (promptMessagesProperty != null && !injected)
                {
                    try
                    {
                        var promptMessages = promptMessagesProperty.GetValue(talkRequest);
                        if (promptMessages != null)
                        {
                            var messagesList = promptMessages as System.Collections.IList;
                            if (messagesList != null && messagesList.Count > 0)
                            {
                                // 查找 RimTalk 的 Role 枚举
                                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                                var roleType = rimTalkAssembly?.GetType("RimTalk.Source.Data.Role");
                                
                                // 回退到其他可能的路径
                                if (roleType == null)
                                {
                                    roleType = rimTalkAssembly?.GetType("RimTalk.Data.Role");
                                }
                                
                                if (roleType != null)
                                {
                                    var systemRole = Enum.Parse(roleType, "System");
                                    
                                    int targetIndex = -1;
                                    string targetContent = null;
                                    object targetRole = null;
                                    bool foundKeywordMatch = false;
                                    bool foundNoMatchMarker = false;
                                    
                                    // 用于定位的文本片段
                                    string searchText = !string.IsNullOrEmpty(keywordKnowledge) && keywordKnowledge.Length > 10
                                        ? keywordKnowledge.Substring(0, Math.Min(50, keywordKnowledge.Length))
                                        : null;
                                    
                                    for (int i = 0; i < messagesList.Count; i++)
                                    {
                                        var msg = messagesList[i];
                                        var msgType = msg.GetType();
                                        var contentField = msgType.GetField("Item2") ?? msgType.GetField("content");
                                        var roleField = msgType.GetField("Item1") ?? msgType.GetField("role");
                                        
                                        if (contentField != null)
                                        {
                                            string content = contentField.GetValue(msg) as string ?? "";
                                            
                                            // 查找关键词匹配内容
                                            if (!string.IsNullOrEmpty(searchText) && content.Contains(searchText))
                                            {
                                                targetIndex = i;
                                                targetContent = content;
                                                targetRole = roleField?.GetValue(msg);
                                                foundKeywordMatch = true;
                                                break;
                                            }
                                            
                                            // 查找 "(No matching knowledge found)"
                                            if (content.Contains(NO_MATCH_MARKER))
                                            {
                                                targetIndex = i;
                                                targetContent = content;
                                                targetRole = roleField?.GetValue(msg);
                                                foundNoMatchMarker = true;
                                            }
                                        }
                                    }
                                    
                                    // 执行注入
                                    if (foundKeywordMatch && targetIndex >= 0 && targetContent != null)
                                    {
                                        int knowledgeEndIndex = targetContent.IndexOf(keywordKnowledge) + keywordKnowledge.Length;
                                        if (knowledgeEndIndex > 0 && knowledgeEndIndex <= targetContent.Length)
                                        {
                                            string newContent = targetContent.Insert(knowledgeEndIndex, vectorContent);
                                            var tupleType = typeof(ValueTuple<,>).MakeGenericType(roleType, typeof(string));
                                            var newMsg = Activator.CreateInstance(tupleType, targetRole ?? systemRole, newContent);
                                            messagesList[targetIndex] = newMsg;
                                            injected = true;
                                        }
                                    }
                                    else if (foundNoMatchMarker && targetIndex >= 0 && targetContent != null)
                                    {
                                        string newContent = targetContent.Replace(NO_MATCH_MARKER, NO_MATCH_MARKER + vectorContent);
                                        var tupleType = typeof(ValueTuple<,>).MakeGenericType(roleType, typeof(string));
                                        var newMsg = Activator.CreateInstance(tupleType, targetRole ?? systemRole, newContent);
                                        messagesList[targetIndex] = newMsg;
                                        injected = true;
                                    }
                                    else
                                    {
                                        // 回退到最后一条消息
                                        int lastIndex = messagesList.Count - 1;
                                        if (lastIndex >= 0)
                                        {
                                            var lastMsg = messagesList[lastIndex];
                                            var msgType = lastMsg.GetType();
                                            var contentField = msgType.GetField("Item2") ?? msgType.GetField("content");
                                            var roleField = msgType.GetField("Item1") ?? msgType.GetField("role");
                                            
                                            if (contentField != null)
                                            {
                                                string currentContent = contentField.GetValue(lastMsg) as string ?? "";
                                                object currentRole = roleField?.GetValue(lastMsg) ?? systemRole;
                                                string newContent = currentContent + "\n" + vectorContent;
                                                
                                                var tupleType = typeof(ValueTuple<,>).MakeGenericType(roleType, typeof(string));
                                                var newMsg = Activator.CreateInstance(tupleType, currentRole, newContent);
                                                messagesList[lastIndex] = newMsg;
                                                injected = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                // 清除缓存的上下文（已使用）
                KnowledgeVariableProvider.ClearContext();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in GenerateAndProcessTalkAsync Prefix: {ex}");
            }
        }
    }
}
