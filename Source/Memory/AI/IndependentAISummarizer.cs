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
                Log.Message("[AI Summarizer] Initializing...");
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RimTalk");
                if (rimTalkAssembly == null) { Log.Warning("[AI Summarizer] RimTalk assembly not found"); return; }
                var settingsType = rimTalkAssembly.GetType("RimTalk.Settings");
                if (settingsType == null) return;
                var getMethod = settingsType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod == null) return;
                var settings = getMethod.Invoke(null, null);
                if (settings == null) return;
                var getActiveConfigMethod = settings.GetType().GetMethod("GetActiveConfig");
                if (getActiveConfigMethod == null) return;
                var config = getActiveConfigMethod.Invoke(settings, null);
                if (config == null) return;
                var configType = config.GetType();
                apiKey = configType.GetField("ApiKey")?.GetValue(config) as string;
                provider = configType.GetField("Provider")?.GetValue(config)?.ToString() ?? "";
                apiUrl = configType.GetField("BaseUrl")?.GetValue(config) as string;
                if (string.IsNullOrEmpty(apiUrl))
                {
                    if (provider == "OpenAI")
                    {
                        apiUrl = "https://api.openai.com/v1/chat/completions";
                    }
                    else if (provider == "Google")
                    {
                        // For Google, we'll build the URL in CallAIAsync using the model name
                        apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/MODEL_PLACEHOLDER:generateContent?key=API_KEY_PLACEHOLDER";
                    }
                }
                model = configType.GetField("SelectedModel")?.GetValue(config) as string;
                if (string.IsNullOrEmpty(model)) model = configType.GetField("CustomModelName")?.GetValue(config) as string;
                if (string.IsNullOrEmpty(model)) model = "gpt-3.5-turbo";
                if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiUrl))
                {
                    Log.Message($"[AI Summarizer] ✅ Initialized (Provider: {provider}, Model: {model})");
                    isInitialized = true;
                }
                else { Log.Warning("[AI Summarizer] Configuration incomplete"); }
            }
            catch (Exception ex)
            {
                Log.Error($"[AI Summarizer] Init failed: {ex.Message}");
                isInitialized = false;
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
            sb.AppendLine($"请为殖民者 {pawn.LabelShort} 总结以下记忆。");
            sb.AppendLine("\n记忆列表：");
            int i = 1;
            foreach (var m in memories.Take(20))
            {
                sb.AppendLine($"{i}. {m.content}");
                i++;
            }
            sb.AppendLine("\n要求：\n1. 提炼地点、人物、事件\n2. 相似事件合并，标注频率（×N）\n3. 极简表达，不超过80字\n4. 只输出总结文字");
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
