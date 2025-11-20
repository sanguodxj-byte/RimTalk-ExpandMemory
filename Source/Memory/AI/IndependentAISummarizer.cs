using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace RimTalk.Memory.AI
{
    public static class IndependentAISummarizer
    {
        private static bool isInitialized = false;
        private static string apiKey, apiUrl, model, provider;
        private static readonly Dictionary<string, string> completedSummaries = new Dictionary<string, string>();
        private static readonly HashSet<string> pendingSummaries = new HashSet<string>();
        private static readonly Dictionary<string, List<Action<string>>> callbackMap = new Dictionary<string, List<Action<string>>>();
        private static readonly Queue<Action> mainThreadActions = new Queue<Action>();

        public static string ComputeCacheKey(Pawn pawn, List<MemoryEntry> memories)
        {
            var ids = memories.Select(m => m.id ?? m.content.GetHashCode().ToString()).ToArray();
            string joinedIds = string.Join("|", ids);
            return $"{pawn.ThingID}_{memories.Count}_{joinedIds.GetHashCode()}";
        }

        public static void RegisterCallback(string cacheKey, Action<string> callback)
        {
            lock (callbackMap)
            {
                if (!callbackMap.TryGetValue(cacheKey, out var callbacks))
                {
                    callbacks = new List<Action<string>>();
                    callbackMap[cacheKey] = callbacks;
                }
                callbacks.Add(callback);
            }
        }

        public static void ProcessPendingCallbacks(int maxPerTick = 5)
        {
            int processed = 0;
            lock (mainThreadActions)
            {
                while (mainThreadActions.Count > 0 && processed < maxPerTick)
                {
                    try
                    {
                        mainThreadActions.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AI Summarizer] Callback error: {ex.Message}");
                    }
                    processed++;
                }
            }
        }

        public static void Initialize()
        {
            if (isInitialized) return;
            try
            {
                var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
                
                // 优先使用独立配置
                if (!settings.useRimTalkAIConfig || !TryLoadFromRimTalk())
                {
                    apiKey = settings.independentApiKey;
                    apiUrl = settings.independentApiUrl;
                    model = settings.independentModel;
                    provider = settings.independentProvider;
                    
                    // 如果 URL 为空，根据提供商设置默认值
                    if (string.IsNullOrEmpty(apiUrl))
                    {
                        if (provider == "OpenAI")
                        {
                            apiUrl = "https://api.openai.com/v1/chat/completions";
                        }
                        else if (provider == "DeepSeek")
                        {
                            apiUrl = "https://api.deepseek.com/v1/chat/completions";
                        }
                        else if (provider == "Google")
                        {
                            apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/MODEL_PLACEHOLDER:generateContent?key=API_KEY_PLACEHOLDER";
                        }
                    }
                    
                    // 验证配置
                    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl))
                    {
                        Log.Warning("[AI] Configuration incomplete, using rule-based summary");
                        return;
                    }
                    
                    Log.Message($"[AI] Initialized ({provider}/{model})");
                    isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AI] Init failed: {ex.Message}");
                isInitialized = false;
            }
        }

        /// <summary>
        /// 尝试从 RimTalk 加载配置（兼容模式）
        /// </summary>
        private static bool TryLoadFromRimTalk()
        {
            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "RimTalk");
                if (assembly == null) return false;
                
                Type type = assembly.GetType("RimTalk.Settings");
                if (type == null) return false;
                
                MethodInfo method = type.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
                if (method == null) return false;
                
                object obj = method.Invoke(null, null);
                if (obj == null) return false;
                
                Type type2 = obj.GetType();
                MethodInfo method2 = type2.GetMethod("GetActiveConfig");
                if (method2 == null) return false;
                
                object obj2 = method2.Invoke(obj, null);
                if (obj2 == null) return false;
                
                Type type3 = obj2.GetType();
                
                FieldInfo field = type3.GetField("ApiKey");
                if (field != null)
                {
                    apiKey = (field.GetValue(obj2) as string);
                }
                
                FieldInfo field2 = type3.GetField("BaseUrl");
                if (field2 != null)
                {
                    apiUrl = (field2.GetValue(obj2) as string);
                }
                
                if (string.IsNullOrEmpty(apiUrl))
                {
                    FieldInfo field3 = type3.GetField("Provider");
                    if (field3 != null)
                    {
                        object value = field3.GetValue(obj2);
                        provider = value.ToString();
                        
                        if (provider == "OpenAI")
                        {
                            apiUrl = "https://api.openai.com/v1/chat/completions";
                        }
                        else if (provider == "DeepSeek")
                        {
                            apiUrl = "https://api.deepseek.com/v1/chat/completions";
                        }
                        else if (provider == "Google")
                        {
                            apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/MODEL_PLACEHOLDER:generateContent?key=API_KEY_PLACEHOLDER";
                        }
                    }
                }
                
                FieldInfo field4 = type3.GetField("SelectedModel");
                if (field4 != null)
                {
                    model = (field4.GetValue(obj2) as string);
                }
                else
                {
                    FieldInfo field5 = type3.GetField("CustomModelName");
                    if (field5 != null)
                    {
                        model = (field5.GetValue(obj2) as string);
                    }
                }
                
                if (string.IsNullOrEmpty(model))
                {
                    model = "gpt-3.5-turbo";
                }
                
                if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiUrl))
                {
                    Log.Message($"[AI] Loaded from RimTalk ({provider}/{model})");
                    isInitialized = true;
                    return true;
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsAvailable()
        {
            if (!isInitialized) Initialize();
            return isInitialized;
        }

        public static string SummarizeMemories(Pawn pawn, List<MemoryEntry> memories, string promptTemplate)
        {
            if (!IsAvailable()) return null;

            string cacheKey = ComputeCacheKey(pawn, memories);

            lock (completedSummaries)
            {
                if (completedSummaries.TryGetValue(cacheKey, out string summary))
                {
                    return summary; // Return cached result directly if available
                }
            }

            lock (pendingSummaries)
            {
                if (pendingSummaries.Contains(cacheKey)) return null; // Already processing
                pendingSummaries.Add(cacheKey);
            }

            string prompt = BuildPrompt(pawn, memories, promptTemplate);

            Task.Run(async () =>
            {
                try
                {
                    string result = await CallAIAsync(prompt);
                    if (result != null)
                    {
                        lock (completedSummaries)
                        {
                            completedSummaries[cacheKey] = result;
                        }
                        lock (callbackMap)
                        {
                            if (callbackMap.TryGetValue(cacheKey, out var callbacks))
                            {
                                foreach (var cb in callbacks)
                                {
                                    lock (mainThreadActions)
                                    {
                                        mainThreadActions.Enqueue(() => cb(result));
                                    }
                                }
                                callbackMap.Remove(cacheKey);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AI Summarizer] Task failed: {ex.Message}");
                }
                finally
                {
                    lock (pendingSummaries)
                    {
                        pendingSummaries.Remove(cacheKey);
                    }
                }
            });

            return null; // Indicates that the process is async
        }

        private static string BuildPrompt(Pawn pawn, List<MemoryEntry> memories, string template)
        {
            var sb = new StringBuilder();
            
            // 根据模板类型生成不同的提示词
            if (template == "deep_archive")
            {
                // 深度归档：更加精炼，关注核心特征和里程碑事件
                sb.AppendLine($"请为殖民者 {pawn.LabelShort} 进行深度记忆归档。");
                sb.AppendLine("\n以下是已总结的中期记忆（ELS）：");
                int i = 1;
                foreach (var m in memories.Take(15))
                {
                    sb.AppendLine($"{i}. {m.content}");
                    i++;
                }
                sb.AppendLine("\n归档要求：");
                sb.AppendLine("1. 提炼核心人设特征和性格特点");
                sb.AppendLine("2. 总结重要里程碑事件和转折点");
                sb.AppendLine("3. 合并相似经历，突出长期趋势");
                sb.AppendLine("4. 极简表达，不超过60字");
                sb.AppendLine("5. 只输出归档总结，不要JSON或其他格式");
                sb.AppendLine("\n示例：擅长建造和研究，是殖民地技术核心。在第2年成功击退机械族大规模袭击。与医生建立深厚友谊。");
            }
            else // daily_summary 或其他
            {
                // 每日总结：常规总结，保留更多细节
                sb.AppendLine($"请为殖民者 {pawn.LabelShort} 总结以下记忆。");
                sb.AppendLine("\n记忆列表：");
                int i = 1;
                foreach (var m in memories.Take(20))
                {
                    sb.AppendLine($"{i}. {m.content}");
                    i++;
                }
                sb.AppendLine("\n要求：");
                sb.AppendLine("1. 提炼地点、人物、事件");
                sb.AppendLine("2. 相似事件合并，标注频率（×N）");
                sb.AppendLine("3. 极简表达，不超过80字");
                sb.AppendLine("4. 只输出总结文字，不要JSON或其他格式");
            }
            
            return sb.ToString();
        }

        private static string BuildJsonRequest(string prompt)
        {
            string str = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t");
			StringBuilder stringBuilder = new StringBuilder();
			bool flag = provider == "Google";
			if (flag)
			{
				stringBuilder.Append("{");
				stringBuilder.Append("\"contents\":[{");
				stringBuilder.Append("\"parts\":[{");
				stringBuilder.Append("\"text\":\"" + str + "\"");
				stringBuilder.Append("}]");
				stringBuilder.Append("}],");
				stringBuilder.Append("\"generationConfig\":{");
				stringBuilder.Append("\"temperature\":0.7,");
				stringBuilder.Append("\"maxOutputTokens\":200");
				bool flag2 = model.Contains("flash");
				if (flag2)
				{
					stringBuilder.Append(",\"thinkingConfig\":{\"thinkingBudget\":0}");
				}
				stringBuilder.Append("}");
				stringBuilder.Append("}");
			}
			else
			{
				stringBuilder.Append("{");
				stringBuilder.Append("\"model\":\"" + model + "\",");
				stringBuilder.Append("\"messages\":[");
				stringBuilder.Append("{\"role\":\"user\",");
				stringBuilder.Append("\"content\":\"" + str + "\"");
				stringBuilder.Append("}],");
				stringBuilder.Append("\"temperature\":0.7,");
				stringBuilder.Append("\"max_tokens\":200");
				stringBuilder.Append("}");
			}
			return stringBuilder.ToString();
        }

        private static async Task<string> CallAIAsync(string prompt)
        {
            string actualUrl = apiUrl;
            if (provider == "Google")
            {
                // Replace placeholders with actual values
                actualUrl = apiUrl.Replace("MODEL_PLACEHOLDER", model).Replace("API_KEY_PLACEHOLDER", apiKey);
            }

            Log.Message($"[AI Summarizer] Calling API: {actualUrl.Substring(0, Math.Min(60, actualUrl.Length))}...");

            var request = (HttpWebRequest)WebRequest.Create(actualUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            if (provider != "Google")
            {
                request.Headers["Authorization"] = $"Bearer {apiKey}";
            }
            request.Timeout = 30000;

            string json = BuildJsonRequest(prompt);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.ContentLength = bodyRaw.Length;

            using (var stream = await request.GetRequestStreamAsync())
            {
                await stream.WriteAsync(bodyRaw, 0, bodyRaw.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var streamReader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    string responseText = await streamReader.ReadToEndAsync();
                    return ParseResponse(responseText);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var errorResponse = (HttpWebResponse)ex.Response)
                    using (var streamReader = new System.IO.StreamReader(errorResponse.GetResponseStream()))
                    {
                        string errorText = streamReader.ReadToEnd();
                        Log.Error($"[AI Summarizer] API Error: {errorResponse.StatusCode} - {errorText.Substring(0, Math.Min(200, errorText.Length))}");
                    }
                }
                else
                {
                    Log.Error($"[AI Summarizer] Network Error: {ex.Message}");
                }
                return null;
            }
        }

        private static string ParseResponse(string responseText)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(responseText, provider == "Google" ? @"""text""\s*:\s*""(.*?)""" : @"""content""\s*:\s*""(.*?)""");
                if (match.Success) return System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value);
            }
            catch (Exception ex)
            {
                Log.Error($"[AI Summarizer] ❌ Parse error: {ex.Message}");
            }
            return null;
        }
    }
}
