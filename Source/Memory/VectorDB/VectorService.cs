using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Verse;
using RimTalk.Memory.VectorDB; // Updated using

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 向量检索引擎 - 单例模式
    /// 负责加载 ONNX 模型、文本向量化、相似度计算
    /// </summary>
    public class VectorService
    {
        private static VectorService _instance;
        private static readonly object _instanceLock = new object();
        private static readonly object _inferenceLock = new object();

    private InferenceSession _session;
    private bool _isInitialized = false;

    private Dictionary<string, float[]> _loreVectors = new Dictionary<string, float[]>();
    private Dictionary<string, int> _vocab = new Dictionary<string, int>();
    private int _unkId = 100;
    private int _clsId = 101;
    private int _sepId = 102;

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
                Log.Message("[RimTalk-ExpandMemory] VectorService: Initializing...");

                NativeLoader.Preload();

                string modelPath = GetVectorModelPath();
                
                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    Log.Error("[RimTalk-ExpandMemory] VectorService: Failed to locate model file.");
                    return;
                }

                Log.Message($"[RimTalk-ExpandMemory] VectorService: Loading model from: {modelPath}");

                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                _session = new InferenceSession(modelPath, sessionOptions);
                
                Log.Message("[RimTalk-ExpandMemory] VectorService: ONNX model loaded successfully!");

                // Load vocabulary
                LoadVocabulary();

                _isInitialized = true;
                Log.Message("[RimTalk-ExpandMemory] VectorService: Initialization complete!");

                // 自动预热
                Warmup();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Initialization failed: {ex}");
                _isInitialized = false;
            }
        }

        public List<(string id, float similarity)> FindBestLoreIds(string userMessage, int topK = 5, float threshold = 0.7f)
        {
            var results = new List<(string id, float similarity)>();
            
            try
            {
                if (!_isInitialized)
                {
                    Log.Warning("[RimTalk-ExpandMemory] VectorService: Service not initialized, returning empty.");
                    return results;
                }

                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    return results;
                }

                float[] queryVector = ComputeEmbedding(userMessage);

                var similarities = new List<(string id, float similarity)>();

                foreach (var kvp in _loreVectors)
                {
                    float similarity = CosineSimilarity(queryVector, kvp.Value);
                    if (similarity >= threshold)
                    {
                        similarities.Add((kvp.Key, similarity));
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
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error in FindBestLoreIds: {ex}");
                return results;
            }
        }
        
        public void SyncKnowledgeLibrary(CommonKnowledgeLibrary library)
        {
            try
            {
                if (!_isInitialized) return;
                if (library == null || library.Entries == null) return;

                Log.Message($"[RimTalk-ExpandMemory] VectorService: Syncing {library.Entries.Count} knowledge entries...");

                _loreVectors.Clear();
                int syncedCount = 0;

                foreach (var entry in library.Entries)
                {
                    if (entry == null || !entry.isEnabled) continue;
                    try
                    {
                        float[] vector = ComputeEmbedding(entry.content);
                        _loreVectors[entry.id] = vector;
                        syncedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandMemory] VectorService: Failed to vectorize entry {entry.id}: {ex.Message}");
                    }
                }

                Log.Message($"[RimTalk-ExpandMemory] VectorService: Sync complete! {syncedCount}/{library.Entries.Count} entries vectorized.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error syncing library: {ex}");
            }
        }
        
        public void UpdateKnowledgeVector(string id, string content)
        {
            try
            {
                if (!_isInitialized) return;
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(content)) return;

                float[] vector = ComputeEmbedding(content);
                _loreVectors[id] = vector;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error updating vector: {ex}");
            }
        }
        
        public void RemoveKnowledgeVector(string id)
        {
            try
            {
                if (_loreVectors.ContainsKey(id))
                {
                    _loreVectors.Remove(id);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error removing vector: {ex}");
            }
        }

        private float[] ComputeEmbedding(string text)
        {
            lock (_inferenceLock)
            {
                try
                {
                    // ⭐ 添加调试日志
                    Log.Message($"[RimTalk-ExpandMemory] [DEBUG] Computing embedding for: {text.Substring(0, Math.Min(50, text.Length))}");
                    
                    int[] inputIds = Tokenize(text);
                    
                    // ⭐ 调试：显示token数量
                    Log.Message($"[RimTalk-ExpandMemory] [DEBUG] Token count: {inputIds.Length}");
                    Log.Message($"[RimTalk-ExpandMemory] [DEBUG] First 10 tokens: {string.Join(", ", inputIds.Take(10))}");
                    
                    var inputIdsTensor = new DenseTensor<long>(new[] { 1, inputIds.Length });
                    var attentionMaskTensor = new DenseTensor<long>(new[] { 1, inputIds.Length });
                    var tokenTypeIdsTensor = new DenseTensor<long>(new[] { 1, inputIds.Length });

                    long[] inputIdsLong = new long[inputIds.Length];
                    for (int i = 0; i < inputIds.Length; i++)
                    {
                        inputIdsTensor[0, i] = inputIds[i];
                        attentionMaskTensor[0, i] = inputIds[i] == 0 ? 0 : 1; // ⭐ PAD位置mask=0
                        tokenTypeIdsTensor[0, i] = 0;
                        inputIdsLong[i] = inputIds[i];
                    }

                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                        NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                        NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
                    };

                    using (var results = _session.Run(inputs))
                    {
                        var output = results.First().AsEnumerable<float>().ToArray();
                        
                        // ⭐ 调试：显示原始输出
                        Log.Message($"[RimTalk-ExpandMemory] [DEBUG] Raw output length: {output.Length}");
                        
                        // ⭐ 关键改动：使用均值池化
                        float[] embedding = MeanPooling(output, inputIdsLong);
                        
                        // ⭐ 调试：归一化后的向量
                        Log.Message($"[RimTalk-ExpandMemory] [DEBUG] First 5 pooled & normalized values: {string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}");
                        
                        return embedding;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-ExpandMemory] VectorService: Error computing embedding: {ex}");
                    return new float[768];
                }
            }
        }

        private float[] MeanPooling(float[] outputData, long[] inputIds)
        {
            const int SEQ_LEN = 128;
            const int HIDDEN_DIM = 768; // text2vec-base-chinese 是 768 维
            
            float[] pooledVector = new float[HIDDEN_DIM];
            int validTokenCount = 0;

            // 遍历所有token位置
            for (int i = 0; i < SEQ_LEN; i++)
            {
                // 跳过 [PAD] token (id=0)
                if (inputIds[i] == 0) continue;
                
                validTokenCount++;
                
                // 累加每个维度的值
                for (int j = 0; j < HIDDEN_DIM; j++)
                {
                    int index = i * HIDDEN_DIM + j;
                    if (index < outputData.Length)
                    {
                        pooledVector[j] += outputData[index];
                    }
                }
            }

            // 求平均值
            if (validTokenCount > 0)
            {
                for (int j = 0; j < HIDDEN_DIM; j++)
                {
                    pooledVector[j] /= validTokenCount;
                }
            }
            
            // ⭐ 归一化（L2 normalization）
            return NormalizeVector(pooledVector);
        }

        private float[] NormalizeVector(float[] vector)
        {
            double norm = 0;
            foreach (var val in vector)
            {
                norm += val * val;
            }
            norm = Math.Sqrt(norm);
            
            if (norm > 0)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] = (float)(vector[i] / norm);
                }
            }
            
            return vector;
        }

        private void LoadVocabulary()
        {
            try
            {
                var currentMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.Name == "RimTalk - Expand Memory");
                if (currentMod == null)
                {
                    Log.Error("[RimTalk-ExpandMemory] VectorService: Could not find mod for vocab loading.");
                    return;
                }

                string vocabPath = Path.Combine(currentMod.RootDir, "1.6", "vocab.txt");
                
                if (!File.Exists(vocabPath))
                {
                    Log.Error($"[RimTalk-ExpandMemory] VectorService: vocab.txt not found at {vocabPath}");
                    return;
                }

                var lines = File.ReadAllLines(vocabPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                    {
                        _vocab[lines[i]] = i;
                    }
                }

                _unkId = _vocab.ContainsKey("[UNK]") ? _vocab["[UNK]"] : 100;
                _clsId = _vocab.ContainsKey("[CLS]") ? _vocab["[CLS]"] : 101;
                _sepId = _vocab.ContainsKey("[SEP]") ? _vocab["[SEP]"] : 102;

                Log.Message($"[RimTalk-ExpandMemory] VectorService: Loaded {_vocab.Count} vocabulary entries.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error loading vocabulary: {ex}");
            }
        }

        private int[] Tokenize(string text)
        {
            const int MAX_LENGTH = 128;
            const int PAD_TOKEN = 0;

            var tokens = new List<int> { _clsId };
            
            // Convert to lowercase (MiniLM is uncased)
            text = text.ToLowerInvariant();
            
            // ⭐ 修复：支持中文字符级分词
            for (int i = 0; i < text.Length && tokens.Count < MAX_LENGTH - 1; i++)
            {
                char c = text[i];
                
                // 检查是否为中文字符
                if (IsChinese(c))
                {
                    // 中文字符直接作为一个token
                    string charStr = c.ToString();
                    if (_vocab.ContainsKey(charStr))
                    {
                        tokens.Add(_vocab[charStr]);
                    }
                    else
                    {
                        tokens.Add(_unkId);
                    }
                }
                else if (char.IsWhiteSpace(c))
                {
                    // 跳过空白字符
                    continue;
                }
                else
                {
                    // 英文/数字：收集完整单词
                    int start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsChinese(text[i]))
                    {
                        i++;
                    }
                    i--; // 回退一个字符
                    
                    string word = text.Substring(start, i - start + 1);
                    var subTokens = WordPieceTokenize(word);
                    foreach (var token in subTokens)
                    {
                        if (tokens.Count >= MAX_LENGTH - 1) break;
                        tokens.Add(token);
                    }
                }
            }
            
            tokens.Add(_sepId);

            // Padding
            while (tokens.Count < MAX_LENGTH)
            {
                tokens.Add(PAD_TOKEN);
            }
            
            return tokens.Take(MAX_LENGTH).ToArray();
        }
        
        /// <summary>
        /// 检查字符是否为中文字符
        /// </summary>
        private bool IsChinese(char c)
        {
            // Unicode范围：中日韩统一表意文字
            return (c >= 0x4E00 && c <= 0x9FFF) ||  // CJK Unified Ideographs
                   (c >= 0x3400 && c <= 0x4DBF) ||  // CJK Extension A
                   (c >= 0x20000 && c <= 0x2A6DF);  // CJK Extension B
        }

        private List<int> WordPieceTokenize(string word)
        {
            var result = new List<int>();
            
            // If word is directly in vocab
            if (_vocab.ContainsKey(word))
            {
                result.Add(_vocab[word]);
                return result;
            }

            // WordPiece algorithm: try to split into subwords
            int start = 0;
            bool isBad = false;

            while (start < word.Length)
            {
                int end = word.Length;
                int curSubTokenId = -1;

                while (start < end)
                {
                    string subStr = word.Substring(start, end - start);
                    
                    // Add ## prefix for non-first subwords
                    if (start > 0)
                    {
                        subStr = "##" + subStr;
                    }

                    if (_vocab.ContainsKey(subStr))
                    {
                        curSubTokenId = _vocab[subStr];
                        break;
                    }
                    
                    end--;
                }

                if (curSubTokenId == -1)
                {
                    isBad = true;
                    break;
                }

                result.Add(curSubTokenId);
                start = end;
            }

            if (isBad)
            {
                result.Clear();
                result.Add(_unkId);
            }

            return result;
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

        private static void Normalize(float[] vector)
        {
            float norm = 0f;
            for (int i = 0; i < vector.Length; i++) norm += vector[i] * vector[i];
            norm = (float)Math.Sqrt(norm);
            if (norm > 0f)
            {
                for (int i = 0; i < vector.Length; i++) vector[i] /= norm;
            }
        }

        private static string GetVectorModelPath()
        {
            try
            {
                var currentMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.Name == "RimTalk - Expand Memory");
                if (currentMod == null)
                {
                    Log.Error("[RimTalk-ExpandMemory] VectorService: Could not find self in mod list.");
                    return null;
                }

                string modelPath = Path.Combine(
                    currentMod.RootDir,
                    "1.6",
                    "Resources",
                    "text2vec-base-chinese.onnx"
                );
                return modelPath;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] VectorService: Error locating model path: {ex}");
                return null;
            }
        }
        
        private void Warmup()
        {
            Task.Run(() =>
            {
                try
                {
                    // 随便用一个空字符跑一次，强迫模型加载进内存，并完成 JIT 编译
                    ComputeEmbedding("warmup");
                    Log.Message("[RimTalk-ExpandMemory] VectorService: Warmup complete.");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] VectorService: Warmup failed: {ex}");
                }
            });
        }

        public void Dispose()
        {
            try
            {
                _session?.Dispose();
                _session = null;
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
