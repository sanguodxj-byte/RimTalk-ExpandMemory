using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using RimWorld;  // ? v3.3.6: 添加 RimWorld 命名空间

namespace RimTalk.Memory.AI
{
    // ? v3.3.2.34: DTO 类定义 - OpenAI 兼容格式
    [Serializable]
    public class OpenAIRequest
    {
        public string model;
        public OpenAIMessage[] messages;
        public float temperature;
        public int max_tokens;
        public bool enable_prompt_cache; // DeepSeek
    }
    
    [Serializable]
    public class OpenAIMessage
    {
        public string role;
        public string content;
        public CacheControl cache_control; // OpenAI Prompt Caching
        public bool cache; // DeepSeek cache
    }
    
    [Serializable]
    public class CacheControl
    {
        public string type;
    }
    
    // ? v3.3.2.34: DTO 类定义 - Google Gemini 格式
    [Serializable]
    public class GeminiRequest
    {
        public GeminiContent[] contents;
        public GeminiGenerationConfig generationConfig;
    }
    
    [Serializable]
    public class GeminiContent
    {
        public GeminiPart[] parts;
    }
    
    [Serializable]
    public class GeminiPart
    {
        public string text;
    }
    
    [Serializable]
    public class GeminiGenerationConfig
    {
        public float temperature;
        public int maxOutputTokens;
        public GeminiThinkingConfig thinkingConfig; // 可选
    }
    
    [Serializable]
    public class GeminiThinkingConfig
    {
        public int thinkingBudget;
    }
    
    public static class IndependentAISummarizer
    {
        // ? v3.3.2.35: 优化正则表达式 - 提升为静态编译字段
        private static readonly Regex GoogleResponseRegex = new Regex(
            @"""text""\s*:\s*""(.*?)""",
            RegexOptions.Compiled | RegexOptions.Singleline
        );
        
        private static readonly Regex OpenAIResponseRegex = new Regex(
            @"""content""\s*:\s*""(.*?)""",
            RegexOptions.Compiled | RegexOptions.Singleline
        );
        
        private static readonly Regex Player2KeyRegex = new Regex(
            @"""p2Key""\s*:\s*""([^""]+)""",
            RegexOptions.Compiled
        );
        
        private static bool isInitialized = false;
        private static string apiKey, apiUrl, model, provider;
        
        // ? 修复1: 添加缓存大小限制，防止内存泄漏
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
        /// ? 修复：添加强制重新初始化方法
        /// </summary>
        public static void ForceReinitialize()
        {
            isInitialized = false;
            Initialize();
        }
        
        /// <summary>
        /// ? v3.3.3: 清除所有API配置和缓存
        /// </summary>
        public static void ClearAllConfiguration()
        {
            // 清除静态变量
            apiKey = "";
            apiUrl = "";
            model = "";
            provider = "";
            isInitialized = false;
            
            // 清除所有缓存
            lock (completedSummaries)
            {
                completedSummaries.Clear();
            }
            
            lock (pendingSummaries)
            {
                pendingSummaries.Clear();
            }
            
            lock (callbackMap)
            {
                callbackMap.Clear();
            }
            
            lock (mainThreadActions)
            {
                mainThreadActions.Clear();
            }
            
            Log.Message("[AI] ?? All API configuration and cache cleared");
        }
        
        public static void Initialize()
        {
            try
            {
                var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
                
                // ? 修复：严格按照用户设置决定是否跟随RimTalk
                if (settings.useRimTalkAIConfig)
                {
                    if (TryLoadFromRimTalk())
                    {
                        if (ValidateConfiguration())
                        {
                            Log.Message($"[AI] ? Loaded from RimTalk ({provider}/{model})");
                            isInitialized = true;
                            return;
                        }
                        else
                        {
                            Log.Warning("[AI] ?? RimTalk config invalid, using independent config");
                        }
                    }
                    else
                    {
                        Log.Warning("[AI] ?? RimTalk not configured, using independent config as fallback");
                    }
                }
                
                // 使用独立配置
                apiKey = settings.independentApiKey;
                apiUrl = settings.independentApiUrl;
                model = settings.independentModel;
                provider = settings.independentProvider;
                
                // ? v3.3.6: Player2 特殊处理 - 优先使用本地应用
                if (provider == "Player2")
                {
                    if (isPlayer2Local && !string.IsNullOrEmpty(player2LocalKey))
                    {
                        // 使用本地 Player2 应用
                        apiKey = player2LocalKey;
                        apiUrl = $"{Player2LocalUrl}/chat/completions";
                        Log.Message("[AI] ?? Using Player2 local app connection");
                    }
                    else if (!string.IsNullOrEmpty(apiKey))
                    {
                        // 使用手动输入的 Key + 远程 API
                        apiUrl = $"{Player2RemoteUrl}/chat/completions";
                        Log.Message("[AI] ?? Using Player2 remote API with manual key");
                    }
                    else
                    {
                        // 尝试检测本地应用
                        Log.Message("[AI] ?? Player2 selected but no key, trying to detect local app...");
                        TryDetectPlayer2LocalApp();
                    }
                }
                
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
                    else if (provider == "Player2")
                    {
                        apiUrl = $"{Player2RemoteUrl}/chat/completions";
                    }
                }
                
                // ? 详细验证配置
                if (!ValidateConfiguration())
                {
                    isInitialized = false;
                    return;
                }
                
                Log.Message($"[AI] ? Initialized with independent config ({provider}/{model})");
                Log.Message($"[AI]    API Key: {SanitizeApiKey(apiKey)}");
                Log.Message($"[AI]    API URL: {apiUrl}");
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[AI] ? Init failed: {ex.Message}");
                isInitialized = false;
            }
        }
        
        /// <summary>
        /// ? v3.3.3: 验证API配置
        /// </summary>
        private static bool ValidateConfiguration()
        {
            // 检查API Key
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("[AI] ? API Key is empty!");
                Log.Error("[AI]    Please configure in: Options → Mod Settings → RimTalk-Expand Memory → AI配置");
                return false;
            }
            
            // 检查API Key长度
            if (apiKey.Length < 10)
            {
                Log.Error($"[AI] ? API Key too short (length: {apiKey.Length})!");
                Log.Error("[AI]    Valid API Keys are usually 20+ characters");
                Log.Error($"[AI]    Your key: {SanitizeApiKey(apiKey)}");
                return false;
            }
            
            // ? v3.3.6: Player2/Custom模式不强制检查格式
            if (provider != "Custom" && provider != "Player2" && provider != "Google")
            {
                // 检查API Key格式（OpenAI/DeepSeek建议以sk-开头，但只是警告）
                if ((provider == "OpenAI" || provider == "DeepSeek") && !apiKey.StartsWith("sk-"))
                {
                    Log.Warning($"[AI] ?? API Key doesn't start with 'sk-' for {provider}");
                    Log.Warning($"[AI]    Your key: {SanitizeApiKey(apiKey)}");
                    Log.Warning("[AI]    If using third-party proxy, select 'Custom' or 'Player2' provider");
                }
            }
            
            // 检查API URL
            if (string.IsNullOrEmpty(apiUrl))
            {
                Log.Error("[AI] ? API URL is empty!");
                return false;
            }
            
            // 检查Model
            if (string.IsNullOrEmpty(model))
            {
                Log.Warning("[AI] ?? Model name is empty, using default");
                model = "gpt-3.5-turbo";
            }
            
            return true;
        }
        
        /// <summary>
        /// ? v3.3.3: 安全显示API Key（只显示前后缀）
        /// </summary>
        private static string SanitizeApiKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "(empty)";
            
            if (key.Length <= 10)
                return key.Substring(0, Math.Min(3, key.Length)) + "...";
            
            return $"{key.Substring(0, 7)}...{key.Substring(key.Length - 4)} (length: {key.Length})";
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
                        else if (provider == "Player2")
                        {
                            apiUrl = "https://api.player2.live/v1/chat/completions";
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
                            // ? 修改1: 增加缓存上限，防止内存泄漏
                            if (completedSummaries.Count >= CACHE_CLEANUP_THRESHOLD)
                            {
                                // ? v3.3.2.29: 确定性清理 - 按 key 字母顺序升序排序后删除前50%
                                // 使用字母顺序排序代替随机 Take()，确保相同的缓存状态总是删除相同的条目
                                var toRemove = completedSummaries.Keys
                                    .OrderBy(k => k, StringComparer.Ordinal) // 字母顺序升序
                                    .Take(MAX_CACHE_SIZE / 2)
                                    .ToList();
                                
                                foreach (var key in toRemove)
                                {
                                    completedSummaries.Remove(key);
                                }
                                

                                if (Prefs.DevMode)
                                {
                                    Log.Message($"[AI Summarizer] ?? Cleaned cache: {toRemove.Count} entries removed (deterministic by key order), {completedSummaries.Count} remaining");
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
            var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
            
            // 构建记忆列表
            var memoryListSb = new StringBuilder();
            int maxMemories = (template == "deep_archive") ? 15 : 20;
            int i = 1;
            foreach (var m in memories.Take(maxMemories))
            {
                memoryListSb.AppendLine($"{i} {m.content}");
                i++;
            }
            string memoryList = memoryListSb.ToString().TrimEnd();
            
            // 使用自定义提示词或默认提示词
            string promptTemplate;
            
            if (template == "deep_archive")
            {
                // 深度归档
                if (!string.IsNullOrEmpty(settings.deepArchivePrompt))
                {
                    // 使用自定义提示词
                    promptTemplate = settings.deepArchivePrompt;
                }
                else
                {
                    // 使用默认提示词
                    promptTemplate = 
                        "殖民者{0}的记忆归档\n\n" +
                        "记忆列表\n" +
                        "{1}\n\n" +
                        "要求提炼核心特征和里程碑事件\n" +
                        "合并相似经历突出长期趋势\n" +
                        "极简表达不超过60字\n" +
                        "只输出总结文字不要其他格式";
                }
            }
            else
            {
                // 每日总结
                if (!string.IsNullOrEmpty(settings.dailySummaryPrompt))
                {
                    // 使用自定义提示词
                    promptTemplate = settings.dailySummaryPrompt;
                }
                else
                {
                    // 使用默认提示词
                    promptTemplate = 
                        "殖民者{0}的记忆总结\n\n" +
                        "记忆列表\n" +
                        "{1}\n\n" +
                        "要求提炼地点人物事件\n" +
                        "相似事件合并标注频率\n" +
                        "极简表达不超过80字\n" +
                        "只输出总结文字不要其他格式";
                }
            }
            
            // 替换占位符
            string result = string.Format(promptTemplate, pawn.LabelShort, memoryList);
            
            return result;
        }

        /// <summary>
        /// ? v3.3.2.34: 重构版 - 使用 DTO 类和手动序列化（安全）
        /// 彻底修复特殊字符导致的 JSON 格式错误
        /// </summary>
        private static string BuildJsonRequest(string prompt)
        {
            bool isGoogle = (provider == "Google");
            var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
            bool enableCaching = settings != null && settings.enablePromptCaching;
            
            if (isGoogle)
            {
                // ? Google Gemini 格式
                string escapedPrompt = EscapeJsonString(prompt);
                
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"contents\":[{");
                sb.Append("\"parts\":[{");
                sb.Append($"\"text\":\"{escapedPrompt}\"");
                sb.Append("}]");
                sb.Append("}],");
                sb.Append("\"generationConfig\":{");
                sb.Append("\"temperature\":0.7,");
                int maxTokens = settings != null ? settings.summaryMaxTokens : 200;
                sb.Append($"\"maxOutputTokens\":{maxTokens}");
                
                if (model.Contains("flash"))
                {
                    sb.Append(",\"thinkingConfig\":{\"thinkingBudget\":0}");
                }
                
                sb.Append("}");
                sb.Append("}");
                
                return sb.ToString();
            }
            else
            {
                // ? OpenAI/DeepSeek/Player2/Custom - 统一使用OpenAI兼容格式
                
                // 固定的系统指令（可缓存）
                string systemPrompt = "你是一个RimWorld殖民地的记忆总结助手。\n" +
                                    "请用极简的语言总结记忆内容。\n" +
                                    "只输出总结文字，不要其他格式。";
                
                string escapedSystem = EscapeJsonString(systemPrompt);
                string escapedPrompt = EscapeJsonString(prompt);
                
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"model\":\"{model}\",");
                sb.Append("\"messages\":[");
                
                // system消息（带缓存控制）
                sb.Append("{\"role\":\"system\",");
                sb.Append($"\"content\":\"{escapedSystem}\"");
                
                if (enableCaching)
                {
                    // ? OpenAI/Custom/Player2 都尝试使用 cache_control
                    if ((provider == "OpenAI" || provider == "Custom" || provider == "Player2") && 
                        (model.Contains("gpt-4") || model.Contains("gpt-3.5")))
                    {
                        // OpenAI Prompt Caching
                        sb.Append(",\"cache_control\":{\"type\":\"ephemeral\"}");
                    }
                    else if (provider == "DeepSeek")
                    {
                        // DeepSeek缓存控制
                        sb.Append(",\"cache\":true");
                    }
                }
                
                sb.Append("},");
                
                // user消息（变化的内容）
                sb.Append("{\"role\":\"user\",");
                sb.Append($"\"content\":\"{escapedPrompt}\"");
                sb.Append("}],");
                
                sb.Append("\"temperature\":0.7,");
                int maxTokens = settings != null ? settings.summaryMaxTokens : 200;
                sb.Append($"\"max_tokens\":{maxTokens}");

                
                if (enableCaching && provider == "DeepSeek")
                {
                    sb.Append(",\"enable_prompt_cache\":true");
                }
                
                sb.Append("}");
                
                return sb.ToString();
            }
        }
        
        /// <summary>
        /// ? v3.3.2.34: 安全的 JSON 字符串转义
        /// 处理所有特殊字符：引号、换行、反斜杠等
        /// </summary>
        private static string EscapeJsonString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            
            var sb = new StringBuilder(text.Length + 20);
            
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        // 其他控制字符使用 Unicode 转义
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            
            return sb.ToString();
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
                        Log.Message($"[AI Summarizer]   Provider: {provider}");
                        Log.Message($"[AI Summarizer]   Model: {model}");
                        Log.Message($"[AI Summarizer]   API Key: {SanitizeApiKey(apiKey)}");
                    }

                    var request = (HttpWebRequest)WebRequest.Create(actualUrl);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    
                    // ? v3.3.3: Google API不使用Bearer token（Key在URL中）
                    if (provider != "Google")
                    {
                        request.Headers["Authorization"] = $"Bearer {apiKey}";
                    }
                    
                    // ? 增加超时时间到120秒（2分钟）
                    request.Timeout = 120000; // 原来是30000（30秒）

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
                        
                        // ? v3.3.7: 添加响应接收确认日志
                        Log.Message($"[AI Summarizer] ✅ Response received, length: {responseText.Length} chars");
                        
                        string result = ParseResponse(responseText);
                        
                        // ? v3.3.7: 添加解析结果确认日志
                        if (result != null)
                        {
                            Log.Message($"[AI Summarizer] ✅ Parse successful, result length: {result.Length} chars");
                        }
                        else
                        {
                            Log.Warning($"[AI Summarizer] ⚠️ Parse returned null!");
                        }
                        
                        if (attempt > 1)
                        {
                            Log.Message($"[AI Summarizer] ? Retry successful on attempt {attempt}");
                        }
                        
                        return result;
                    }
                }
                catch (WebException ex)
                {
                    bool shouldRetry = false;
                    string errorDetail = "";
                    HttpStatusCode statusCode = 0; // ? v3.3.3: 保存状态码到外部变量
                    


	                if (ex.Response != null)
	                {
	                    using (var errorResponse = (HttpWebResponse)ex.Response)
	                    using (var streamReader = new System.IO.StreamReader(errorResponse.GetResponseStream()))
	                    {
	                        string errorText = streamReader.ReadToEnd();
	                        statusCode = errorResponse.StatusCode; // ? 保存状态码
	                        
	                        // ? v3.3.3: 根据错误类型显示完整或截断的错误信息
	                        if (errorResponse.StatusCode == HttpStatusCode.Unauthorized || // 401
	                            errorResponse.StatusCode == HttpStatusCode.Forbidden)      // 403
	                        {
	                            // 认证错误：显示完整错误信息（帮助调试）
	                            errorDetail = errorText;
	                            Log.Error($"[AI Summarizer] ? Authentication Error ({errorResponse.StatusCode}):");
	                            Log.Error($"[AI Summarizer]    API Key: {SanitizeApiKey(apiKey)}");
	                            Log.Error($"[AI Summarizer]    Provider: {provider}");
	                            Log.Error($"[AI Summarizer]    Response: {errorText}");
	                            Log.Error("[AI Summarizer] ");
	                            Log.Error("[AI Summarizer] ?? Possible solutions:");
	                            Log.Error("[AI Summarizer]    1. Check if API Key is correct");
	                            Log.Error("[AI Summarizer]    2. Verify Provider selection matches your key");
	                            Log.Error("[AI Summarizer]    3. Check if API Key has sufficient credits");
	                            Log.Error("[AI Summarizer]    4. Try regenerating your API Key");
	                        }
	                        else
	                        {
	                            // 其他错误：截断显示
	                            errorDetail = errorText.Substring(0, Math.Min(200, errorText.Length));
	                        }
	                        
	                        // 判断是否应该重试
	                        if (errorResponse.StatusCode == HttpStatusCode.ServiceUnavailable || // 503
	                            errorResponse.StatusCode == (HttpStatusCode)429 ||              // Too Many Requests
	                            errorResponse.StatusCode == HttpStatusCode.GatewayTimeout ||    // 504
	                            errorText.Contains("overloaded") ||
	                            errorText.Contains("UNAVAILABLE"))
	                        {
	                            shouldRetry = true;
	                        }
	                        
	                        if (errorResponse.StatusCode != HttpStatusCode.Unauthorized && 
	                            errorResponse.StatusCode != HttpStatusCode.Forbidden)
	                        {
	                            Log.Warning($"[AI Summarizer] ?? API Error (attempt {attempt}/{MAX_RETRIES}): {errorResponse.StatusCode} - {errorDetail}");
	                        }
	                    }
	                }
	                else
	                {
	                    errorDetail = ex.Message;
	                    Log.Warning($"[AI Summarizer] ?? Network Error (attempt {attempt}/{MAX_RETRIES}): {errorDetail}");
	                    shouldRetry = true; // 网络错误也重试
	                }
	                
	                // 如果是最后一次尝试或不应该重试，则失败
	                if (attempt >= MAX_RETRIES || !shouldRetry)
	                {
	                    // ? v3.3.3: 使用保存的状态码判断
	                    if (statusCode != HttpStatusCode.Unauthorized && 
	                        statusCode != HttpStatusCode.Forbidden)
	                    {
	                        Log.Error($"[AI Summarizer] ? Failed after {attempt} attempts. Last error: {errorDetail}");
	                    }
	                    return null;
	                }
	                
	                // 等待后重试
	                await Task.Delay(RETRY_DELAY_MS * attempt); // 递增延迟：2s, 4s, 6s
                }
                catch (Exception ex)
                {
                    Log.Error($"[AI Summarizer] ? Unexpected error: {ex.GetType().Name} - {ex.Message}");
                    Log.Error($"[AI Summarizer]    Stack trace: {ex.StackTrace}");
                    return null;
                }
            }
            
            return null;
        }

        /// <summary>
        /// ? v3.3.2.35: 优化版 - 使用静态编译的正则表达式
        /// </summary>
        private static string ParseResponse(string responseText)
        {
            // ? v3.3.7: 方法入口日志（确保方法被调用）
            Log.Message($"[AI Summarizer] 🔍 ParseResponse called, provider={provider}");
            
            try
            {
                // ? 调试日志：输出完整响应
                Log.Message($"[AI Summarizer] Full API Response (Length: {responseText.Length}):\n{responseText}");

                // 必须配合之前给你的那个能跳过转义引号的正则
                var regex = provider == "Google" ? GoogleResponseRegex : OpenAIResponseRegex;

                // ⭐ 核心修改：使用 Matches (复数) 抓取所有数据包
                var matches = regex.Matches(responseText);
                
                Log.Message($"[AI Summarizer] Regex matched {matches.Count} fragments");

                if (matches.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (Match match in matches)
                    {
                        string fragment = match.Groups[1].Value;
                        // 把每个数据包里的碎片拼起来
                        sb.Append(fragment);
                    }
                    // 拼完之后再统一反转义
                    string result = Regex.Unescape(sb.ToString());
                    Log.Message($"[AI Summarizer] Final parsed result: {result}");
                    return result;
                }
                else
                {
                    Log.Warning("[AI Summarizer] No matches found in response text!");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AI Summarizer] ?? Parse error: {ex.Message}");
            }
            return null;
        }
        
        // ? v3.3.6: Player2 本地应用支持
        private const string Player2LocalUrl = "http://localhost:4315/v1";
        private const string Player2RemoteUrl = "https://api.player2.game/v1";
        private const string Player2GameClientId = "rimtalk-expand-memory";
        private static bool isPlayer2Local = false;
        private static string player2LocalKey = null;
        
        /// <summary>
        /// ? v3.3.6: 尝试检测并连接本地 Player2 桌面应用
        /// </summary>
        public static void TryDetectPlayer2LocalApp()
        {
            Task.Run(async () =>
            {
                try
                {
                    Log.Message("[AI] ?? Checking for local Player2 app...");
                    
                    // 1. 健康检查
                    var healthRequest = (HttpWebRequest)WebRequest.Create($"{Player2LocalUrl}/health");
                    healthRequest.Method = "GET";
                    healthRequest.Timeout = 2000; // 2秒超时
                    
                    try
                    {
                        using (var response = (HttpWebResponse)await healthRequest.GetResponseAsync())
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                Log.Message("[AI] ? Player2 local app detected!");
                                
                                // 2. 获取本地Key
                                await TryGetPlayer2LocalKey();
                                
                                if (!string.IsNullOrEmpty(player2LocalKey))
                                {
                                    isPlayer2Local = true;
                                    LongEventHandler.ExecuteWhenFinished(() =>
                                    {
                                        Messages.Message("RimTalk_Settings_Player2Detected".Translate(), MessageTypeDefOf.PositiveEvent, false);
                                    });
                                    return;
                                }
                            }
                        }
                    }
                    catch (WebException)
                    {
                        // 本地应用未运行
                    }
                    
                    isPlayer2Local = false;
                    player2LocalKey = null;
                    Log.Message("[AI] ?? Player2 local app not found, will use remote API");
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        Messages.Message("RimTalk_Settings_Player2NotFound".Translate(), MessageTypeDefOf.NeutralEvent, false);
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning($"[AI] Player2 detection error: {ex.Message}");
                    isPlayer2Local = false;
                    player2LocalKey = null;
                }
            });
        }
        
        /// <summary>
        /// ? v3.3.6: 从本地 Player2 应用获取 API Key
        /// </summary>
        private static async Task TryGetPlayer2LocalKey()
        {
            try
            {
                string loginUrl = $"{Player2LocalUrl}/login/web/{Player2GameClientId}";
                
                var request = (HttpWebRequest)WebRequest.Create(loginUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 3000;
                
                byte[] bodyRaw = Encoding.UTF8.GetBytes("{}");
                request.ContentLength = bodyRaw.Length;
                
                using (var stream = await request.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(bodyRaw, 0, bodyRaw.Length);
                }
                
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    string responseText = await reader.ReadToEndAsync();
                    
                    // ? 使用预编译的正则表达式
                    var match = Player2KeyRegex.Match(responseText);
                    if (match.Success)
                    {
                        player2LocalKey = match.Groups[1].Value;
                        Log.Message($"[AI] ? Got Player2 local key: {SanitizeApiKey(player2LocalKey)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AI] Failed to get Player2 local key: {ex.Message}");
                player2LocalKey = null;
            }
        }
    }
}
