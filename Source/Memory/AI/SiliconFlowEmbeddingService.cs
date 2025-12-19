// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net.Http;
// using System.Text;
// using System.Threading.Tasks;
// using Verse;

// namespace RimTalk.Memory.AI
// {
//     /// <summary>
//     /// SiliconFlow向量嵌入服务
//     /// ★ v3.3.20: 基于SiliconFlow API的语义向量检索
//     /// API文档: https://docs.siliconflow.cn/cn/api-reference/embeddings/create-embeddings
//     /// ? 注意：使用手动JSON构建避免外部依赖
//     /// </summary>
//     public static class SiliconFlowEmbeddingService
//     {
//         private static readonly HttpClient httpClient = new HttpClient();
//         private static bool isInitialized = false;
        
//         // 缓存向量（避免重复计算）
//         private static Dictionary<string, float[]> embeddingCache = new Dictionary<string, float[]>();
//         private const int MAX_CACHE_SIZE = 1000;
        
//         /// <summary>
//         /// 初始化服务
//         /// </summary>
//         public static void Initialize(string apiKey)
//         {
//             if (string.IsNullOrEmpty(apiKey))
//             {
//                 Log.Warning("[SiliconFlow] API Key is empty, embedding service disabled");
//                 isInitialized = false;
//                 return;
//             }
            
//             httpClient.DefaultRequestHeaders.Clear();
//             httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
//             httpClient.Timeout = TimeSpan.FromSeconds(30);
            
//             isInitialized = true;
//             Log.Message("[SiliconFlow] Embedding service initialized");
//         }
        
//         /// <summary>
//         /// 检查服务是否可用
//         /// </summary>
//         public static bool IsAvailable()
//         {
//             return isInitialized;
//         }
        
//         /// <summary>
//         /// 获取文本的向量表示
//         /// </summary>
//         public static async Task<float[]> GetEmbeddingAsync(string text, string model = "BAAI/bge-large-zh-v1.5")
//         {
//             if (!isInitialized)
//             {
//                 Log.Warning("[SiliconFlow] Service not initialized");
//                 return null;
//             }
            
//             if (string.IsNullOrEmpty(text))
//                 return null;
            
//             // 检查缓存
//             string cacheKey = $"{model}:{text}";
//             if (embeddingCache.TryGetValue(cacheKey, out float[] cached))
//             {
//                 return cached;
//             }
            
//             try
//             {
//                 // 手动构建JSON请求（避免Newtonsoft.Json依赖）
//                 string jsonRequest = $"{{\"model\":\"{model}\",\"input\":\"{EscapeJson(text)}\",\"encoding_format\":\"float\"}}";
//                 var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
//                 // 发送请求
//                 var response = await httpClient.PostAsync(
//                     "https://api.siliconflow.cn/v1/embeddings",
//                     content
//                 );
                
//                 if (!response.IsSuccessStatusCode)
//                 {
//                     string error = await response.Content.ReadAsStringAsync();
//                     Log.Error($"[SiliconFlow] API error: {response.StatusCode} - {error}");
//                     return null;
//                 }
                
//                 // 解析响应（简化版JSON解析）
//                 string jsonResponse = await response.Content.ReadAsStringAsync();
//                 float[] embedding = ParseEmbeddingFromJson(jsonResponse);
                
//                 if (embedding != null)
//                 {
//                     // 缓存结果
//                     if (embeddingCache.Count >= MAX_CACHE_SIZE)
//                     {
//                         // 清理最旧的一半缓存
//                         var keysToRemove = embeddingCache.Keys.Take(MAX_CACHE_SIZE / 2).ToList();
//                         foreach (var key in keysToRemove)
//                         {
//                             embeddingCache.Remove(key);
//                         }
//                     }
                    
//                     embeddingCache[cacheKey] = embedding;
//                     return embedding;
//                 }
                
//                 return null;
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SiliconFlow] GetEmbedding failed: {ex.Message}");
//                 return null;
//             }
//         }
        
//         /// <summary>
//         /// 批量获取向量（更高效）
//         /// ? v3.3.20: 完整实现批量JSON处理
//         /// </summary>
//         public static async Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts, string model = "BAAI/bge-large-zh-v1.5")
//         {
//             if (!isInitialized || texts == null || texts.Count == 0)
//                 return new List<float[]>();
            
//             try
//             {
//                 // 手动构建JSON数组请求
//                 var escapedTexts = texts.Select(t => $"\"{EscapeJson(t)}\"");
//                 string inputArray = "[" + string.Join(",", escapedTexts) + "]";
//                 string jsonRequest = $"{{\"model\":\"{model}\",\"input\":{inputArray},\"encoding_format\":\"float\"}}";
                
//                 var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
//                 // 发送请求
//                 var response = await httpClient.PostAsync(
//                     "https://api.siliconflow.cn/v1/embeddings",
//                     content
//                 );
                
//                 if (!response.IsSuccessStatusCode)
//                 {
//                     string error = await response.Content.ReadAsStringAsync();
//                     Log.Error($"[SiliconFlow] Batch API error: {response.StatusCode} - {error}");
//                     return new List<float[]>();
//                 }
                
//                 // 解析响应（批量版本）
//                 string jsonResponse = await response.Content.ReadAsStringAsync();
//                 var embeddings = ParseBatchEmbeddingsFromJson(jsonResponse);
                
//                 return embeddings;
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SiliconFlow] GetEmbeddingsBatch failed: {ex.Message}");
//                 return new List<float[]>();
//             }
//         }
        
//         /// <summary>
//         /// 计算余弦相似度
//         /// </summary>
//         public static float CosineSimilarity(float[] vec1, float[] vec2)
//         {
//             if (vec1 == null || vec2 == null || vec1.Length != vec2.Length)
//                 return 0f;
            
//             float dotProduct = 0f;
//             float magnitude1 = 0f;
//             float magnitude2 = 0f;
            
//             for (int i = 0; i < vec1.Length; i++)
//             {
//                 dotProduct += vec1[i] * vec2[i];
//                 magnitude1 += vec1[i] * vec1[i];
//                 magnitude2 += vec2[i] * vec2[i];
//             }
            
//             magnitude1 = (float)Math.Sqrt(magnitude1);
//             magnitude2 = (float)Math.Sqrt(magnitude2);
            
//             if (magnitude1 == 0f || magnitude2 == 0f)
//                 return 0f;
            
//             return dotProduct / (magnitude1 * magnitude2);
//         }
        
//         /// <summary>
//         /// 清除缓存
//         /// </summary>
//         public static void ClearCache()
//         {
//             embeddingCache.Clear();
//             Log.Message("[SiliconFlow] Embedding cache cleared");
//         }
        
//         /// <summary>
//         /// 获取缓存统计
//         /// </summary>
//         public static string GetCacheStats()
//         {
//             return $"Cache: {embeddingCache.Count}/{MAX_CACHE_SIZE} entries";
//         }
        
//         /// <summary>
//         /// ? v3.3.20: 保存缓存到文件
//         /// </summary>
//         public static void SaveCacheToFile(string filePath)
//         {
//             try
//             {
//                 if (embeddingCache.Count == 0)
//                 {
//                     Log.Message("[SiliconFlow] No cache to save");
//                     return;
//                 }
                
//                 var cacheData = new List<string>();
//                 foreach (var kvp in embeddingCache)
//                 {
//                     // 格式: key|vector1,vector2,vector3,...
//                     string vectorStr = string.Join(",", kvp.Value.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)));
//                     cacheData.Add($"{kvp.Key}|{vectorStr}");
//                 }
                
//                 System.IO.File.WriteAllLines(filePath, cacheData);
//                 Log.Message($"[SiliconFlow] Saved {embeddingCache.Count} cached embeddings to {filePath}");
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SiliconFlow] Failed to save cache: {ex.Message}");
//             }
//         }
        
//         /// <summary>
//         /// ? v3.3.20: 从文件加载缓存
//         /// </summary>
//         public static void LoadCacheFromFile(string filePath)
//         {
//             try
//             {
//                 if (!System.IO.File.Exists(filePath))
//                 {
//                     Log.Message("[SiliconFlow] No cache file found");
//                     return;
//                 }
                
//                 var lines = System.IO.File.ReadAllLines(filePath);
//                 int loaded = 0;
                
//                 foreach (var line in lines)
//                 {
//                     if (string.IsNullOrEmpty(line))
//                         continue;
                    
//                     int separatorIndex = line.IndexOf('|');
//                     if (separatorIndex < 0)
//                         continue;
                    
//                     string key = line.Substring(0, separatorIndex);
//                     string vectorStr = line.Substring(separatorIndex + 1);
                    
//                     var parts = vectorStr.Split(',');
//                     var vector = new List<float>();
                    
//                     foreach (var part in parts)
//                     {
//                         if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
//                             System.Globalization.CultureInfo.InvariantCulture, out float value))
//                         {
//                             vector.Add(value);
//                         }
//                     }
                    
//                     if (vector.Count > 0)
//                     {
//                         embeddingCache[key] = vector.ToArray();
//                         loaded++;
//                     }
                    
//                     // 限制加载数量
//                     if (loaded >= MAX_CACHE_SIZE)
//                         break;
//                 }
                
//                 Log.Message($"[SiliconFlow] Loaded {loaded} cached embeddings from {filePath}");
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SiliconFlow] Failed to load cache: {ex.Message}");
//             }
//         }
        
//         /// <summary>
//         /// ? v3.3.20: 获取缓存文件路径
//         /// </summary>
//         public static string GetCacheFilePath()
//         {
//             string configDir = GenFilePaths.ConfigFolderPath;
//             return System.IO.Path.Combine(configDir, "RimTalk_VectorCache.txt");
//         }
        
//         // ==================== 私有辅助方法 ====================
        
//         /// <summary>
//         /// 转义JSON字符串
//         /// </summary>
//         private static string EscapeJson(string text)
//         {
//             if (string.IsNullOrEmpty(text))
//                 return "";
            
//             return text
//                 .Replace("\\", "\\\\")
//                 .Replace("\"", "\\\"")
//                 .Replace("\n", "\\n")
//                 .Replace("\r", "\\r")
//                 .Replace("\t", "\\t");
//         }
        
//         /// <summary>
//         /// 从JSON响应中解析向量数组（简化版解析器）
//         /// </summary>
//         private static float[] ParseEmbeddingFromJson(string json)
//         {
//             try
//             {
//                 // 查找 "embedding":[ 开始位置
//                 int embeddingStart = json.IndexOf("\"embedding\":");
//                 if (embeddingStart < 0)
//                     return null;
                
//                 int arrayStart = json.IndexOf('[', embeddingStart);
//                 int arrayEnd = json.IndexOf(']', arrayStart);
                
//                 if (arrayStart < 0 || arrayEnd < 0)
//                     return null;
                
//                 // 提取数组内容
//                 string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                
//                 // 分割并解析
//                 var parts = arrayContent.Split(',');
//                 var result = new List<float>();
                
//                 foreach (var part in parts)
//                 {
//                     if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
//                         System.Globalization.CultureInfo.InvariantCulture, out float value))
//                     {
//                         result.Add(value);
//                     }
//                 }
                
//                 return result.Count > 0 ? result.ToArray() : null;
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SiliconFlow] Failed to parse embedding JSON: {ex.Message}");
//                 return null;
//             }
//         }
        
//         /// <summary>
//         /// ? v3.3.20: 从批量JSON响应中解析多个向量数组
//         /// </summary>
//         private static List<float[]> ParseBatchEmbeddingsFromJson(string json)
//         {
//             var results = new List<float[]>();
            
//             try
//             {
//                 // 查找 "data":[ 开始位置
//                 int dataStart = json.IndexOf("\"data\":");
//                 if (dataStart < 0)
//                     return results;
                
//                 int dataArrayStart = json.IndexOf('[', dataStart);
//                 if (dataArrayStart < 0)
//                     return results;
                
//                 // 逐个查找 "embedding":[ 块
//                 int searchPos = dataArrayStart;
//                 while (true)
//                 {
//                     int embeddingStart = json.IndexOf("\"embedding\":", searchPos);
//                     if (embeddingStart < 0)
//                         break;
                    
//                     int arrayStart = json.IndexOf('[', embeddingStart);
//                     int arrayEnd = json.IndexOf(']', arrayStart);
                    
//                     if (arrayStart < 0 || arrayEnd < 0)
//                         break;
                    
//                     // 提取数组内容
//                     string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                    
//                     // 分割并解析
//                     var parts = arrayContent.Split(',');
//                     var embedding = new List<float>();
                    
//                     foreach (var part in parts)
//                     {
//                         if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
//                             System.Globalization.CultureInfo.InvariantCulture, out float value))
//                         {
//                             embedding.Add(value);
//                         }
//                     }
                    
//                     if (embedding.Count > 0)
//                     {
//                         results.Add(embedding.ToArray());
//                     }
                    
//                     // 移动搜索位置到下一个可能的embedding
//                     searchPos = arrayEnd + 1;
//                 }
                
//                 return results;
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SiliconFlow] Failed to parse batch embeddings JSON: {ex.Message}");
//                 return results;
//             }
//         }
//     }
    
//     // ==================== API响应模型（已废弃，保留用于参考） ====================
    
//     /*
//     [Serializable]
//     public class EmbeddingResponse
//     {
//         public string @object;
//         public List<EmbeddingData> data;
//         public string model;
//         public Usage usage;
//     }
    
//     [Serializable]
//     public class EmbeddingData
//     {
//         public string @object;
//         public float[] embedding;
//         public int index;
//     }
    
//     [Serializable]
//     public class Usage
//     {
//         public int prompt_tokens;
//         public int total_tokens;
//     }
//     */
// }
