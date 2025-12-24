using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.AI
{
    /// <summary>
    /// 向量嵌入服务 - 支持Gemini和DeepSeek
    /// v3.1.0 实验性功能
    /// 
    /// 用途：
    /// - 语义相似度计算（比关键词匹配更准确）
    /// - 归档记忆摘要
    /// - 长期记忆检索
    /// 
    /// 成本估算：
    /// - DeepSeek: ?0.0002/1K tokens (~$0.00003)
    /// - Gemini: $0.00001/1K tokens
    /// 
    /// 使用建议：
    /// - 仅对重要记忆（importance > 0.7）使用
    /// - 缓存结果，避免重复计算
    /// - 月成本控制在 $0.01 以内
    /// </summary>
    public static class EmbeddingService
    {
        // 嵌入缓存（内存缓存，重启后清空）
        private static Dictionary<string, float[]> embeddingCache = new Dictionary<string, float[]>();
        private const int MAX_CACHE_SIZE = 500; // 最多缓存500个向量
        
        // 配置
        private static bool isInitialized = false;
        private static string apiKey, apiUrl, provider;
        private static int embeddingDimension = 1024; // DeepSeek: 1024, Gemini: 768
        
        /// <summary>
        /// 初始化Embedding服务
        /// ? v3.3.2.27: VectorDB已移除，始终返回未初始化状态
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            
            try
            {
                // ? v3.3.2.27: enableSemanticEmbedding已移除，始终不初始化
                Log.Message("[Embedding] v3.3.2.27: Semantic embedding功能已移除，使用SuperKeywordEngine替代");
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"[Embedding] Init failed: {ex.Message}");
                isInitialized = false;
            }
        }
        
        /// <summary>
        /// 检查服务是否可用
        /// ? v3.3.2.27: VectorDB已移除，始终返回false
        /// </summary>
        public static bool IsAvailable()
        {
            return false; // v3.3.2.27: 语义嵌入功能已移除
        }
        
        /// <summary>
        /// 获取文本的嵌入向量（带缓存）
        /// ? v3.3.2: 减少日志输出频率
        /// </summary>
        public static async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (!IsAvailable()) return null;
            
            if (string.IsNullOrEmpty(text))
                return null;
            
            // 生产缓存键
            string cacheKey = GenerateCacheKey(text);
            
            // 检查缓存
            lock (embeddingCache)
            {
                if (embeddingCache.TryGetValue(cacheKey, out float[] cachedVector))
                {
                    // ? v3.3.2: 只在DevMode下且随机1%概率输出，避免刷屏
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.01f)
                    {
                        Log.Message($"[Embedding] Cache hit ({embeddingCache.Count}/{MAX_CACHE_SIZE})");
                    }
                    return cachedVector;
                }
            }
            
            // ? v3.3.2: 降低API调用日志频率
            if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
            {
                Log.Message($"[Embedding] API call: {text.Substring(0, Math.Min(30, text.Length))}...");
            }
            
            // 调用API
            float[] embedding = await CallEmbeddingAPIAsync(text);
            
            if (embedding != null)
            {
                // 缓存结果
                lock (embeddingCache)
                {
                    // 限制缓存大小
                    if (embeddingCache.Count >= MAX_CACHE_SIZE)
                    {
                        // 移除最旧的50个
                        var toRemove = embeddingCache.Keys.Take(50).ToList();
                        foreach (var key in toRemove)
                        {
                            embeddingCache.Remove(key);
                        }
                        
                        // ? v3.3.2: 降低日志输出
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                            Log.Message($"[Embedding] Cache cleanup: {toRemove.Count} removed, {embeddingCache.Count} remain");
                    }
                    
                    embeddingCache[cacheKey] = embedding;
                }
            }
            
            return embedding;
        }
        
        /// <summary>
        /// 批量获取嵌入向量
        /// </summary>
        public static async Task<Dictionary<string, float[]>> GetEmbeddingsBatchAsync(List<string> texts)
        {
            var results = new Dictionary<string, float[]>();
            
            if (!IsAvailable() || texts == null || texts.Count == 0)
                return results;
            
            // 分批处理（每批最多20个）
            const int BATCH_SIZE = 20;
            
            for (int i = 0; i < texts.Count; i += BATCH_SIZE)
            {
                var batch = texts.Skip(i).Take(BATCH_SIZE).ToList();
                
                foreach (var text in batch)
                {
                    try
                    {
                        var embedding = await GetEmbeddingAsync(text);
                        if (embedding != null)
                        {
                            results[text] = embedding;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Embedding] Failed to get embedding for text: {ex.Message}");
                    }
                }
                
                // 避免频率限制
                if (i + BATCH_SIZE < texts.Count)
                {
                    await Task.Delay(100); // 延迟100ms
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// 计算余弦相似度
        /// </summary>
        public static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA == null || vectorB == null)
                return 0f;
            
            if (vectorA.Length != vectorB.Length)
            {
                Log.Error($"[Embedding] Vector dimension mismatch: {vectorA.Length} vs {vectorB.Length}");
                return 0f;
            }
            
            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;
            
            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }
            
            if (magnitudeA == 0f || magnitudeB == 0f)
                return 0f;
            
            return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
        
        /// <summary>
        /// 调用Embedding API
        /// </summary>
        private static async Task<float[]> CallEmbeddingAPIAsync(string text)
        {
            try
            {
                if (provider == "Google")
                {
                    return await CallGeminiEmbeddingAsync(text);
                }
                else // DeepSeek, OpenAI
                {
                    return await CallOpenAIStyleEmbeddingAsync(text);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Embedding] API call failed: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 调用OpenAI风格的Embedding API (DeepSeek, OpenAI)
        /// </summary>
        private static async Task<float[]> CallOpenAIStyleEmbeddingAsync(string text)
        {
            // ? 添加API Key验证
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("[Embedding] API Key is empty! Please configure it in Mod Settings.");
                return null;
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Embedding] Calling {provider} API...");
                Log.Message($"[Embedding] API URL: {apiUrl}");
                Log.Message($"[Embedding] API Key: {apiKey.Substring(0, Math.Min(10, apiKey.Length))}... (length: {apiKey.Length})");
            }
            
            var request = (HttpWebRequest)WebRequest.Create(apiUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Headers["Authorization"] = $"Bearer {apiKey}";
            request.Timeout = 10000; // 10秒超时
            
            // 构建请求体
            string model = provider == "DeepSeek" ? "deepseek-embedding" : "text-embedding-ada-002";
            string jsonRequest = BuildOpenAIEmbeddingRequest(text, model);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Embedding] Request body: {jsonRequest.Substring(0, Math.Min(200, jsonRequest.Length))}...");
            }
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
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
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Embedding] Response status: {response.StatusCode}");
                        Log.Message($"[Embedding] Response: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");
                    }
                    
                    return ParseOpenAIEmbeddingResponse(responseText);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var errorResponse = (HttpWebResponse)ex.Response)
                    using (var reader = new System.IO.StreamReader(errorResponse.GetResponseStream()))
                    {
                        string errorText = reader.ReadToEnd();
                        Log.Error($"[Embedding] API Error {errorResponse.StatusCode}: {errorText}");
                    }
                }
                throw;
            }
        }
        
        /// <summary>
        /// 调用Gemini Embedding API
        /// </summary>
        private static async Task<float[]> CallGeminiEmbeddingAsync(string text)
        {
            var request = (HttpWebRequest)WebRequest.Create(apiUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 10000;
            
            // Gemini请求格式
            string jsonRequest = BuildGeminiEmbeddingRequest(text);
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
            request.ContentLength = bodyRaw.Length;
            
            using (var stream = await request.GetRequestStreamAsync())
            {
                await stream.WriteAsync(bodyRaw, 0, bodyRaw.Length);
            }
            
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var streamReader = new System.IO.StreamReader(response.GetResponseStream()))
            {
                string responseText = await streamReader.ReadToEndAsync();
                return ParseGeminiEmbeddingResponse(responseText);
            }
        }
        
        /// <summary>
        /// 构建OpenAI风格的请求
        /// </summary>
        private static string BuildOpenAIEmbeddingRequest(string text, string model)
        {
            string escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
            
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{model}\",");
            sb.Append($"\"input\":\"{escapedText}\"");
            sb.Append("}");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 构建Gemini请求
        /// </summary>
        private static string BuildGeminiEmbeddingRequest(string text)
        {
            string escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
            
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"content\":{");
            sb.Append("\"parts\":[{");
            sb.Append($"\"text\":\"{escapedText}\"");
            sb.Append("}]");
            sb.Append("}");
            sb.Append("}");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 解析OpenAI风格的响应
        /// </summary>
        private static float[] ParseOpenAIEmbeddingResponse(string responseText)
        {
            try
            {
                // 简单的JSON解析（提取embedding数组）
                int embeddingStart = responseText.IndexOf("\"embedding\":");
                if (embeddingStart == -1)
                    return null;
                
                int arrayStart = responseText.IndexOf('[', embeddingStart);
                int arrayEnd = responseText.IndexOf(']', arrayStart);
                
                if (arrayStart == -1 || arrayEnd == -1)
                    return null;
                
                string arrayContent = responseText.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                var values = arrayContent.Split(',');
                
                float[] embedding = new float[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    if (!float.TryParse(values[i].Trim(), out embedding[i]))
                    {
                        Log.Error($"[Embedding] Failed to parse float at index {i}");
                        return null;
                    }
                }
                
                return embedding;
            }
            catch (Exception ex)
            {
                Log.Error($"[Embedding] Parse error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 解析Gemini响应
        /// </summary>
        private static float[] ParseGeminiEmbeddingResponse(string responseText)
        {
            try
            {
                // Gemini返回格式: {"embedding":{"values":[...]}}
                int valuesStart = responseText.IndexOf("\"values\":");
                if (valuesStart == -1)
                    return null;
                
                int arrayStart = responseText.IndexOf('[', valuesStart);
                int arrayEnd = responseText.IndexOf(']', arrayStart);
                
                if (arrayStart == -1 || arrayEnd == -1)
                    return null;
                
                string arrayContent = responseText.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                var values = arrayContent.Split(',');
                
                float[] embedding = new float[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    if (!float.TryParse(values[i].Trim(), out embedding[i]))
                    {
                        Log.Error($"[Embedding] Failed to parse float at index {i}");
                        return null;
                    }
                }
                
                return embedding;
            }
            catch (Exception ex)
            {
                Log.Error($"[Embedding] Parse error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 生成缓存键
        /// </summary>
        private static string GenerateCacheKey(string text)
        {
            // 使用MD5哈希（简化版）
            int hash = text.GetHashCode();
            return $"{provider}_{hash}";
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            lock (embeddingCache)
            {
                int count = embeddingCache.Count;
                embeddingCache.Clear();
                Log.Message($"[Embedding] Cleared {count} cached embeddings");
            }
        }
        
        /// <summary>
        /// 获取缓存统计
        /// </summary>
        public static EmbeddingCacheStats GetCacheStats()
        {
            lock (embeddingCache)
            {
                return new EmbeddingCacheStats
                {
                    CachedCount = embeddingCache.Count,
                    MaxCacheSize = MAX_CACHE_SIZE,
                    Provider = provider,
                    Dimension = embeddingDimension,
                    IsInitialized = isInitialized
                };
            }
        }
        
        /// <summary>
        /// 获取EmbeddingService实例（静态访问）
        /// </summary>
        public static EmbeddingServiceWrapper GetInstance()
        {
            // 返回包装器实例
            return new EmbeddingServiceWrapper();
        }
        
        /// <summary>
        /// 同步版本的GetEmbedding（用于向量库注入）
        /// </summary>
        public static float[] GetEmbedding(string text)
        {
            try
            {
                var task = GetEmbeddingAsync(text);
                task.Wait(5000); // 等待最多5秒
                return task.Result;
            }
            catch (Exception ex)
            {
                Log.Warning($"[Embedding] Sync GetEmbedding failed: {ex.Message}");
                return null;
            }
        }
    }
    
    /// <summary>
    /// EmbeddingService包装器，用于实例模式访问
    /// </summary>
    public class EmbeddingServiceWrapper
    {
        public float[] GetEmbedding(string text)
        {
            return EmbeddingService.GetEmbedding(text);
        }
        
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            return await EmbeddingService.GetEmbeddingAsync(text);
        }
    }
    
    /// <summary>
    /// Embedding缓存统计信息
    /// </summary>
    public class EmbeddingCacheStats
    {
        public int CachedCount;
        public int MaxCacheSize;
        public string Provider;
        public int Dimension;
        public bool IsInitialized;
    }
}
