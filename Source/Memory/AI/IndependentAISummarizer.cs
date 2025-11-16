using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.AI
{
    /// <summary>
    /// 独立的AI总结服务 - 不依赖RimTalk，直接调用AI API
    /// </summary>
    public static class IndependentAISummarizer
    {
        private static HttpClient httpClient = null;
        private static bool isInitialized = false;
        private static string apiKey = "";
        private static string apiUrl = "";
        private static string model = "";
        private static string provider = ""; // OpenAI, Google等
        
        // 缓存正在进行的总结任务
        private static Dictionary<string, string> completedSummaries = new Dictionary<string, string>();
        private static HashSet<string> pendingSummaries = new HashSet<string>();
        
        /// <summary>
        /// 初始化HttpClient
        /// </summary>
        private static void InitializeHttpClient()
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                Log.Message("[Independent AI Summarizer] ✓ HttpClient initialized");
            }
        }
        
        /// <summary>
        /// 初始化AI服务（从RimTalk设置读取配置）
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            
            try
            {
                Log.Message("[Independent AI Summarizer] Initializing...");
                
                // 尝试从RimTalk读取API配置（只读，不调用方法）
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (rimTalkAssembly == null)
                {
                    Log.Warning("[Independent AI Summarizer] ❌ RimTalk assembly not found");
                    Log.Message("[Independent AI Summarizer] Will use rule-based summary instead");
                    return;
                }
                
                Log.Message($"[Independent AI Summarizer] ✓ Found RimTalk assembly: {rimTalkAssembly.FullName}");
                
                // 读取RimTalk的Settings
                var settingsType = rimTalkAssembly.GetType("RimTalk.Settings");
                if (settingsType == null)
                {
                    Log.Warning("[Independent AI Summarizer] ❌ RimTalk.Settings type not found");
                    return;
                }
                
                Log.Message("[Independent AI Summarizer] ✓ Found Settings type");
                
                var getMethod = settingsType.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getMethod == null)
                {
                    Log.Warning("[Independent AI Summarizer] ❌ Settings.Get() method not found");
                    return;
                }
                
                Log.Message("[Independent AI Summarizer] ✓ Found Settings.Get() method");
                
                var settings = getMethod.Invoke(null, null);
                if (settings == null)
                {
                    Log.Warning("[Independent AI Summarizer] ❌ Settings.Get() returned null");
                    return;
                }
                
                Log.Message("[Independent AI Summarizer] ✓ Got settings instance");
                Log.Message($"[Independent AI Summarizer] Settings type: {settings.GetType().FullName}");
                
                // 注意：Settings.Get()返回的是RimTalkSettings实例
                // GetActiveConfig()是RimTalkSettings的方法
                var settingsDataType = settings.GetType();
                var getActiveConfigMethod = settingsDataType.GetMethod("GetActiveConfig");
                
                if (getActiveConfigMethod == null)
                {
                    Log.Warning("[Independent AI Summarizer] ❌ GetActiveConfig() method not found");
                    
                    // 列出所有可用的方法（调试）
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Independent AI Summarizer] Available methods in {settingsDataType.Name}:");
                        foreach (var m in settingsDataType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            Log.Message($"  - {m.Name}");
                        }
                    }
                    return;
                }
                
                Log.Message("[Independent AI Summarizer] ✓ Found GetActiveConfig() method");
                
                var config = getActiveConfigMethod.Invoke(settings, null);
                if (config == null)
                {
                    Log.Warning("[Independent AI Summarizer] ❌ GetActiveConfig() returned null");
                    Log.Warning("[Independent AI Summarizer] Please configure API in RimTalk settings");
                    return;
                }
                
                Log.Message("[Independent AI Summarizer] ✓ Got config instance");
                
                var configType = config.GetType();
                Log.Message($"[Independent AI Summarizer] Config type: {configType.FullName}");
                
                // 注意：ApiConfig使用的是字段(Field)，不是属性(Property)
                
                // 读取API Key
                var apiKeyField = configType.GetField("ApiKey");
                if (apiKeyField != null)
                {
                    apiKey = apiKeyField.GetValue(config) as string;
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        Log.Message($"[Independent AI Summarizer] ✓ API Key: {apiKey.Substring(0, Math.Min(10, apiKey.Length))}...");
                    }
                    else
                    {
                        Log.Warning("[Independent AI Summarizer] ⚠️ API Key is empty");
                    }
                }
                else
                {
                    Log.Warning("[Independent AI Summarizer] ❌ ApiKey field not found");
                }
                
                // 读取Base URL
                var baseUrlField = configType.GetField("BaseUrl");
                if (baseUrlField != null)
                {
                    apiUrl = baseUrlField.GetValue(config) as string;
                    if (!string.IsNullOrEmpty(apiUrl))
                    {
                        Log.Message($"[Independent AI Summarizer] ✓ Base URL: {apiUrl}");
                    }
                    else
                    {
                        // BaseUrl为空时，使用Provider的默认URL
                        var providerField = configType.GetField("Provider");
                        if (providerField != null)
                        {
                            var providerValue = providerField.GetValue(config);
                            provider = providerValue.ToString();
                            Log.Message($"[Independent AI Summarizer] Provider: {provider}");
                            
                            // 根据Provider设置默认URL
                            if (provider == "OpenAI")
                            {
                                apiUrl = "https://api.openai.com/v1/chat/completions";
                                Log.Message($"[Independent AI Summarizer] Using OpenAI default URL: {apiUrl}");
                            }
                            else if (provider == "Google")
                            {
                                // Google Gemini API URL格式: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
                                // 我们需要API Key来构建完整URL
                                if (!string.IsNullOrEmpty(apiKey))
                                {
                                    // 先设置一个临时值，稍后在BuildJsonRequest中会用model替换
                                    apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/MODEL_PLACEHOLDER:generateContent?key=" + apiKey;
                                    Log.Message($"[Independent AI Summarizer] ✓ Using Google Gemini API");
                                }
                                else
                                {
                                    Log.Warning("[Independent AI Summarizer] Google Gemini requires API Key");
                                }
                            }
                            else
                            {
                                Log.Warning("[Independent AI Summarizer] Unknown provider, BaseUrl required");
                            }
                        }
                    }
                }
                else
                {
                    Log.Warning("[Independent AI Summarizer] ❌ BaseUrl field not found");
                }
                
                // 读取Model
                var modelField = configType.GetField("SelectedModel");
                if (modelField != null)
                {
                    model = modelField.GetValue(config) as string;
                    if (!string.IsNullOrEmpty(model))
                    {
                        Log.Message($"[Independent AI Summarizer] ✓ Model: {model}");
                    }
                    else
                    {
                        Log.Warning("[Independent AI Summarizer] ⚠️ Model is empty");
                        model = "gpt-3.5-turbo"; // 默认模型
                    }
                }
                else
                {
                    // 尝试CustomModelName
                    var customModelField = configType.GetField("CustomModelName");
                    if (customModelField != null)
                    {
                        model = customModelField.GetValue(config) as string;
                        if (!string.IsNullOrEmpty(model))
                        {
                            Log.Message($"[Independent AI Summarizer] ✓ Custom Model: {model}");
                        }
                    }
                    
                    if (string.IsNullOrEmpty(model))
                    {
                        Log.Warning("[Independent AI Summarizer] ❌ Model field not found");
                        model = "gpt-3.5-turbo"; // 默认模型
                    }
                }

                // 检查配置是否完整
                if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiUrl))
                {
                    Log.Message($"[Independent AI Summarizer] ✅ Configuration complete! Ready to use AI summarization");
                    
                    // 初始化HttpClient
                    InitializeHttpClient();
                    
                    isInitialized = true;
                }
                else
                {
                    Log.Warning("[Independent AI Summarizer] ❌ Configuration incomplete:");
                    if (string.IsNullOrEmpty(apiKey)) Log.Warning("  - API Key is missing");
                    if (string.IsNullOrEmpty(apiUrl)) Log.Warning("  - API URL is missing");
                    Log.Message("[Independent AI Summarizer] Please configure API in RimTalk settings");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Independent AI Summarizer] Initialization failed: {ex.Message}");
                if (Prefs.DevMode)
                    Log.Error($"[Independent AI Summarizer] Stack trace: {ex.StackTrace}");
                isInitialized = false;
            }
            
            if (!isInitialized)
            {
                Log.Message("[Independent AI Summarizer] AI summarization disabled, using rule-based summary");
            }
        }
        
        /// <summary>
        /// 检查是否可用
        /// </summary>
        public static bool IsAvailable()
        {
            if (!isInitialized)
                Initialize();
            
            return isInitialized;
        }
        
        /// <summary>
        /// 总结记忆（异步，非阻塞）
        /// </summary>
        public static string SummarizeMemories(Verse.Pawn pawn, List<Memory.MemoryEntry> memories, string promptTemplate)
        {
            if (!IsAvailable())
            {
                if (Prefs.DevMode)
                    Log.Message("[Independent AI Summarizer] Not available, skipping");
                return null;
            }
            
            string cacheKey = $"{pawn.ThingID}_{memories.Count}_{memories.GetHashCode()}";
            
            // 检查是否已经有完成的结果
            if (completedSummaries.TryGetValue(cacheKey, out string cachedResult))
            {
                Log.Message($"[Independent AI Summarizer] ✅ Using cached result for {pawn.LabelShort}");
                completedSummaries.Remove(cacheKey); // 使用后移除
                return cachedResult;
            }
            
            // 检查是否正在处理中
            if (pendingSummaries.Contains(cacheKey))
            {
                if (Prefs.DevMode)
                    Log.Message($"[Independent AI Summarizer] ⏳ Already processing for {pawn.LabelShort}, waiting...");
                return null; // 还在处理中，返回null使用简单总结
            }
            
            // 标记为处理中
            pendingSummaries.Add(cacheKey);
            Log.Message($"[Independent AI Summarizer] 🚀 Starting async task for {pawn.LabelShort}...");
            
            // 构建请求
            string prompt = BuildPrompt(pawn, memories, promptTemplate);
            
            // 在后台线程执行（不阻塞Unity主线程）
            Task.Run(async () =>
            {
                try
                {
                    Log.Message($"[Independent AI Summarizer] 🤖 Background task started for {pawn.LabelShort}");
                    
                    string result = await CallAIAsync(prompt);
                    
                    if (result != null)
                    {
                        Log.Message($"[Independent AI Summarizer] ✅ Task completed for {pawn.LabelShort}: {result.Substring(0, Math.Min(60, result.Length))}...");
                        completedSummaries[cacheKey] = result;
                    }
                    else
                    {
                        Log.Warning($"[Independent AI Summarizer] ⚠️ Task completed but result is null for {pawn.LabelShort}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Independent AI Summarizer] ❌ Background task failed for {pawn.LabelShort}: {ex.Message}");
                }
                finally
                {
                    pendingSummaries.Remove(cacheKey);
                }
            });
            
            // 立即返回null，使用简单总结作为临时结果
            // AI完成后，下次调用时会返回缓存的结果
            Log.Message($"[Independent AI Summarizer] 📤 Task submitted, using simple summary as temporary result");
            return null;
        }
        
        /// <summary>
        /// 构建提示词
        /// </summary>
        private static string BuildPrompt(Verse.Pawn pawn, List<Memory.MemoryEntry> memories, string template)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"请为殖民者 {pawn.LabelShort} 总结以下记忆。");
            sb.AppendLine();
            sb.AppendLine("记忆列表：");
            
            int i = 1;
            foreach (var m in memories)
            {
                if (i > 20) break; // 最多20条
                sb.AppendLine($"{i}. {m.content}");
                i++;
            }
            
            sb.AppendLine();
            sb.AppendLine("要求：");
            sb.AppendLine("1. 提炼地点、人物、事件");
            sb.AppendLine("2. 相似事件合并，标注频率（×N）");
            sb.AppendLine("3. 极简表达，不超过80字");
            sb.AppendLine("4. 只输出总结文字，不要JSON或其他格式");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 手动构建JSON字符串（避免依赖Newtonsoft.Json）
        /// </summary>
        private static string BuildJsonRequest(string prompt)
        {
            // 转义特殊字符
            string escapedPrompt = prompt
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "")
                .Replace("\t", "\\t");
            
            var sb = new StringBuilder();
            
            if (provider == "Google")
            {
                // Google Gemini API 格式
                sb.Append("{");
                sb.Append("\"contents\":[{");
                sb.Append("\"parts\":[{");
                sb.Append($"\"text\":\"{escapedPrompt}\"");
                sb.Append("}]");
                sb.Append("}],");
                sb.Append("\"generationConfig\":{");
                sb.Append("\"temperature\":0.7,");
                sb.Append("\"maxOutputTokens\":200");
                sb.Append("}");
                sb.Append("}");
            }
            else
            {
                // OpenAI API 格式（默认）
                sb.Append("{");
                sb.Append($"\"model\":\"{model}\",");
                sb.Append("\"messages\":[");
                sb.Append("{\"role\":\"user\",");
                sb.Append($"\"content\":\"{escapedPrompt}\"");
                sb.Append("}],");
                sb.Append("\"temperature\":0.7,");
                sb.Append("\"max_tokens\":200");
                sb.Append("}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 调用AI API（异步）
        /// </summary>
        private static async Task<string> CallAIAsync(string prompt)
        {
            try
            {
                // 确保HttpClient已初始化
                if (httpClient == null)
                {
                    Log.Warning("[Independent AI Summarizer] HttpClient not initialized, initializing now...");
                    InitializeHttpClient();
                }
                
                Log.Message($"[Independent AI Summarizer] 📤 Preparing request...");
                Log.Message($"[Independent AI Summarizer]   Provider: {provider}");
                Log.Message($"[Independent AI Summarizer]   Model: {model}");
                Log.Message($"[Independent AI Summarizer]   Prompt length: {prompt.Length} chars");
                
                // 手动构建JSON（不使用Newtonsoft.Json）
                string json = BuildJsonRequest(prompt);
                
                // 处理Gemini的URL（替换MODEL_PLACEHOLDER）
                string actualUrl = apiUrl;
                if (provider == "Google" && actualUrl.Contains("MODEL_PLACEHOLDER"))
                {
                    actualUrl = actualUrl.Replace("MODEL_PLACEHOLDER", model);
                }
                
                Log.Message($"[Independent AI Summarizer]   API URL: {actualUrl}");
                
                if (Prefs.DevMode)
                    Log.Message($"[Independent AI Summarizer]   Request JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // 设置Headers
                httpClient.DefaultRequestHeaders.Clear();
                
                if (provider == "Google")
                {
                    // Gemini不需要Authorization header，API key在URL中
                }
                else
                {
                    // OpenAI需要Authorization header
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
                
                Log.Message($"[Independent AI Summarizer] 🌐 Sending HTTP POST request...");
                
                // 发送请求
                var response = await httpClient.PostAsync(actualUrl, content);
                
                Log.Message($"[Independent AI Summarizer] 📥 Got HTTP response: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Log.Warning($"[Independent AI Summarizer] ❌ API error: {response.StatusCode}");
                    Log.Warning($"[Independent AI Summarizer]   Error body: {errorBody.Substring(0, Math.Min(500, errorBody.Length))}");
                    return null;
                }
                
                string responseText = await response.Content.ReadAsStringAsync();
                Log.Message($"[Independent AI Summarizer] ✓ Response received: {responseText.Length} chars");
                
                if (Prefs.DevMode)
                    Log.Message($"[Independent AI Summarizer]   Response: {responseText.Substring(0, Math.Min(300, responseText.Length))}...");
                
                // 手动解析JSON响应（简单字符串查找）
                try
                {
                    string result = null;
                    
                    if (provider == "Google")
                    {
                        // Gemini格式: {"candidates":[{"content":{"parts":[{"text":"..."}]}}]}
                        int textStart = responseText.IndexOf("\"text\":");
                        if (textStart > 0)
                        {
                            textStart = responseText.IndexOf("\"", textStart + 7) + 1;
                            int textEnd = responseText.IndexOf("\"", textStart);
                            
                            // 处理转义的引号
                            while (textEnd > 0 && responseText[textEnd - 1] == '\\')
                            {
                                textEnd = responseText.IndexOf("\"", textEnd + 1);
                            }
                            
                            if (textEnd > textStart)
                            {
                                result = responseText.Substring(textStart, textEnd - textStart);
                            }
                        }
                    }
                    else
                    {
                        // OpenAI格式: {"choices":[{"message":{"content":"..."}}]}
                        int contentStart = responseText.IndexOf("\"content\":");
                        if (contentStart > 0)
                        {
                            contentStart = responseText.IndexOf("\"", contentStart + 10) + 1;
                            int contentEnd = responseText.IndexOf("\"", contentStart);
                            
                            // 处理转义的引号
                            while (contentEnd > 0 && responseText[contentEnd - 1] == '\\')
                            {
                                contentEnd = responseText.IndexOf("\"", contentEnd + 1);
                            }
                            
                            if (contentEnd > contentStart)
                            {
                                result = responseText.Substring(contentStart, contentEnd - contentStart);
                            }
                        }
                    }
                    
                    if (result != null)
                    {
                        // 处理转义字符
                        result = result.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                        
                        Log.Message($"[Independent AI Summarizer] ✅ Parsed result: {result.Substring(0, Math.Min(100, result.Length))}...");
                        return result?.Trim();
                    }
                    
                    Log.Warning($"[Independent AI Summarizer] ❌ Could not find content/text field in response");
                    Log.Message($"[Independent AI Summarizer]   Full response: {responseText}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[Independent AI Summarizer] ❌ Parse error: {ex.Message}");
                    if (Prefs.DevMode)
                        Log.Error($"  Stack: {ex.StackTrace}");
                }
                
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Log.Error($"[Independent AI Summarizer] ⏱️ Request timeout: {ex.Message}");
                Log.Error($"[Independent AI Summarizer]   API might be slow or unreachable");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"[Independent AI Summarizer] 🌐 Network error: {ex.Message}");
                Log.Error($"[Independent AI Summarizer]   Check internet connection and API URL");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[Independent AI Summarizer] ❌ API call failed: {ex.GetType().Name}");
                Log.Error($"[Independent AI Summarizer]   Message: {ex.Message}");
                if (Prefs.DevMode)
                    Log.Error($"  Stack: {ex.StackTrace}");
                return null;
            }
        }
    }
}
