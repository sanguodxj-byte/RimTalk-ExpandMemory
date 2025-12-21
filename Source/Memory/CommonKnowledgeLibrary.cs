using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

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
        
        // 目标Pawn限制（用于角色专属常识）
        public int targetPawnId = -1;  // -1表示全局，否则只对特定Pawn有效
        
        // 创建时间戳和原始事件文本（用于动态更新时间前缀）
        public int creationTick = -1;       // -1表示永久，>=0表示创建时的游戏tick
        public string originalEventText = "";  // 保存不带时间前缀的原始事件文本

        // 匹配控制属性
        public KeywordMatchMode matchMode = KeywordMatchMode.Any; // 关键词匹配模式（默认Any）
        
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
            Scribe_Values.Look(ref targetPawnId, "targetPawnId", -1);
            Scribe_Values.Look(ref creationTick, "creationTick", -1);
            Scribe_Values.Look(ref originalEventText, "originalEventText", "");
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);
            
            Scribe_Values.Look(ref matchMode, "matchMode", KeywordMatchMode.Any);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (keywords == null) keywords = new List<string>();
                cachedTags = null; // 清除缓存，强制重新解析
                
                // 兼容旧存档 - 如果没有originalEventText，从content中提取
                if (string.IsNullOrEmpty(originalEventText) && !string.IsNullOrEmpty(content))
                {
                    originalEventText = RemoveTimePrefix(content);
                }
            }
        }
        
        private static string RemoveTimePrefix(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
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
        
        public void UpdateEventTimePrefix(int currentTick)
        {
            if (creationTick < 0 || string.IsNullOrEmpty(originalEventText))
                return;
            
            if (isUserEdited)
                return;
            
            int ticksElapsed = currentTick - creationTick;
            int daysElapsed = ticksElapsed / GenDate.TicksPerDay;
            
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
                timePrefix = "约7天前";
            }
            
            content = timePrefix + originalEventText;
        }

        public List<string> GetTags()
        {
            if (cachedTags != null)
                return cachedTags;
            
            if (string.IsNullOrEmpty(tag))
            {
                cachedTags = new List<string>();
                return cachedTags;
            }
            
            cachedTags = tag.Split(new[] { ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            
            return cachedTags;
        }

        public string FormatForExport()
        {
            // 获取扩展属性
            bool canBeExtracted = ExtendedKnowledgeEntry.CanBeExtracted(this);
            bool canBeMatched = ExtendedKnowledgeEntry.CanBeMatched(this);
            
            return $"[{tag}|{importance:F2}|{matchMode}|{canBeExtracted}|{canBeMatched}]{content}";
        }

        public override string ToString()
        {
            return FormatForExport();
        }
        
        public bool IsRuleKnowledge()
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
        
        // 向量数据存储（仅用于序列化）
        // 使用字符串格式存储向量，避免 Scribe 嵌套列表序列化问题
        private List<string> vectorIds;
        private List<string> vectorDataSerialized; // 序列化后的向量数据（逗号分隔的浮点数）
        private List<string> vectorHashes;

        public List<CommonKnowledgeEntry> Entries => entries;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "commonKnowledge", LookMode.Deep);

            // ⭐ 序列化扩展属性（允许被提取、允许被匹配）
            ExtendedKnowledgeEntry.ExposeData();

            // 保存向量数据
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
                {
                    try
                    {
                        List<List<float>> vectorData;
                        VectorDB.VectorService.Instance.ExportVectorsForSave(
                            out vectorIds, out vectorData, out vectorHashes);
                        
                        // 将 List<List<float>> 转换为 List<string>
                        vectorDataSerialized = new List<string>();
                        if (vectorData != null)
                        {
                            foreach (var vector in vectorData)
                            {
                                if (vector != null)
                                {
                                    vectorDataSerialized.Add(string.Join(",", vector));
                                }
                                else
                                {
                                    vectorDataSerialized.Add("");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandMemory] Failed to export vectors for save: {ex}");
                        vectorIds = null;
                        vectorDataSerialized = null;
                        vectorHashes = null;
                    }
                }
            }
            
            // 序列化向量数据（使用字符串格式）
            Scribe_Collections.Look(ref vectorIds, "vectorIds", LookMode.Value);
            Scribe_Collections.Look(ref vectorDataSerialized, "vectorDataSerialized", LookMode.Value);
            Scribe_Collections.Look(ref vectorHashes, "vectorHashes", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (entries == null) entries = new List<CommonKnowledgeEntry>();
                
                // 向量数据恢复和同步
                if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
                {
                    try
                    {
                        // 先恢复向量数据（如果存在）
                        if (vectorIds != null && vectorDataSerialized != null && vectorHashes != null && vectorIds.Count > 0)
                        {
                            Log.Message($"[RimTalk-ExpandMemory] Restoring {vectorIds.Count} vectors from save...");
                            
                            // 将 List<string> 转换回 List<List<float>>
                            var vectorData = new List<List<float>>();
                            foreach (var serialized in vectorDataSerialized)
                            {
                                if (!string.IsNullOrEmpty(serialized))
                                {
                                    var floats = new List<float>();
                                    foreach (var str in serialized.Split(','))
                                    {
                                        if (float.TryParse(str, out float value))
                                        {
                                            floats.Add(value);
                                        }
                                    }
                                    vectorData.Add(floats);
                                }
                                else
                                {
                                    vectorData.Add(new List<float>());
                                }
                            }
                            
                            VectorDB.VectorService.Instance.ImportVectorsFromLoad(
                                vectorIds, vectorData, vectorHashes);
                        }
                        else
                        {
                            Log.Message("[RimTalk-ExpandMemory] No saved vectors found, will perform full sync.");
                        }
                        
                        // 再进行增量同步（只处理新增/修改的条目）
                        Log.Message("[RimTalk-ExpandMemory] Syncing knowledge library to vector database...");
                        VectorDB.VectorService.Instance.SyncKnowledgeLibrary(this);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandMemory] Failed to restore/sync vectors on game load: {ex}");
                    }
                }
            }
        }

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
        
        public void AddEntry(string tag, string content)
        {
            var entry = new CommonKnowledgeEntry(tag, content);
            AddEntry(entry);
        }

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
                
                // 清理扩展属性（如果存在）
                ExtendedKnowledgeEntry.CleanupDeletedEntries(this);
            }
        }

        public void Clear()
        {
            entries.Clear();
            
            // 向量同步
            if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
            {
                try
                {
                    VectorDB.VectorService.Instance.SyncKnowledgeLibrary(this);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] Failed to clear vectors on Clear: {ex.Message}");
                }
            }
            
            ExtendedKnowledgeEntry.CleanupDeletedEntries(this);
        }

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
                    VectorDB.VectorService.Instance.SyncKnowledgeLibrary(this);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] Failed to sync vectors on ImportFromText: {ex.Message}");
                }
            }

            return importCount;
        }

        private CommonKnowledgeEntry ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            int tagStart = line.IndexOf('[');
            int tagEnd = -1;
            
            if (tagStart >= 0)
            {
                tagEnd = line.IndexOf(']', tagStart + 1);
                
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
                return new CommonKnowledgeEntry("通用", line) { importance = 0.5f };
            }

            string tagPart = line.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
            string content = line.Substring(tagEnd + 1).Trim();

            if (string.IsNullOrEmpty(content))
                return null;

            // 解析标签部分（支持新旧格式）
            // 旧格式: [标签|重要性]
            // 新格式: [标签|重要性|匹配模式|允许提取|允许匹配]
            string[] parts = tagPart.Split('|');
            
            string tag = parts.Length > 0 ? parts[0].Trim() : "通用";
            float importance = 0.5f;
            KeywordMatchMode matchMode = KeywordMatchMode.Any;
            bool canBeExtracted = false;
            bool canBeMatched = false;

            // 解析重要性（第2个字段）
            if (parts.Length > 1)
            {
                string importanceStr = parts[1].Trim();
                if (!float.TryParse(importanceStr, out importance))
                {
                    importance = 0.5f;
                    Log.Warning($"[CommonKnowledge] Failed to parse importance '{importanceStr}' in line: {line.Substring(0, Math.Min(50, line.Length))}");
                }
                importance = Math.Max(0f, Math.Min(1f, importance));
            }

            // 解析匹配模式（第3个字段，新格式）
            if (parts.Length > 2)
            {
                string matchModeStr = parts[2].Trim();
                if (!Enum.TryParse(matchModeStr, true, out matchMode))
                {
                    matchMode = KeywordMatchMode.Any;
                    Log.Warning($"[CommonKnowledge] Failed to parse matchMode '{matchModeStr}', using default 'Any'");
                }
            }

            // 解析允许提取（第4个字段，新格式）
            if (parts.Length > 3)
            {
                string canBeExtractedStr = parts[3].Trim();
                if (!bool.TryParse(canBeExtractedStr, out canBeExtracted))
                {
                    canBeExtracted = false;
                }
            }

            // 解析允许匹配（第5个字段，新格式）
            if (parts.Length > 4)
            {
                string canBeMatchedStr = parts[4].Trim();
                if (!bool.TryParse(canBeMatchedStr, out canBeMatched))
                {
                    canBeMatched = false;
                }
            }

            // 创建条目
            var entry = new CommonKnowledgeEntry(tag, content) 
            { 
                importance = importance,
                matchMode = matchMode
            };

            // 设置扩展属性
            ExtendedKnowledgeEntry.SetCanBeExtracted(entry, canBeExtracted);
            ExtendedKnowledgeEntry.SetCanBeMatched(entry, canBeMatched);

            return entry;
        }

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

        public string InjectKnowledge(string context, int maxEntries = 5)
        {
            return InjectKnowledgeWithDetails(context, maxEntries, out _);
        }

        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            return InjectKnowledgeWithDetails(context, maxEntries, out scores, out _, out _, currentPawn, targetPawn);
        }
        
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, out KeywordExtractionInfo keywordInfo, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            return InjectKnowledgeWithDetails(context, maxEntries, out scores, out _, out keywordInfo, currentPawn, targetPawn);
        }
        
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, out List<KnowledgeScoreDetail> allScores, out KeywordExtractionInfo keywordInfo, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            scores = new List<KnowledgeScore>();
            allScores = new List<KnowledgeScoreDetail>();
            keywordInfo = new KeywordExtractionInfo();

            var settings = RimTalkMemoryPatchMod.Settings;
            
            // 构建完整的匹配文本（上下文 + 完整Pawn信息）
            StringBuilder matchTextBuilder = new StringBuilder();
            matchTextBuilder.Append(context);
            
            // 提取完整的 Pawn 信息文本（不切碎）
            if (currentPawn != null)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildCompletePawnInfoText(currentPawn));
                
                // 同时记录关键词信息（用于UI显示）
                var tempKeywords = new List<string>();
                var pawnInfo = KeywordExtractionHelper.ExtractPawnKeywords(tempKeywords, currentPawn);
                keywordInfo.PawnInfo = pawnInfo;
                keywordInfo.PawnKeywordsCount = pawnInfo.TotalCount;
            }
            
            if (targetPawn != null && targetPawn != currentPawn)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildCompletePawnInfoText(targetPawn));
            }
            
            string originalMatchText = matchTextBuilder.ToString();
            string currentMatchText = originalMatchText;
            
            keywordInfo.ContextKeywords = new List<string> { context };
            keywordInfo.TotalKeywords = 1;

            var allMatchedEntries = new HashSet<CommonKnowledgeEntry>();
            
            // 多轮匹配（常识链）
            int maxRounds = settings.enableKnowledgeChaining ? settings.maxChainingRounds : 1;
            
            for (int round = 0; round < maxRounds; round++)
            {
                if (string.IsNullOrEmpty(currentMatchText))
                    break;

                bool isChaining = round > 0;
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
            
            var scoredEntries = new List<KnowledgeScore>();
            
            foreach (var entry in allMatchedEntries)
            {
                KnowledgeMatchType matchType = KnowledgeMatchType.Keyword;
                
                // 标签匹配：0.5分 + 重要性
                float matchTypeScore = 0.5f;
                float finalScore = matchTypeScore + entry.importance;
                
                allScores.Add(new KnowledgeScoreDetail
                {
                    Entry = entry,
                    IsEnabled = entry.isEnabled,
                    TotalScore = finalScore,
                    BaseScore = entry.importance,
                    ManualBonus = 0f,
                    MatchTypeScore = matchTypeScore,
                    MatchType = matchType,
                    MatchedTags = entry.GetTags(),
                    FailReason = "Pending"
                });
                
                scoredEntries.Add(new KnowledgeScore
                {
                    Entry = entry,
                    Score = finalScore
                });
            }

            // 向量增强检索 (已移至 Patch_GenerateAndProcessTalkAsync 异步处理)
            // CommonKnowledgeLibrary 仅负责标签匹配

            // 排序
            scoredEntries.Sort((a, b) => b.Score.CompareTo(a.Score));
            
            // 限制数量
            for (int i = 0; i < scoredEntries.Count; i++)
            {
                var detail = allScores.FirstOrDefault(d => d.Entry == scoredEntries[i].Entry);
                if (detail != null)
                {
                    if (i < maxEntries)
                    {
                        detail.FailReason = "Selected";
                        scores.Add(scoredEntries[i]);
                    }
                    else
                    {
                        detail.FailReason = "ExceedMaxEntries";
                    }
                }
            }
            
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

        private List<CommonKnowledgeEntry> MatchKnowledgeByTags(
            string matchText,
            Verse.Pawn currentPawn,
            HashSet<CommonKnowledgeEntry> alreadyMatched,
            bool isChaining = false)
        {
            var matches = new List<CommonKnowledgeEntry>();

            if (string.IsNullOrEmpty(matchText))
                return matches;

            foreach (var entry in entries)
            {
                if (alreadyMatched.Contains(entry))
                    continue;

                if (!entry.isEnabled)
                    continue;

                if (isChaining && !ExtendedKnowledgeEntry.CanBeMatched(entry)) continue;

                if (entry.targetPawnId != -1 && (currentPawn == null || entry.targetPawnId != currentPawn.thingIDNumber))
                    continue;

                if (IsMatched(matchText, entry))
                {
                    matches.Add(entry);
                }
            }

            return matches;
        }

        private bool IsMatched(string text, CommonKnowledgeEntry entry)
        {
            var tags = entry.GetTags();
            if (tags == null || tags.Count == 0) return false;

            switch (entry.matchMode)
            {
                case KeywordMatchMode.Any:
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    return false;

                case KeywordMatchMode.All:
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        private string BuildMatchTextFromKnowledge(List<CommonKnowledgeEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var entry in entries)
            {
                if (!ExtendedKnowledgeEntry.CanBeExtracted(entry)) continue;

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
        /// 构建完整的 Pawn 信息文本（不切碎，完整保留所有信息）
        /// </summary>
        private string BuildCompletePawnInfoText(Verse.Pawn pawn)
        {
            if (pawn == null)
                return string.Empty;

            var sb = new StringBuilder();

            try
            {
                // 1. 名字
                if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                {
                    sb.Append(pawn.Name.ToStringShort);
                    sb.Append(" ");
                }

                // 2. 年龄段
                if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                {
                    float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                    
                    if (ageYears < 3f)
                    {
                        sb.Append("婴儿 宝宝 ");
                    }
                    else if (ageYears < 13f)
                    {
                        sb.Append("儿童 小孩 ");
                    }
                    else if (ageYears < 18f)
                    {
                        sb.Append("青少年 ");
                    }
                    else
                    {
                        sb.Append("成人 ");
                    }
                }

                // 3. 性别
                if (pawn.gender != null)
                {
                    sb.Append(pawn.gender.GetLabel());
                    sb.Append(" ");
                }

                // 4. 种族
                if (pawn.def != null)
                {
                    sb.Append(pawn.def.label);
                    sb.Append(" ");
                    
                    // 亚种信息（Biotech DLC）
                    try
                    {
                        if (pawn.genes != null && pawn.genes.Xenotype != null)
                        {
                            string xenotypeName = pawn.genes.Xenotype.label ?? pawn.genes.Xenotype.defName;
                            if (!string.IsNullOrEmpty(xenotypeName))
                            {
                                sb.Append(xenotypeName);
                                sb.Append(" ");
                            }
                        }
                    }
                    catch { /* 兼容性：没有Biotech DLC时跳过 */ }
                }

                // 4.5. 身份（殖民者/囚犯/奴隶/访客）
                if (pawn.IsColonist)
                {
                    sb.Append("殖民者 ");
                }
                else if (pawn.IsPrisoner)
                {
                    sb.Append("囚犯 ");
                }
                else if (pawn.IsSlaveOfColony)
                {
                    sb.Append("奴隶 ");
                }
                else if (pawn.HostFaction == Faction.OfPlayer)
                {
                    sb.Append("访客 ");
                }
                else if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
                {
                    sb.Append(pawn.Faction.Name);
                    sb.Append(" ");
                }

                // 5. 特性（所有特性）
                if (pawn.story?.traits != null)
                {
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def?.label != null)
                        {
                            sb.Append(trait.def.label);
                            sb.Append(" ");
                        }
                    }
                }

                // 6. 技能（所有技能，带等级）
                if (pawn.skills != null)
                {
                    foreach (var skillRecord in pawn.skills.skills)
                    {
                        if (skillRecord.TotallyDisabled || skillRecord.def?.label == null)
                            continue;
                        
                        int level = skillRecord.Level;
                        
                        // 只输出有一定等级的技能（>=5级）
                        if (level >= 5)
                        {
                            sb.Append(skillRecord.def.label);
                            sb.Append(level);
                            sb.Append(" ");
                            
                            // 高等级技能额外标记
                            if (level >= 15)
                            {
                                sb.Append(skillRecord.def.label);
                                sb.Append("精通 ");
                            }
                            else if (level >= 10)
                            {
                                sb.Append(skillRecord.def.label);
                                sb.Append("熟练 ");
                            }
                        }
                    }
                }

                // 7. 健康状况
                if (pawn.health != null)
                {
                    if (pawn.health.hediffSet.GetInjuredParts().Any())
                    {
                        sb.Append("受伤 ");
                    }
                    else if (!pawn.health.HasHediffsNeedingTend())
                    {
                        sb.Append("健康 ");
                    }
                }

                // 8. 关系（前5个相关Pawn）
                if (pawn.relations != null)
                {
                    var relatedPawns = pawn.relations.RelatedPawns.Take(5);
                    foreach (var relatedPawn in relatedPawns)
                    {
                        if (!string.IsNullOrEmpty(relatedPawn.Name?.ToStringShort))
                        {
                            sb.Append(relatedPawn.Name.ToStringShort);
                            sb.Append(" ");
                        }
                    }
                }

                // 9. 成年背景（使用完整标题）
                if (pawn.story?.Adulthood != null)
                {
                    string backstoryTitle = pawn.story.Adulthood.TitleFor(pawn.gender);
                    if (!string.IsNullOrEmpty(backstoryTitle))
                    {
                        sb.Append(backstoryTitle);
                        sb.Append(" ");
                    }
                }
                
                // 10. 童年背景（使用完整标题）
                if (pawn.story?.Childhood != null)
                {
                    string childhoodTitle = pawn.story.Childhood.TitleFor(pawn.gender);
                    if (!string.IsNullOrEmpty(childhoodTitle))
                    {
                        sb.Append(childhoodTitle);
                        sb.Append(" ");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-ExpandMemory] Error building complete pawn info text: {ex.Message}");
            }

            return sb.ToString().Trim();
        }
    }

    public class KnowledgeScore
    {
        public CommonKnowledgeEntry Entry;
        public float Score;
    }
    
    public class KnowledgeScoreDetail
    {
        public CommonKnowledgeEntry Entry;
        public bool IsEnabled;
        public float TotalScore;
        
        public float BaseScore;
        public float ManualBonus;
        public float MatchTypeScore;
        
        public KnowledgeMatchType MatchType;
        
        public float JaccardScore;
        public float TagScore;
        public float ImportanceScore;
        public int KeywordMatchCount;
        public List<string> MatchedKeywords = new List<string>();
        public List<string> MatchedTags = new List<string>();
        public string FailReason;
    }
    
    public class KeywordExtractionInfo
    {
        public List<string> ContextKeywords = new List<string>();
        public int TotalKeywords;
        public int PawnKeywordsCount;
        public PawnKeywordInfo PawnInfo;
    }
    
    public class PawnKeywordInfo
    {
        public string PawnName;
        public List<string> NameKeywords = new List<string>();
        public List<string> AgeKeywords = new List<string>();
        public List<string> GenderKeywords = new List<string>();
        public List<string> RaceKeywords = new List<string>();
        public List<string> IdentityKeywords = new List<string>();  // 身份（殖民者/囚犯/奴隶/访客）
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
