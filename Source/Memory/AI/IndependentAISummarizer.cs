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
        
        // ⭐ 修复1: 添加缓存大小限制，防止内存泄漏
        private const int MAX_CACHE_SIZE = 100; // 最多缓存100个总结
        private const int CACHE_CLEANUP_THRESHOLD = 120; // 达到120个时清理
        
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

        /// <summary>
        /// ⭐ 修复：添加强制重新初始化方法
        /// </summary>
        public static void ForceReinitialize()
        {
            isInitialized = false;
            Initialize();
        }
        
        public static void Initialize()
        {
            // ⭐ 修复：移除isInitialized检查，允许重新初始化
            // if (isInitialized) return; // <-- 删除这行
            
            try
            {
                var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
                
                // ⭐ 修复：严格按照用户设置决定是否跟随RimTalk
                if (settings.useRimTalkAIConfig)
                {
                    // 用户选择跟随RimTalk，尝试加载
                    if (TryLoadFromRimTalk())
                    {
                        // 成功从RimTalk加载
                        Log.Message($"[AI] Loaded from RimTalk ({provider}/{model})");
                        isInitialized = true;
                        return;
                    }
                    // 如果RimTalk未配置，继续使用独立配置（fallback）
                    Log.Warning("[AI] RimTalk not configured, using independent config as fallback");
                }
                
                // 使用独立配置（用户主动选择 或 RimTalk未配置）
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
                    isInitialized = false; // ⭐ 标记为未初始化，允许下次重试
                    return;
                }
                
                Log.Message($"[AI] Initialized with independent config ({provider}/{model})");
                isInitialized = true;
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
                            // ⭐ 修复1: 清理过大的缓存，防止内存泄漏
                            if (completedSummaries.Count >= CACHE_CLEANUP_THRESHOLD)
                            {
                                // 移除最旧的50%条目（使用FIFO策略）
                                var toRemove = completedSummaries.Keys.Take(MAX_CACHE_SIZE / 2).ToList();
                                foreach (var key in toRemove)
                                {
                                    completedSummaries.Remove(key);
                                }
                                
                                if (Prefs.DevMode)
                                {
                                    Log.Message($"[AI Summarizer] 🧹 Cleaned cache: {toRemove.Count} entries removed, {completedSummaries.Count} remaining");
                                }
                            }
                            
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
            
            // 统一极简格式
            if (template == "deep_archive")
            {
                // 深度归档
                sb.AppendLine($"殖民者{pawn.LabelShort}的记忆归档");
                sb.AppendLine();
                sb.AppendLine("记忆列表");
                int i = 1;
                foreach (var m in memories.Take(15))
                {
                    sb.AppendLine($"{i} {m.content}");
                    i++;
                }
                sb.AppendLine();
                sb.AppendLine("要求提炼核心特征和里程碑事件");
                sb.AppendLine("合并相似经历突出长期趋势");
                sb.AppendLine("极简表达不超过60字");
                sb.AppendLine("只输出总结文字不要其他格式");
            }
            else
            {
                // 每日总结
                sb.AppendLine($"殖民者{pawn.LabelShort}的记忆总结");
                sb.AppendLine();
                sb.AppendLine("记忆列表");
                int i = 1;
                foreach (var m in memories.Take(20))
                {
                    sb.AppendLine($"{i} {m.content}");
                    i++;
                }
                sb.AppendLine();
                sb.AppendLine("要求提炼地点人物事件");
                sb.AppendLine("相似事件合并标注频率");
                sb.AppendLine("极简表达不超过80字");
                sb.AppendLine("只输出总结文字不要其他格式");
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
            const int MAX_RETRIES = 3;
            const int RETRY_DELAY_MS = 2000; // 2秒重试延迟
            
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    string actualUrl = apiUrl;
                    if (provider == "Google")
                    {
                        actualUrl = apiUrl.Replace("MODEL_PLACEHOLDER", model).Replace("API_KEY_PLACEHOLDER", apiKey);
                    }

                    if (attempt > 1)
                    {
                        Log.Message($"[AI Summarizer] Retry attempt {attempt}/{MAX_RETRIES}...");
                    }
                    else
                    {
                        Log.Message($"[AI Summarizer] Calling API: {actualUrl.Substring(0, Math.Min(60, actualUrl.Length))}...");
                    }

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

                    using (var response = (HttpWebResponse)await request.GetResponseAsync())
                    using (var streamReader = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        string responseText = await streamReader.ReadToEndAsync();
                        string result = ParseResponse(responseText);
                        
                        if (attempt > 1)
                        {
                            Log.Message($"[AI Summarizer] ✅ Retry successful on attempt {attempt}");
                        }
                        
                        return result;
                    }
                }
                catch (WebException ex)
                {
                    bool shouldRetry = false;
                    string errorDetail = "";
                    
                    if (ex.Response != null)
                    {
                        using (var errorResponse = (HttpWebResponse)ex.Response)
                        using (var streamReader = new System.IO.StreamReader(errorResponse.GetResponseStream()))
                        {
                            string errorText = streamReader.ReadToEnd();
                            errorDetail = errorText.Substring(0, Math.Min(200, errorText.Length));
                            
                            // 判断是否应该重试
                            if (errorResponse.StatusCode == HttpStatusCode.ServiceUnavailable || // 503
                                errorResponse.StatusCode == (HttpStatusCode)429 ||              // Too Many Requests
                                errorResponse.StatusCode == HttpStatusCode.GatewayTimeout ||    // 504
                                errorText.Contains("overloaded") ||
                                errorText.Contains("UNAVAILABLE"))
                            {
                                shouldRetry = true;
                            }
                            
                            Log.Warning($"[AI Summarizer] ⚠️ API Error (attempt {attempt}/{MAX_RETRIES}): {errorResponse.StatusCode} - {errorDetail}");
                        }
                    }
                    else
                    {
                        errorDetail = ex.Message;
                        Log.Warning($"[AI Summarizer] ⚠️ Network Error (attempt {attempt}/{MAX_RETRIES}): {errorDetail}");
                        shouldRetry = true; // 网络错误也重试
                    }
                    
                    // 如果是最后一次尝试或不应该重试，则失败
                    if (attempt >= MAX_RETRIES || !shouldRetry)
                    {
                        Log.Error($"[AI Summarizer] ❌ Failed after {attempt} attempts. Last error: {errorDetail}");
                        return null;
                    }
                    
                    // 等待后重试
                    await Task.Delay(RETRY_DELAY_MS * attempt); // 递增延迟：2s, 4s, 6s
                }
                catch (Exception ex)
                {
                    Log.Error($"[AI Summarizer] ❌ Unexpected error: {ex.GetType().Name} - {ex.Message}");
                    return null;
                }
            }
            
            return null;
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
