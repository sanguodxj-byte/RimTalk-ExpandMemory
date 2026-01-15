using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimTalk.MemoryPatch;
using Verse;
using RimWorld;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 向量检索引擎 - 云端版
    /// 负责调用云端 Embedding API、文本向量化、相似度计算
    /// </summary>
    public class VectorService
    {
        private static VectorService _instance;
        private static readonly object _instanceLock = new object();
        
        private Dictionary<string, float[]> _loreVectors = new Dictionary<string, float[]>();
        private Dictionary<string, string> _contentHashes = new Dictionary<string, string>(); // 内容哈希值缓存
        private HttpClient _httpClient;
        private bool _isInitialized = false;
        private bool _isSyncing = false; // 是否正在同步

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static VectorService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VectorService();
                        }
                    }
                }
                return _instance;
            }
        }

        private VectorService()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                Log.Message("[RimTalk-ExpandMemory] VectorService: Initializing Cloud Embedding Service...");
                _httpClient = new HttpClient();
                _httpClient.Timeout = TimeSpan.FromSeconds(30);
                _isInitialized = true;
                Log.Message("[RimTalk-ExpandMemory] VectorService: Cloud Service Initialized.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Initialization failed: {ex}");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 异步查找最佳匹配的知识条目
        /// </summary>
        public async Task<List<(string id, float similarity)>> FindBestLoreIdsAsync(string userMessage, int topK = 5, float threshold = 0.7f)
        {
            var results = new List<(string id, float similarity)>();
            
            try
            {
                if (!_isInitialized)
                {
                    Log.Warning("[RimTalk-ExpandMemory] VectorService: Service not initialized.");
                    return results;
                }

                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    return results;
                }

                // 异步获取查询向量
                float[] queryVector = await GetEmbeddingAsync(userMessage).ConfigureAwait(false);
                if (queryVector == null || queryVector.Length == 0)
                {
                    return results;
                }

                var similarities = new List<(string id, float similarity)>();

                lock (_loreVectors)
                {
                    foreach (var kvp in _loreVectors)
                    {
                        float similarity = CosineSimilarity(queryVector, kvp.Value);
                        if (similarity >= threshold)
                        {
                            similarities.Add((kvp.Key, similarity));
                        }
                    }
                }

                results = similarities
                    .OrderByDescending(s => s.similarity)
                    .Take(topK)
                    .ToList();

                return results;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error in FindBestLoreIdsAsync: {ex}");
                return results;
            }
        }

        /// <summary>
        /// 同步版本（已废弃，仅用于向后兼容）
        /// 注意：此方法会阻塞调用线程，建议使用 FindBestLoreIdsAsync
        /// </summary>
        [Obsolete("Use FindBestLoreIdsAsync instead to avoid blocking")]
        public List<(string id, float similarity)> FindBestLoreIds(string userMessage, int topK = 5, float threshold = 0.7f)
        {
            return FindBestLoreIdsAsync(userMessage, topK, threshold).GetAwaiter().GetResult();
        }
        
        public void SyncKnowledgeLibrary(CommonKnowledgeLibrary library)
        {
            // 在后台任务中运行，避免阻塞主线程
            Task.Run(async () => 
            {
                try
                {
                    if (!_isInitialized) return;
                    if (library == null || library.Entries == null) return;
                    if (_isSyncing) return; // 防止重复同步

                    _isSyncing = true;

                    var entriesToProcess = library.Entries.Where(e => e != null && e.isEnabled && !string.IsNullOrWhiteSpace(e.content)).ToList();
                    
                    // 检测需要更新的条目
                    var entriesToUpdate = new List<CommonKnowledgeEntry>();
                    var entriesToRemove = new List<string>();
                    
                    lock (_loreVectors)
                    {
                        // 检查哪些条目需要更新
                        foreach (var entry in entriesToProcess)
                        {
                            string currentHash = ComputeHash(entry.content);
                            
                            if (!_contentHashes.ContainsKey(entry.id) || _contentHashes[entry.id] != currentHash)
                            {
                                entriesToUpdate.Add(entry);
                            }
                        }
                        
                        // 检查哪些条目需要删除（已不存在或被禁用）
                        var currentIds = new HashSet<string>(entriesToProcess.Select(e => e.id));
                        foreach (var id in _loreVectors.Keys.ToList())
                        {
                            if (!currentIds.Contains(id))
                            {
                                entriesToRemove.Add(id);
                            }
                        }
                    }

                    if (entriesToUpdate.Count == 0 && entriesToRemove.Count == 0)
                    {
                        Log.Message("[RimTalk-ExpandMemory] VectorService: No changes detected, skipping sync.");
                        _isSyncing = false;
                        return;
                    }

                    // 显示开始同步的消息
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        Messages.Message($"正在更新向量库... ({entriesToUpdate.Count} 条新增/修改, {entriesToRemove.Count} 条删除)", MessageTypeDefOf.NeutralEvent, false);
                    });

                    Log.Message($"[RimTalk-ExpandMemory] VectorService: Syncing {entriesToUpdate.Count} updated entries, removing {entriesToRemove.Count} entries...");

                    // 删除过期条目
                    lock (_loreVectors)
                    {
                        foreach (var id in entriesToRemove)
                        {
                            _loreVectors.Remove(id);
                            _contentHashes.Remove(id);
                        }
                    }

                    // 批量处理更新
                    int batchSize = 10;
                    int syncedCount = 0;

                    for (int i = 0; i < entriesToUpdate.Count; i += batchSize)
                    {
                        var batch = entriesToUpdate.Skip(i).Take(batchSize).ToList();
                        var texts = batch.Select(e => e.content).ToList();
                        
                        var embeddings = await GetEmbeddingsAsync(texts).ConfigureAwait(false);
                        
                        if (embeddings != null && embeddings.Count == batch.Count)
                        {
                            lock (_loreVectors)
                            {
                                for (int j = 0; j < batch.Count; j++)
                                {
                                    _loreVectors[batch[j].id] = embeddings[j];
                                    _contentHashes[batch[j].id] = ComputeHash(batch[j].content);
                                }
                            }
                            syncedCount += batch.Count;
                        }
                        
                        // 避免触发速率限制
                        await Task.Delay(200).ConfigureAwait(false);
                    }

                    Log.Message($"[RimTalk-ExpandMemory] VectorService: Sync complete! {syncedCount}/{entriesToUpdate.Count} entries vectorized.");
                    
                    // 显示完成消息
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        Messages.Message($"向量库已更新 ({syncedCount} 条)", MessageTypeDefOf.PositiveEvent, false);
                    });
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-ExpandMemory] VectorService: Error syncing library: {ex}");
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        Messages.Message($"向量库更新失败: {ex.Message}", MessageTypeDefOf.RejectInput, false);
                    });
                }
                finally
                {
                    _isSyncing = false;
                }
            });
        }
        
        public void UpdateKnowledgeVector(string id, string content)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (!_isInitialized) return;
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(content)) return;

                    // 检查是否需要更新
                    string currentHash = ComputeHash(content);
                    lock (_loreVectors)
                    {
                        if (_contentHashes.ContainsKey(id) && _contentHashes[id] == currentHash)
                        {
                            return; // 内容未变化，跳过
                        }
                    }

                    float[] vector = await GetEmbeddingAsync(content).ConfigureAwait(false);
                    if (vector != null)
                    {
                        lock (_loreVectors)
                        {
                            _loreVectors[id] = vector;
                            _contentHashes[id] = currentHash;
                        }
                        Log.Message($"[RimTalk-ExpandMemory] VectorService: Updated vector for entry {id}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-ExpandMemory] VectorService: Error updating vector: {ex}");
                }
            });
        }
        
        public void RemoveKnowledgeVector(string id)
        {
            try
            {
                lock (_loreVectors)
                {
                    if (_loreVectors.ContainsKey(id))
                    {
                        _loreVectors.Remove(id);
                        _contentHashes.Remove(id);
                        Log.Message($"[RimTalk-ExpandMemory] VectorService: Removed vector for entry {id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error removing vector: {ex}");
            }
        }

        /// <summary>
        /// 导出向量数据用于存档保存
        /// </summary>
        public void ExportVectorsForSave(
            out List<string> ids, 
            out List<List<float>> vectors, 
            out List<string> hashes)
        {
            lock (_loreVectors)
            {
                ids = new List<string>(_loreVectors.Keys);
                vectors = new List<List<float>>();
                hashes = new List<string>();
                
                foreach (var id in ids)
                {
                    vectors.Add(_loreVectors[id].ToList());
                    hashes.Add(_contentHashes.ContainsKey(id) ? _contentHashes[id] : "");
                }
                
                Log.Message($"[RimTalk-ExpandMemory] VectorService: Exported {ids.Count} vectors for save");
            }
        }

        /// <summary>
        /// 从存档载入向量数据
        /// </summary>
        public void ImportVectorsFromLoad(
            List<string> ids, 
            List<List<float>> vectors, 
            List<string> hashes)
        {
            if (ids == null || vectors == null || hashes == null)
            {
                Log.Warning("[RimTalk-ExpandMemory] VectorService: Cannot import null vector data");
                return;
            }
                
            lock (_loreVectors)
            {
                _loreVectors.Clear();
                _contentHashes.Clear();
                
                for (int i = 0; i < ids.Count && i < vectors.Count; i++)
                {
                    if (vectors[i] != null)
                    {
                        _loreVectors[ids[i]] = vectors[i].ToArray();
                        if (i < hashes.Count && !string.IsNullOrEmpty(hashes[i]))
                            _contentHashes[ids[i]] = hashes[i];
                    }
                }
                
                Log.Message($"[RimTalk-ExpandMemory] VectorService: Imported {_loreVectors.Count} vectors from save");
            }
        }

        /// <summary>
        /// 计算内容的哈希值
        /// </summary>
        private static string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;
            
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private float[] GetEmbedding(string text)
        {
            return GetEmbeddingAsync(text).GetAwaiter().GetResult();
        }

        private async Task<float[]> GetEmbeddingAsync(string text)
        {
            var results = await GetEmbeddingsAsync(new List<string> { text }).ConfigureAwait(false);
            return results?.FirstOrDefault();
        }

        private async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // 优先使用专用的 Embedding API Key，如果为空则使用通用 API Key
                string apiKey = string.IsNullOrEmpty(settings.embeddingApiKey) 
                    ? settings.independentApiKey 
                    : settings.embeddingApiKey;
                    
                string apiUrl = settings.embeddingApiUrl;
                string model = settings.embeddingModel;

                if (string.IsNullOrEmpty(apiKey))
                {
                    Log.Warning("[RimTalk-ExpandMemory] VectorService: API Key is missing. Please configure either Embedding API Key or Independent API Key.");
                    return null;
                }

                // 构建请求体
                var requestBody = new
                {
                    input = texts,
                    model = model
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                
                // 详细日志：记录请求信息
                Log.Message($"[RimTalk-ExpandMemory] VectorService: Sending request to {apiUrl}");
                Log.Message($"[RimTalk-ExpandMemory] VectorService: Model: {model}");
                Log.Message($"[RimTalk-ExpandMemory] VectorService: Input count: {texts.Count}");
                Log.Message($"[RimTalk-ExpandMemory] VectorService: Request body: {jsonBody}");

                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl))
                {
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                    
                    // 详细日志：记录响应状态
                    Log.Message($"[RimTalk-ExpandMemory] VectorService: Response status: {(int)response.StatusCode} {response.StatusCode}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Log.Error($"[RimTalk-ExpandMemory] VectorService: API request failed: {(int)response.StatusCode} ({response.StatusCode})");
                        Log.Error($"[RimTalk-ExpandMemory] VectorService: Error response: {errorBody}");
                        return null;
                    }

                    string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Message($"[RimTalk-ExpandMemory] VectorService: Response received, length: {responseString.Length}");
                    
                    JObject jsonResponse = JObject.Parse(responseString);
                    
                    var dataArray = jsonResponse["data"] as JArray;
                    if (dataArray == null)
                    {
                        Log.Error($"[RimTalk-ExpandMemory] VectorService: No 'data' field in response: {responseString}");
                        return null;
                    }

                    var embeddings = new List<float[]>();
                    foreach (var item in dataArray)
                    {
                        var embeddingArray = item["embedding"]?.ToObject<float[]>();
                        if (embeddingArray != null)
                        {
                            embeddings.Add(embeddingArray);
                        }
                    }

                    Log.Message($"[RimTalk-ExpandMemory] VectorService: Successfully parsed {embeddings.Count} embeddings");
                    return embeddings;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: HTTP request failed: {ex.Message}");
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Stack trace: {ex.StackTrace}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Unexpected error: {ex.Message}");
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private static float CosineSimilarity(float[] vec1, float[] vec2)
        {
            if (vec1.Length != vec2.Length) return 0f;
            float dotProduct = 0f, norm1 = 0f, norm2 = 0f;
            for (int i = 0; i < vec1.Length; i++)
            {
                dotProduct += vec1[i] * vec2[i];
                norm1 += vec1[i] * vec1[i];
                norm2 += vec2[i] * vec2[i];
            }
            if (norm1 == 0f || norm2 == 0f) return 0f;
            return dotProduct / (float)(Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        public void Dispose()
        {
            try
            {
                _httpClient?.Dispose();
                _httpClient = null;
                _isInitialized = false;
                Log.Message("[RimTalk-ExpandMemory] VectorService: Disposed.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error during disposal: {ex}");
            }
        }
    }
}
