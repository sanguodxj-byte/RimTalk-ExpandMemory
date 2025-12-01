using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimTalk.Memory.VectorDB;

namespace RimTalk.Memory.RAG
{
    /// <summary>
    /// RAG检索器 - 增强生成检索系统
    /// v3.3.0 核心功能
    /// 
    /// RAG工作流程:
    /// 1. 接收用户查询
    /// 2. 向量检索（语义相似度）
    /// 3. 重排序（上下文相关性）
    /// 4. 生成增强上下文
    /// 5. 返回格式化结果
    /// 
    /// 优势:
    /// - 语义理解查询意图
    /// - 多源信息融合（记忆+常识）
    /// - 智能上下文构建
    /// - 自适应结果数量
    /// </summary>
    public static class RAGRetriever
    {
        // 配置
        private const int DEFAULT_TOP_K = 10;
        private const float DEFAULT_MIN_SIMILARITY = 0.5f;
        private const int MAX_CONTEXT_LENGTH = 2000; // 最大上下文长度（字符）
        
        /// <summary>
        /// RAG检索 - 主入口
        /// </summary>
        public static async Task<RAGResult> RetrieveAsync(
            string query,
            Pawn speaker = null,
            Pawn listener = null,
            RAGConfig config = null)
        {
            if (string.IsNullOrEmpty(query))
                return RAGResult.Empty();
            
            config = config ?? new RAGConfig();
            
            try
            {
                var result = new RAGResult
                {
                    Query = query,
                    Speaker = speaker?.LabelShort,
                    Listener = listener?.LabelShort
                };
                
                // 阶段1: 向量检索
                var vectorResults = await VectorRetrievalAsync(query, config);
                result.VectorMatches = vectorResults;
                
                // 阶段2: 混合检索（关键词 + 向量）
                var hybridResults = await HybridRetrievalAsync(query, speaker, listener, config);
                result.HybridMatches = hybridResults;
                
                // 阶段3: 重排序
                var reranked = RerankResults(query, hybridResults, speaker, listener);
                result.RerankedMatches = reranked.Take(config.MaxResults).ToList();
                
                // 阶段4: 生成上下文
                result.GeneratedContext = GenerateContext(result.RerankedMatches, config);
                
                // 阶段5: 元数据
                result.RetrievalTime = DateTime.Now;
                result.TotalMatches = hybridResults.Count;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[RAG] Query: '{query}' → {result.RerankedMatches.Count} results");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[RAG] Retrieval failed: {ex.Message}");
                return RAGResult.Empty();
            }
        }
        
        /// <summary>
        /// 阶段1: 向量检索（语义相似度）
        /// ? v3.3.2.4: 同时搜索记忆和常识向量
        /// </summary>
        private static async Task<List<RAGMatch>> VectorRetrievalAsync(string query, RAGConfig config)
        {
            var matches = new List<RAGMatch>();
            
            // 检查向量数据库是否可用
            if (!VectorDBManager.IsAvailable())
                return matches;
            
            try
            {
                // ? 1. 搜索记忆向量
                var memoryIds = await VectorDBManager.SemanticSearchAsync(
                    query,
                    topK: config.TopK,
                    minSimilarity: config.MinSimilarity
                );
                
                // 获取记忆详情
                foreach (var memoryId in memoryIds)
                {
                    var memory = FindMemoryById(memoryId);
                    if (memory != null)
                    {
                        matches.Add(new RAGMatch
                        {
                            Source = RAGMatchSource.VectorDB,
                            Memory = memory,
                            Score = 0.9f, // 向量检索高置信度
                            MatchType = "Semantic"
                        });
                    }
                }
                
                // ? 2. 搜索常识向量（新增）
                var knowledgeIds = await VectorDBManager.SearchKnowledgeAsync(
                    query,
                    topK: config.TopK / 2,  // 常识数量减半
                    minSimilarity: 0.2f      // ? 降低阈值到0.3
                );
                
                // 获取常识详情
                var library = MemoryManager.GetCommonKnowledge();
                if (library != null && knowledgeIds != null)
                {
                    foreach (var knowledgeId in knowledgeIds)
                    {
                        var knowledge = library.Entries.FirstOrDefault(e => e.id == knowledgeId);
                        if (knowledge != null)
                        {
                            matches.Add(new RAGMatch
                            {
                                Source = RAGMatchSource.VectorDB,
                                Knowledge = knowledge,
                                Score = 0.9f,  // 向量检索高置信度
                                MatchType = "Semantic"
                            });
                        }
                    }
                }
                
                if (Prefs.DevMode)
                    Log.Message($"[RAG] Vector retrieval: {matches.Count} matches ({memoryIds.Count} memories + {knowledgeIds?.Count ?? 0} knowledge)");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RAG] Vector retrieval failed: {ex.Message}");
            }
            
            return matches;
        }
        
        /// <summary>
        /// 阶段2: 混合检索（关键词 + 向量）
        /// </summary>
        private static async Task<List<RAGMatch>> HybridRetrievalAsync(
            string query,
            Pawn speaker,
            Pawn listener,
            RAGConfig config)
        {
            var matches = new List<RAGMatch>();
            
            // 2.1 记忆检索
            var memoryMatches = await RetrieveMemoriesAsync(query, speaker, config);
            matches.AddRange(memoryMatches);
            
            // 2.2 常识检索
            var knowledgeMatches = await RetrieveKnowledgeAsync(query, speaker, listener, config);
            matches.AddRange(knowledgeMatches);
            
            // 去重（优先保留高分）
            matches = matches
                .GroupBy(m => m.Memory?.id ?? m.Knowledge?.id)
                .Select(g => g.OrderByDescending(m => m.Score).First())
                .ToList();
            
            if (Prefs.DevMode)
                Log.Message($"[RAG] Hybrid retrieval: {matches.Count} matches");
            
            return matches;
        }
        
        /// <summary>
        /// 检索记忆
        /// </summary>
        private static async Task<List<RAGMatch>> RetrieveMemoriesAsync(
            string query,
            Pawn speaker,
            RAGConfig config)
        {
            var matches = new List<RAGMatch>();
            
            if (speaker == null)
                return matches;
            
            try
            {
                var memoryComp = speaker.TryGetComp<FourLayerMemoryComp>();
                if (memoryComp == null)
                    return matches;
                
                // 收集所有记忆
                var allMemories = new List<MemoryEntry>();
                allMemories.AddRange(memoryComp.SituationalMemories);
                allMemories.AddRange(memoryComp.EventLogMemories);
                
                if (config.IncludeArchive)
                {
                    allMemories.AddRange(memoryComp.ArchiveMemories.Take(50));
                }
                
                // 使用高级评分系统
                List<ScoredItem<MemoryEntry>> scored;
                
                if (config.UseSemanticScoring && AI.EmbeddingService.IsAvailable())
                {
                    // 语义评分
                    scored = await Task.Run(() => 
                        SemanticScoringSystem.ScoreMemoriesWithSemantics(
                            allMemories, query, speaker, null
                        )
                    );
                }
                else
                {
                    // 关键词评分
                    scored = AdvancedScoringSystem.ScoreMemories(
                        allMemories, query, speaker, null
                    );
                }
                
                // 转换为RAGMatch
                foreach (var item in scored.Take(config.TopK))
                {
                    matches.Add(new RAGMatch
                    {
                        Source = RAGMatchSource.Memory,
                        Memory = item.Item,
                        Score = item.Score,
                        MatchType = config.UseSemanticScoring ? "Semantic+Keyword" : "Keyword",
                        Breakdown = item.Breakdown
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RAG] Memory retrieval failed: {ex.Message}");
            }
            
            return matches;
        }
        
        /// <summary>
        /// 检索常识
        /// </summary>
        private static async Task<List<RAGMatch>> RetrieveKnowledgeAsync(
            string query,
            Pawn speaker,
            Pawn listener,
            RAGConfig config)
        {
            var matches = new List<RAGMatch>();
            
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return matches;
                
                var knowledge = library.Entries
                    .Where(e => e.isEnabled)
                    .ToList();
                
                // 使用高级评分系统
                List<ScoredItem<CommonKnowledgeEntry>> scored;
                
                if (config.UseSemanticScoring && AI.EmbeddingService.IsAvailable())
                {
                    // 语义评分
                    scored = await Task.Run(() => 
                        SemanticScoringSystem.ScoreKnowledgeWithSemantics(
                            knowledge, query, speaker, listener
                        )
                    );
                }
                else
                {
                    // 关键词评分
                    scored = AdvancedScoringSystem.ScoreKnowledge(
                        knowledge, query, speaker, listener
                    );
                }
                
                // 转换为RAGMatch
                foreach (var item in scored.Take(config.TopK / 2)) // 常识数量减半
                {
                    matches.Add(new RAGMatch
                    {
                        Source = RAGMatchSource.Knowledge,
                        Knowledge = item.Item,
                        Score = item.Score,
                        MatchType = config.UseSemanticScoring ? "Semantic+Keyword" : "Keyword"
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RAG] Knowledge retrieval failed: {ex.Message}");
            }
            
            return matches;
        }
        
        /// <summary>
        /// 阶段3: 重排序（融合多种信号）
        /// </summary>
        private static List<RAGMatch> RerankResults(
            string query,
            List<RAGMatch> matches,
            Pawn speaker,
            Pawn listener)
        {
            if (matches == null || matches.Count == 0)
                return new List<RAGMatch>();
            
            try
            {
                // 重排序策略：
                // 1. 原始分数（40%）
                // 2. 时效性（20%）
                // 3. 重要性（20%）
                // 4. 来源权重（20%）
                
                foreach (var match in matches)
                {
                    float finalScore = 0f;
                    
                    // 1. 原始分数
                    finalScore += match.Score * 0.4f;
                    
                    // 2. 时效性
                    if (match.Memory != null)
                    {
                        int age = Find.TickManager.TicksGame - match.Memory.timestamp;
                        float recency = UnityEngine.Mathf.Exp(-age / 60000f); // 24小时半衰期
                        finalScore += recency * 0.2f;
                    }
                    else
                    {
                        finalScore += 0.1f; // 常识无时间概念
                    }
                    
                    // 3. 重要性
                    float importance = match.Memory?.importance ?? match.Knowledge?.importance ?? 0.5f;
                    finalScore += importance * 0.2f;
                    
                    // 4. 来源权重
                    float sourceWeight = GetSourceWeight(match.Source, match.Memory?.layer);
                    finalScore += sourceWeight * 0.2f;
                    
                    match.FinalScore = finalScore;
                }
                
                // 按最终分数排序
                return matches.OrderByDescending(m => m.FinalScore).ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[RAG] Reranking failed: {ex.Message}");
                return matches;
            }
        }
        
        /// <summary>
        /// 获取来源权重
        /// </summary>
        private static float GetSourceWeight(RAGMatchSource source, MemoryLayer? layer)
        {
            if (source == RAGMatchSource.VectorDB)
                return 1.0f; // 向量DB最高置信度
            
            if (source == RAGMatchSource.Memory && layer.HasValue)
            {
                switch (layer.Value)
                {
                    case MemoryLayer.Situational: return 0.9f;
                    case MemoryLayer.EventLog: return 0.7f;
                    case MemoryLayer.Archive: return 0.5f;
                    default: return 0.3f;
                }
            }
            
            if (source == RAGMatchSource.Knowledge)
                return 0.8f;
            
            return 0.5f;
        }
        
        /// <summary>
        /// 阶段4: 生成上下文
        /// </summary>
        private static string GenerateContext(List<RAGMatch> matches, RAGConfig config)
        {
            if (matches == null || matches.Count == 0)
                return null;
            
            var sb = new StringBuilder();
            int currentLength = 0;
            int index = 1;
            
            foreach (var match in matches)
            {
                string entry = FormatMatch(match, index, config.IncludeScores);
                
                // 检查长度限制
                if (currentLength + entry.Length > config.MaxContextLength)
                    break;
                
                sb.AppendLine(entry);
                currentLength += entry.Length;
                index++;
            }
            
            return sb.Length > 0 ? sb.ToString() : null;
        }
        
        /// <summary>
        /// 格式化匹配项
        /// </summary>
        private static string FormatMatch(RAGMatch match, int index, bool includeScores)
        {
            var sb = new StringBuilder();
            
            if (match.Memory != null)
            {
                // 记忆格式
                string typeTag = match.Memory.type.ToString();
                string timeAgo = match.Memory.TimeAgoString;
                
                sb.Append($"{index}. [{typeTag}] {match.Memory.content}");
                
                if (includeScores)
                {
                    sb.Append($" (score: {match.FinalScore:F2}, {timeAgo})");
                }
                else
                {
                    sb.Append($" ({timeAgo})");
                }
            }
            else if (match.Knowledge != null)
            {
                // 常识格式
                sb.Append($"{index}. [{match.Knowledge.tag}] {match.Knowledge.content}");
                
                if (includeScores)
                {
                    sb.Append($" (score: {match.FinalScore:F2})");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 根据ID查找记忆
        /// </summary>
        private static MemoryEntry FindMemoryById(string memoryId)
        {
            try
            {
                // 遍历所有地图和Pawn查找
                foreach (var map in Find.Maps)
                {
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
                        if (memoryComp != null)
                        {
                            var memory = memoryComp.GetAllMemories()
                                .FirstOrDefault(m => m.id == memoryId);
                            
                            if (memory != null)
                                return memory;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RAG] FindMemoryById failed: {ex.Message}");
            }
            
            return null;
        }
    }
    
    #region 数据结构
    
    /// <summary>
    /// RAG配置
    /// </summary>
    public class RAGConfig
    {
        public int TopK = 10;                          // 每个来源的Top K
        public int MaxResults = 15;                    // 最终结果数量
        public float MinSimilarity = 0.5f;             // 最小相似度
        public int MaxContextLength = 2000;            // 最大上下文长度
        public bool IncludeArchive = false;            // 包含归档记忆
        public bool UseSemanticScoring = true;         // 使用语义评分
        public bool IncludeScores = false;             // 上下文中包含分数
    }
    
    /// <summary>
    /// RAG检索结果
    /// </summary>
    public class RAGResult
    {
        public string Query;                           // 查询文本
        public string Speaker;                         // 说话者
        public string Listener;                        // 听众
        public List<RAGMatch> VectorMatches;           // 向量检索结果
        public List<RAGMatch> HybridMatches;           // 混合检索结果
        public List<RAGMatch> RerankedMatches;         // 重排序结果
        public string GeneratedContext;                // 生成的上下文
        public int TotalMatches;                       // 总匹配数
        public DateTime RetrievalTime;                 // 检索时间
        
        public static RAGResult Empty()
        {
            return new RAGResult
            {
                VectorMatches = new List<RAGMatch>(),
                HybridMatches = new List<RAGMatch>(),
                RerankedMatches = new List<RAGMatch>(),
                TotalMatches = 0
            };
        }
    }
    
    /// <summary>
    /// RAG匹配项
    /// </summary>
    public class RAGMatch
    {
        public RAGMatchSource Source;                  // 来源
        public MemoryEntry Memory;                     // 记忆（如果来自记忆）
        public CommonKnowledgeEntry Knowledge;         // 常识（如果来自常识）
        public float Score;                            // 原始分数
        public float FinalScore;                       // 最终分数（重排序后）
        public string MatchType;                       // 匹配类型
        public ScoreBreakdown Breakdown;               // 评分详情
    }
    
    /// <summary>
    /// RAG匹配来源
    /// </summary>
    public enum RAGMatchSource
    {
        VectorDB,    // 向量数据库
        Memory,      // 记忆系统
        Knowledge    // 常识库
    }
    
    #endregion
}
