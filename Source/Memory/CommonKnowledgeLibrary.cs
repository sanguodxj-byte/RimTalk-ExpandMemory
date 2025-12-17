using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld; // ⭐ v3.3.3: 添加RimWorld命名空间（用于GenDate）
using RimTalk.MemoryPatch; // 引用设置类

namespace RimTalk.Memory
{
    /// <summary>
    /// 关键词匹配模式
    /// </summary>
    public enum KeywordMatchMode
    {
        Any,    // 单词匹配：只要出现其中一个词就算
        All     // 组合匹配：必须同时出现所有标签
    }

    /// <summary>
    /// 匹配类型
    /// </summary>
    public enum KnowledgeMatchType
    {
        Keyword,    // 关键词匹配
        Vector,     // 向量检索
        Mixed       // 混合
    }

    /// <summary>
    /// 常识条目
    /// </summary>
    public class CommonKnowledgeEntry : IExposable
    {
        public string id;
        public string tag;          // 标签（支持多个，用逗号分隔）
        public string content;      // 内容（用于注入）
        public float importance;    // 重要性
        public List<string> keywords; // 关键词（可选，用户手动设置，不导出导入）
        public bool isEnabled;      // 是否启用
        public bool isUserEdited;   // 是否被用户编辑过（用于保护手动修改）
        
        // ⭐ 新增：目标Pawn限制（用于角色专属常识）
        public int targetPawnId = -1;  // -1表示全局，否则只对特定Pawn有效
        
        // ⭐ v3.3.3: 新增创建时间戳和原始事件文本（用于动态更新时间前缀）
        public int creationTick = -1;       // -1表示永久，>=0表示创建时的游戏tick
        public string originalEventText = "";  // 保存不带时间前缀的原始事件文本

        // ⭐ v3.3.20: 新增匹配控制属性
        public KeywordMatchMode matchMode = KeywordMatchMode.Any; // 关键词匹配模式（默认Any）
        public List<string> excludeKeywords = new List<string>(); // 局部排除词
        
        private List<string> cachedTags; // 缓存分割后的标签列表

        /// <summary>
        /// 清除标签缓存（在修改tag后必须调用）
        /// </summary>
        public void InvalidateCache()
        {
            cachedTags = null;
        }

        public CommonKnowledgeEntry()
        {
            id = "ck-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            keywords = new List<string>();
            isEnabled = true;
            importance = 0.5f;
            targetPawnId = -1; // 默认全局
            creationTick = -1; // 默认永久
            originalEventText = "";
            matchMode = KeywordMatchMode.Any;
            excludeKeywords = new List<string>();
        }

        public CommonKnowledgeEntry(string tag, string content) : this()
        {
            this.tag = tag;
            this.content = content;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref tag, "tag");
            Scribe_Values.Look(ref content, "content");
            Scribe_Values.Look(ref importance, "importance", 0.5f);
            Scribe_Values.Look(ref isEnabled, "isEnabled", true);
            Scribe_Values.Look(ref isUserEdited, "isUserEdited", false);
            Scribe_Values.Look(ref targetPawnId, "targetPawnId", -1); // ⭐ 序列化专属Pawn ID
            Scribe_Values.Look(ref creationTick, "creationTick", -1); // ⭐ v3.3.3: 序列化创建时间
            Scribe_Values.Look(ref originalEventText, "originalEventText", ""); // ⭐ v3.3.3: 序列化原始事件文本
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);
            
            // ⭐ v3.3.20: 序列化新属性
            Scribe_Values.Look(ref matchMode, "matchMode", KeywordMatchMode.Any);
            Scribe_Collections.Look(ref excludeKeywords, "excludeKeywords", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (keywords == null) keywords = new List<string>();
                if (excludeKeywords == null) excludeKeywords = new List<string>();
                cachedTags = null; // 清除缓存，强制重新解析
                
                // ⭐ v3.3.3: 兼容旧存档 - 如果没有originalEventText，从content中提取
                if (string.IsNullOrEmpty(originalEventText) && !string.IsNullOrEmpty(content))
                {
                    // 尝试移除时间前缀（"今天"、"3天前"等）
                    originalEventText = RemoveTimePrefix(content);
                }
            }
        }
        
        /// <summary>
        /// ⭐ v3.3.3: 移除时间前缀，提取原始事件文本
        /// </summary>
        private static string RemoveTimePrefix(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // 移除常见的时间前缀
            string[] timePrefixes = { "今天", "1天前", "2天前", "3天前", "4天前", "5天前", "6天前", 
                                     "约3天前", "约4天前", "约5天前", "约6天前", "约7天前" };
            
            foreach (var prefix in timePrefixes)
            {
                if (text.StartsWith(prefix))
                {
                    return text.Substring(prefix.Length);
                }
            }
            
            return text;
        }
        
        /// <summary>
        /// ⭐ v3.3.3: 更新事件常识的时间前缀
        /// </summary>
        public void UpdateEventTimePrefix(int currentTick)
        {
            // 只更新带时间戳的事件常识
            if (creationTick < 0 || string.IsNullOrEmpty(originalEventText))
                return;
            
            // 如果被用户编辑过，不自动更新（保护用户修改）
            if (isUserEdited)
                return;
            
            // 计算时间差
            int ticksElapsed = currentTick - creationTick;
            int daysElapsed = ticksElapsed / GenDate.TicksPerDay;
            
            // 生成新的时间前缀
            string timePrefix = "";
            if (daysElapsed < 1)
            {
                timePrefix = "今天";
            }
            else if (daysElapsed == 1)
            {
                timePrefix = "1天前";
            }
            else if (daysElapsed == 2)
            {
                timePrefix = "2天前";
            }
            else if (daysElapsed < 7)
            {
                timePrefix = $"约{daysElapsed}天前";
            }
            else
            {
                // 超过7天，不再更新时间（保持"约7天前"）
                timePrefix = "约7天前";
            }
            
            // 更新content
            content = timePrefix + originalEventText;
        }

        /// <summary>
        /// 获取标签列表（支持逗号分隔）
        /// </summary>
        public List<string> GetTags()
        {
            if (cachedTags != null)
                return cachedTags;
            
            if (string.IsNullOrEmpty(tag))
            {
                cachedTags = new List<string>();
                return cachedTags;
            }
            
            // 分割标签（支持逗号、顿号、分号）
            cachedTags = tag.Split(new[] { ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            
            return cachedTags;
        }

        
        /// <summary>
        /// 格式化为导出格式（包含重要性）
        /// 格式: [标签|重要性]内容
        /// 例如: [规则|0.9]回复控制在80字以内
        /// </summary>
        public string FormatForExport()
        {
            return $"[{tag}|{importance:F2}]{content}";
        }

        public override string ToString()
        {
            return FormatForExport();
        }
        
        /// <summary>
        /// ⭐ v3.3.22: 判断当前条目是否为规则类常识
        /// 标签包含"规则"、"Instructions"、"rule"（不区分大小写）
        /// </summary>
        private bool IsRuleKnowledge()
        {
            if (string.IsNullOrEmpty(tag))
                return false;
            
            string lowerTag = tag.ToLower();
            return lowerTag.Contains("规则") || 
                   lowerTag.Contains("instructions") || 
                   lowerTag.Contains("rule");
        }
    }

    /// <summary>
    /// 常识库管理器
    /// </summary>
    public class CommonKnowledgeLibrary : IExposable
    {
        private List<CommonKnowledgeEntry> entries = new List<CommonKnowledgeEntry>();

        public List<CommonKnowledgeEntry> Entries => entries;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "commonKnowledge", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (entries == null) entries = new List<CommonKnowledgeEntry>();
                
                // 向量同步
                if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
                {
                    try
                    {
                        // 只在载入时同步
                        Log.Message("[RimTalk-ExpandMemory] Loading game, syncing knowledge library to vector database...");
                        VectorDB.VectorService.Instance.SyncKnowledgeLibrary(this);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandMemory] Failed to sync vectors on game load: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// 添加常识
        /// ⭐ 集成向量同步功能
        /// </summary>
        public void AddEntry(CommonKnowledgeEntry entry)
        {
            if (entry != null && !entries.Contains(entry))
            {
                entries.Add(entry);
                
                // 向量同步
                if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
                {
                    try
                    {
                        if (entry.isEnabled)
                        {
                            VectorDB.VectorService.Instance.UpdateKnowledgeVector(entry.id, entry.content);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandMemory] Failed to sync vector on AddEntry: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 移除常识
        /// ⭐ 集成向量同步功能 + 扩展属性清理
        /// </summary>
        public void RemoveEntry(CommonKnowledgeEntry entry)
        {
            if (entry != null)
            {
                entries.Remove(entry);
                
                // 向量同步
                if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
                {
                    try
                    {
                        VectorDB.VectorService.Instance.RemoveKnowledgeVector(entry.id);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandMemory] Failed to remove vector on RemoveEntry: {ex.Message}");
                    }
                }
                
                // ⭐ 清理扩展属性（防止内存泄漏）
                ExtendedKnowledgeEntry.CleanupDeletedEntries(this);
            }
        }

        /// <summary>
        /// 清空常识库
        /// ⭐ 集成向量同步功能 + 扩展属性清理
        /// </summary>
        public void Clear()
        {
            entries.Clear();
            
            // 向量同步
            if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
            {
                try
                {
                    // 重新同步（清空）
                    VectorDB.VectorService.Instance.SyncKnowledgeLibrary(this);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] Failed to clear vectors on Clear: {ex.Message}");
                }
            }
            
            // ⭐ 清理扩展属性（防止内存泄漏）
            ExtendedKnowledgeEntry.CleanupDeletedEntries(this);
        }

        /// <summary>
        /// 从文本导入常识
        /// 格式: [标签]内容\n[标签]内容
        /// ⭐ 集成向量同步功能
        /// </summary>
        public int ImportFromText(string text, bool clearExisting = false)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            if (clearExisting)
            {
                entries.Clear();
            }

            int importCount = 0;
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                // 解析格式: [标签]内容
                var entry = ParseLine(trimmedLine);
                if (entry != null)
                {
                    entries.Add(entry);
                    importCount++;
                }
            }
            
            // 向量同步
            if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
            {
                try
                {
                    // 批量同步
                    VectorDB.VectorService.Instance.SyncKnowledgeLibrary(this);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] Failed to sync vectors on ImportFromText: {ex.Message}");
                }
            }

            return importCount;
        }

        /// <summary>
        /// 解析单行文本
        /// 支持格式:
        /// 1. [标签|重要性]内容  -> 新格式，带重要性
        /// 2. [标签]内容          -> 旧格式，默认重要性0.5
        /// 3. 纯文本              -> 默认标签"通用"，重要性0.5
        /// ⭐ v3.3.2.38: 增强容错性，支持 [标签} 格式（右括号写错）
        /// </summary>
        private CommonKnowledgeEntry ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            // ⭐ v3.3.2.38: 查找 [ 和第一个 ] 或 }（容错右括号）
            int tagStart = line.IndexOf('[');
            int tagEnd = -1;
            
            if (tagStart >= 0)
            {
                // 优先查找 ]
                tagEnd = line.IndexOf(']', tagStart + 1);
                
                // 如果没有找到 ]，尝试查找 }（容错）
                if (tagEnd == -1)
                {
                    int braceEnd = line.IndexOf('}', tagStart + 1);
                    if (braceEnd > tagStart)
                    {
                        tagEnd = braceEnd;
                        Log.Warning($"[CommonKnowledge] 检测到错误的标签格式（使用了花括号）: {line.Substring(0, Math.Min(50, line.Length))}");
                    }
                }
            }

            if (tagStart == -1 || tagEnd == -1 || tagEnd <= tagStart)
            {
                // 没有标签，整行作为内容，默认重要性0.5
                return new CommonKnowledgeEntry("通用", line) { importance = 0.5f };
            }

            // 提取标签部分
            string tagPart = line.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
            string content = line.Substring(tagEnd + 1).Trim();

            if (string.IsNullOrEmpty(content))
                return null;

            // 解析标签和重要性
            string tag;
            float importance = 0.5f; // 默认重要性

            // 检查是否包含重要性 (格式: 标签|0.8)
            int pipeIndex = tagPart.IndexOf('|');
            if (pipeIndex > 0)
            {
                tag = tagPart.Substring(0, pipeIndex).Trim();
                string importanceStr = tagPart.Substring(pipeIndex + 1).Trim();
                
                // 尝试解析重要性
                if (!float.TryParse(importanceStr, out importance))
                {
                    importance = 0.5f; // 解析失败，使用默认值
                    Log.Warning($"[CommonKnowledge] Failed to parse importance '{importanceStr}' in line: {line.Substring(0, Math.Min(50, line.Length))}");
                }
                
                // 限制重要性范围 [0, 1]
                importance = Math.Max(0f, Math.Min(1f, importance));
            }
            else
            {
                // 旧格式，没有重要性
                tag = tagPart;
            }

            return new CommonKnowledgeEntry(tag, content) { importance = importance };
        }

        /// <summary>
        /// 导出为文本
        /// </summary>
        public string ExportToText()
        {
            var sb = new StringBuilder();

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    sb.AppendLine(entry.FormatForExport());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 动态注入常识到提示词
        /// </summary>
        public string InjectKnowledge(string context, int maxEntries = 5)
        {
            return InjectKnowledgeWithDetails(context, maxEntries, out _);
        }

        /// <summary>
        /// 动态注入常识（带详细评分信息）- 用于预览
        /// 增强版：支持角色关键词注入
        /// </summary>
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            return InjectKnowledgeWithDetails(context, maxEntries, out scores, out _, currentPawn, targetPawn);
        }
        
        /// <summary>
        /// 动态注入常识（带详细评分信息和关键词信息）- 用于预览器
        /// </summary>
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, out KeywordExtractionInfo keywordInfo, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            return InjectKnowledgeWithDetails(context, maxEntries, out scores, out _, out keywordInfo, currentPawn, targetPawn);
        }
        
        /// <summary>
        /// 动态注入常识（完整版 - 带所有调试信息）
        /// 支持双Pawn关键词提取：当前角色 + 交互对象
        /// ⭐ 集成新的标签匹配逻辑 + 常识链 + 向量增强
        /// </summary>
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, out List<KnowledgeScoreDetail> allScores, out KeywordExtractionInfo keywordInfo, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            scores = new List<KnowledgeScore>();
            allScores = new List<KnowledgeScoreDetail>();
            keywordInfo = new KeywordExtractionInfo();

            var settings = RimTalkMemoryPatchMod.Settings;
            
            // 构建完整的匹配文本（上下文 + Pawn信息）
            StringBuilder matchTextBuilder = new StringBuilder();
            matchTextBuilder.Append(context);
            
            if (currentPawn != null)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildPawnInfoText(currentPawn));
            }
            
            if (targetPawn != null && targetPawn != currentPawn)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildPawnInfoText(targetPawn));
            }
            
            // ⭐ 保留原始匹配文本（用于标签匹配和向量匹配）
            string originalMatchText = matchTextBuilder.ToString();
            string currentMatchText = originalMatchText;
            
            keywordInfo.ContextKeywords = new List<string> { context };
            keywordInfo.TotalKeywords = 1;
            keywordInfo.PawnKeywordsCount = 0;

            var allMatchedEntries = new HashSet<CommonKnowledgeEntry>();
            
            // 多轮匹配（常识链）
            int maxRounds = settings.enableKnowledgeChaining ? settings.maxChainingRounds : 1;
            
            for (int round = 0; round < maxRounds; round++)
            {
                if (string.IsNullOrEmpty(currentMatchText))
                    break;

                bool isChaining = round > 0;
                // ⭐ 第一轮使用原始文本，后续轮使用常识链文本
                string matchText = (round == 0) ? originalMatchText : currentMatchText;
                var roundMatches = MatchKnowledgeByTags(matchText, currentPawn, allMatchedEntries, isChaining);
                
                if (roundMatches.Count == 0)
                    break;

                foreach (var match in roundMatches)
                {
                    allMatchedEntries.Add(match);
                }

                if (!settings.enableKnowledgeChaining || round >= maxRounds - 1)
                    break;

                currentMatchText = BuildMatchTextFromKnowledge(roundMatches);
            }
            
            // 向量增强阶段
            var vectorSimilarities = new Dictionary<CommonKnowledgeEntry, float>();

            if (settings.enableVectorEnhancement)
            {
                try
                {
                    // ⭐ v3.3.26: 向量匹配优先使用纯上下文，避免Pawn信息稀释语义
                    // 如果上下文为空，才使用完整文本(包含Pawn信息)
                    // ⭐ v3.3.27: 使用 ContextCleaner 提取核心语义，去除 RimTalk 格式噪音
                    string rawContext = !string.IsNullOrWhiteSpace(context) ? context : originalMatchText;
                    string vectorSearchText = ContextCleaner.CleanForVectorMatching(rawContext);
                    
                    // 如果清理后为空（可能全是噪音），回退到原始文本，防止完全匹配失败
                    if (string.IsNullOrWhiteSpace(vectorSearchText))
                    {
                        vectorSearchText = rawContext;
                    }
                    
                    string trimmedMatchText = vectorSearchText?.Trim() ?? "";
                    
                    if (trimmedMatchText.Length >= 2) // 放宽长度限制
                    {
                        var vectorMatches = MatchKnowledgeByVector(trimmedMatchText, currentPawn, allMatchedEntries, settings.maxVectorResults, settings.vectorSimilarityThreshold);
                        
                        foreach (var (match, similarity) in vectorMatches)
                        {
                            allMatchedEntries.Add(match);
                            vectorSimilarities[match] = similarity;
                        }
                        
                        if (vectorMatches.Count > 0)
                        {
                            Log.Message($"[RimTalk-ExpandMemory] Vector enhancement: matched {vectorMatches.Count} entries for context: '{trimmedMatchText.Substring(0, Math.Min(50, trimmedMatchText.Length))}'");
                        }
                    }
                    else
                    {
                        Log.Message($"[RimTalk-ExpandMemory] Vector enhancement: skipped (context too short: '{trimmedMatchText}')");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] Vector enhancement failed: {ex.Message}");
                }
            }

            // ⭐ v3.3.25: 重构评分系统 - 引入相似度加权 + 混合权重平衡
            // 优先级：标签匹配(100分) vs 向量匹配(100分 * 相似度)
            // 这样高相似度的向量匹配可以与标签匹配平起平坐
            
            // 计算混合权重系数 (0.0=Keywords, 0.5=Equal, 1.0=Vector)
            float keywordWeight = 1.0f;
            float vectorWeight = 1.0f;
            float balance = settings.hybridWeightBalance;

            if (balance < 0.5f)
            {
                // 关键词优先: Key [1.0-1.5], Vec [0.5-1.0]
                // 0.0 -> Key=1.5, Vec=0.5
                // 0.5 -> Key=1.0, Vec=1.0
                keywordWeight = 1.0f + (0.5f - balance);
                vectorWeight = 0.5f + balance;
            }
            else
            {
                // 向量优先: Key [0.5-1.0], Vec [1.0-1.5]
                // 0.5 -> Key=1.0, Vec=1.0
                // 1.0 -> Key=0.5, Vec=1.5
                keywordWeight = 1.0f - (balance - 0.5f);
                vectorWeight = 1.0f + (balance - 0.5f);
            }

            var scoredEntries = new List<KnowledgeScore>();
            
            foreach (var entry in allMatchedEntries)
            {
                // 判断匹配类型（使用原始匹配文本）
                bool isKeywordMatched = IsMatched(originalMatchText, entry);
                KnowledgeMatchType matchType = isKeywordMatched ? 
                    KnowledgeMatchType.Keyword : KnowledgeMatchType.Vector;
                
                // 计算最终得分
                float finalScore = 0f;
                float matchTypeScore = 0f;
                
                if (isKeywordMatched)
                {
                    // 标签匹配：100 * 权重 + 重要性
                    matchTypeScore = 100f * keywordWeight;
                    finalScore = matchTypeScore + entry.importance;
                }
                else
                {
                    // 向量匹配：100 * 相似度 * 权重 + 重要性
                    float similarity = vectorSimilarities.ContainsKey(entry) ? vectorSimilarities[entry] : 0f;
                    matchTypeScore = 100f * similarity * vectorWeight;
                    finalScore = matchTypeScore + entry.importance;
                }
                
                // ⭐ 同时添加到 allScores（所有候选）
                allScores.Add(new KnowledgeScoreDetail
                {
                    Entry = entry,
                    IsEnabled = entry.isEnabled,
                    TotalScore = finalScore,
                    BaseScore = entry.importance,
                    ManualBonus = 0f, // 已删除手动优先级
                    MatchTypeScore = matchTypeScore,
                    MatchType = matchType,
                    MatchedTags = entry.GetTags(),
                    FailReason = "Pending" // 稍后更新
                });
                
                scoredEntries.Add(new KnowledgeScore
                {
                    Entry = entry,
                    Score = finalScore
                });
            }

            // 排序
            scoredEntries.Sort((a, b) => b.Score.CompareTo(a.Score));
            
            // ⭐ v3.3.25: 移除防误触领跑分 (Confidence Margin)
            // 用户反馈该机制会错误过滤掉高分向量匹配
            int cutoffIndex = scoredEntries.Count;
            /*
            if (scoredEntries.Count >= 2 && settings.confidenceMargin > 0)
            {
                float topScore = scoredEntries[0].Score;
                float secondScore = scoredEntries[1].Score;
                
                if (topScore - secondScore > settings.confidenceMargin)
                {
                    cutoffIndex = 1;
                    // 标记被 ConfidenceMargin 过滤的
                    for (int i = 1; i < scoredEntries.Count; i++)
                    {
                        var detail = allScores.FirstOrDefault(d => d.Entry == scoredEntries[i].Entry);
                        if (detail != null)
                        {
                            detail.FailReason = "ConfidenceMargin";
                        }
                    }
                }
            }
            */

            // ⭐ 限制数量，标记 Selected 和 ExceedMaxEntries，并检查阈值
            for (int i = 0; i < scoredEntries.Count; i++)
            {
                var detail = allScores.FirstOrDefault(d => d.Entry == scoredEntries[i].Entry);
                if (detail != null)
                {
                    // ⭐ 检查是否通过阈值
                    bool passThreshold = scoredEntries[i].Score >= settings.knowledgeScoreThreshold;
                    
                    if (i < maxEntries && i < cutoffIndex && passThreshold)
                    {
                        detail.FailReason = "Selected";
                        scores.Add(scoredEntries[i]);
                    }
                    else if (detail.FailReason == "Pending")
                    {
                        // 标记失败原因
                        if (!passThreshold)
                        {
                            detail.FailReason = "LowScore";
                        }
                        else
                        {
                            // 未被 ConfidenceMargin 标记，则是超出数量限制
                            detail.FailReason = "ExceedMaxEntries";
                        }
                    }
                }
            }
            
            // 生成最终注入文本
            var sortedEntries = scores.Select(s => s.Entry).ToList();

            if (sortedEntries.Count == 0)
                return null;

            var sb = new StringBuilder();
            int index = 1;
            foreach (var entry in sortedEntries)
            {
                sb.AppendLine($"{index}. [{entry.tag}] {entry.content}");
                index++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 通过标签匹配常识（新版：支持多种匹配模式和排除词）
        /// </summary>
        private List<CommonKnowledgeEntry> MatchKnowledgeByTags(
            string matchText,
            Verse.Pawn currentPawn,
            HashSet<CommonKnowledgeEntry> alreadyMatched,
            bool isChaining = false)
        {
            var matches = new List<CommonKnowledgeEntry>();

            if (string.IsNullOrEmpty(matchText))
                return matches;

            var settings = RimTalkMemoryPatchMod.Settings;
            string[] globalExcludeList = settings.GetGlobalExcludeKeywords();

            foreach (var entry in entries)
            {
                if (alreadyMatched.Contains(entry))
                    continue;

                if (!entry.isEnabled)
                    continue;

                if (isChaining && !ExtendedKnowledgeEntry.CanBeMatched(entry))
                    continue;

                if (entry.targetPawnId != -1 && (currentPawn == null || entry.targetPawnId != currentPawn.thingIDNumber))
                    continue;

                // 1. 检查排除词 (Global & Local)
                if (IsExcluded(matchText, entry, globalExcludeList))
                    continue;

                // 2. 检查匹配模式
                if (IsMatched(matchText, entry))
                {
                    matches.Add(entry);
                }
            }

            return matches;
        }

        /// <summary>
        /// 检查是否被排除
        /// ⭐ 修复：添加空字符串检查，防止全军覆没
        /// </summary>
        private bool IsExcluded(string text, CommonKnowledgeEntry entry, string[] globalExcludeList)
        {
            // 检查全局排除词
            if (globalExcludeList != null)
            {
                foreach (var exclude in globalExcludeList)
                {
                    // ⭐ 救命代码：跳过空字符串
                    if (string.IsNullOrWhiteSpace(exclude)) continue;
                    if (text.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            // 检查局部排除词
            if (entry.excludeKeywords != null)
            {
                foreach (var exclude in entry.excludeKeywords)
                {
                    // ⭐ 救命代码：跳过空字符串
                    if (string.IsNullOrWhiteSpace(exclude)) continue;
                    if (text.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查是否匹配
        /// ⭐ 修复：Exact 模式改为遍历标签列表，Any 和 Exact 逻辑统一
        /// </summary>
        private bool IsMatched(string text, CommonKnowledgeEntry entry)
        {
            var tags = entry.GetTags();
            if (tags == null || tags.Count == 0) return false;

            switch (entry.matchMode)
            {
                case KeywordMatchMode.Any:
                    // 单词匹配：遍历所有标签，只要有一个匹配就算成功
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    return false;

                case KeywordMatchMode.All:
                    // 组合匹配：必须同时出现所有标签
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) < 0)
                            return false; // 只要有一个没出现就不匹配
                    }
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 从匹配的常识构建新的匹配文本（用于常识链）
        /// </summary>
        private string BuildMatchTextFromKnowledge(List<CommonKnowledgeEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var entry in entries)
            {
                if (!ExtendedKnowledgeEntry.CanBeExtracted(entry))
                    continue;

                if (!string.IsNullOrEmpty(entry.content))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    sb.Append(entry.content);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 通过向量匹配常识（补充标签未匹配的语义相关常识）
        /// ⭐ v3.3.25: 返回相似度
        /// </summary>
        private List<(CommonKnowledgeEntry entry, float similarity)> MatchKnowledgeByVector(
            string context,
            Verse.Pawn currentPawn,
            HashSet<CommonKnowledgeEntry> alreadyMatched,
            int maxResults,
            float similarityThreshold)
        {
            var matches = new List<(CommonKnowledgeEntry, float)>();

            if (string.IsNullOrEmpty(context))
                return matches;

            try
            {
                var vectorResults = VectorDB.VectorService.Instance.FindBestLoreIds(context, maxResults * 2, similarityThreshold);
                
                foreach (var (id, similarity) in vectorResults)
                {
                    var entry = entries.FirstOrDefault(e => e.id == id);
                    
                    if (entry == null)
                        continue;
                    
                    if (alreadyMatched.Contains(entry))
                        continue;
                    
                    if (!entry.isEnabled)
                        continue;
                    
                    if (entry.targetPawnId != -1 && (currentPawn == null || entry.targetPawnId != currentPawn.thingIDNumber))
                        continue;
                    
                    matches.Add((entry, similarity));
                    
                    if (matches.Count >= maxResults)
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] Error in MatchKnowledgeByVector: {ex}");
            }

            return matches;
        }
        
        /// <summary>
        /// 构建Pawn信息文本
        /// </summary>
        private string BuildPawnInfoText(Verse.Pawn pawn)
        {
            if (pawn == null)
                return string.Empty;

            var sb = new StringBuilder();

            try
            {
                if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                {
                    sb.Append(pawn.Name.ToStringShort);
                    sb.Append(" ");
                }

                sb.Append(pawn.gender.GetLabel());
                sb.Append(" ");

                if (pawn.def != null)
                {
                    sb.Append(pawn.def.label);
                    sb.Append(" ");
                }

                if (pawn.story?.traits != null)
                {
                    int traitCount = 0;
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def?.label != null && traitCount < 5)
                        {
                            sb.Append(trait.def.label);
                            sb.Append(" ");
                            traitCount++;
                        }
                    }
                }

                if (pawn.skills != null)
                {
                    foreach (var skillRecord in pawn.skills.skills)
                    {
                        if (skillRecord.TotallyDisabled || skillRecord.Level < 10)
                            continue;

                        if (skillRecord.def?.label != null)
                        {
                            sb.Append(skillRecord.def.label);
                            sb.Append(" ");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-ExpandMemory] Error building pawn info text: {ex.Message}");
            }

            return sb.ToString().Trim();
        }
        
    }

    /// <summary>
    /// 常识评分
    /// </summary>
    public class KnowledgeScore
    {
        public CommonKnowledgeEntry Entry;
        public float Score;
    }
    
    /// <summary>
    /// 常识评分详细信息（用于调试预览）
    /// </summary>
    public class KnowledgeScoreDetail
    {
        public CommonKnowledgeEntry Entry;
        public bool IsEnabled;
        public float TotalScore;
        
        // ⭐ 新增：详细分项
        public float BaseScore;       // 基础重要性
        public float ManualBonus;     // 手动加权 (Priority - 3) * 0.1
        public float MatchTypeScore;  // 匹配类型得分
        
        // ⭐ 新增：匹配来源
        public KnowledgeMatchType MatchType;
        
        // 保留原有字段（向后兼容）
        public float JaccardScore;
        public float TagScore;
        public float ImportanceScore;
        public int KeywordMatchCount;
        public List<string> MatchedKeywords = new List<string>();
        public List<string> MatchedTags = new List<string>();
        public string FailReason; // "Selected", "LowScore", "Excluded", "ConfidenceMargin", "ExceedMaxEntries"
    }
    
    /// <summary>
    /// 关键词提取信息
    /// </summary>
    public class KeywordExtractionInfo
    {
        public List<string> ContextKeywords = new List<string>();
        public int TotalKeywords;
        public int PawnKeywordsCount;
        public PawnKeywordInfo PawnInfo;
    }
    
    /// <summary>
    /// 角色关键词详细信息
    /// </summary>
    public class PawnKeywordInfo
    {
        public string PawnName;
        public List<string> NameKeywords = new List<string>();
        public List<string> AgeKeywords = new List<string>();
        public List<string> GenderKeywords = new List<string>();
        public List<string> RaceKeywords = new List<string>();
        public List<string> TraitKeywords = new List<string>();
        public List<string> SkillKeywords = new List<string>();
        public List<string> SkillLevelKeywords = new List<string>();
        public List<string> HealthKeywords = new List<string>();
        public List<string> RelationshipKeywords = new List<string>();
        public List<string> BackstoryKeywords = new List<string>();
        public List<string> ChildhoodKeywords = new List<string>();
        public int TotalCount;
    }
    
}
