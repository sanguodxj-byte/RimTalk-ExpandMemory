using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory
{
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
        
        private List<string> cachedTags; // 缓存分割后的标签列表

        public CommonKnowledgeEntry()
        {
            id = "ck-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            keywords = new List<string>();
            isEnabled = true;
            importance = 0.5f;
            targetPawnId = -1; // 默认全局
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
            Scribe_Values.Look(ref targetPawnId, "targetPawnId", -1); // ⭐ 新增序列化
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (keywords == null) keywords = new List<string>();
                cachedTags = null; // 清除缓存，强制重新解析
            }
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
        /// 计算与上下文的相关性分数（标签匹配 + 内容匹配）
        /// </summary>
        public float CalculateRelevanceScore(List<string> contextKeywords)
        {
            if (!isEnabled)
                return 0f;

            // 基础分：基于重要性
            float baseScore = importance * KnowledgeWeights.BaseScore;

            // 如果无上下文，只返回基础分
            if (contextKeywords == null || contextKeywords.Count == 0)
                return baseScore;

            // 1. 标签匹配（粗略分类）
            var tags = GetTags();
            int tagMatchCount = 0;
            
            if (tags.Count > 0)
            {
                foreach (var tag in tags)
                {
                    foreach (var keyword in contextKeywords)
                    {
                        // 标签和关键词互相包含即算匹配
                        if (tag.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            keyword.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            tagMatchCount++;
                            break; // 每个标签最多匹配一次
                        }
                    }
                }
            }

            // 2. 内容匹配（精确匹配）
            int contentMatchCount = 0;
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var keyword in contextKeywords)
                {
                    // 直接在内容中查找关键词
                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        contentMatchCount++;
                    }
                }
            }

            // 计算匹配率和匹配分
            float tagMatchRate = tags.Count > 0 ? (float)tagMatchCount / tags.Count : 0f;
            float contentMatchScore = Math.Min(contentMatchCount * 0.05f, 0.5f); // 每个匹配+0.05，最高0.5

            // 综合评分
            float tagPart = tagMatchRate * importance * KnowledgeWeights.TagWeight;
            float contentPart = contentMatchScore * importance;
            float totalScore = baseScore + tagPart + contentPart;

            return totalScore;
        }
        
        /// <summary>
        /// 计算与上下文的相关性分数（带详细信息）- 用于调试
        /// </summary>
        public KnowledgeScoreDetail CalculateRelevanceScoreWithDetails(List<string> contextKeywords)
        {
            var detail = new KnowledgeScoreDetail
            {
                Entry = this,
                IsEnabled = isEnabled
            };

            if (!isEnabled)
            {
                detail.TotalScore = 0f;
                detail.FailReason = "常识已禁用";
                return detail;
            }

            // 基础分
            float baseScore = importance * KnowledgeWeights.BaseScore;
            detail.ImportanceScore = importance;

            if (contextKeywords == null || contextKeywords.Count == 0)
            {
                detail.TotalScore = baseScore;
                detail.FailReason = "无上下文关键词";
                return detail;
            }

            // 1. 标签匹配
            var tags = GetTags();
            var matchedTags = new List<string>();
            
            if (tags.Count > 0)
            {
                foreach (var tag in tags)
                {
                    foreach (var keyword in contextKeywords)
                    {
                        if (tag.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            keyword.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedTags.Add($"{tag}←→{keyword}");
                            break;
                        }
                    }
                }
            }

            float tagMatchRate = tags.Count > 0 ? (float)matchedTags.Count / tags.Count : 0f;
            detail.MatchedTags = matchedTags;
            detail.TagScore = tagMatchRate;

            // 2. 内容匹配
            var matchedKeywords = new List<string>();
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var keyword in contextKeywords)
                {
                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedKeywords.Add(keyword);
                    }
                }
            }

            float contentMatchScore = Math.Min(matchedKeywords.Count * 0.05f, 0.5f);
            detail.MatchedKeywords = matchedKeywords;
            detail.KeywordMatchCount = matchedKeywords.Count;

            // 综合评分
            float tagPart = tagMatchRate * importance * KnowledgeWeights.TagWeight;
            float contentPart = contentMatchScore * importance;
            float totalScore = baseScore + tagPart + contentPart;
            
            detail.TotalScore = totalScore;
            detail.JaccardScore = 0f; // 不再使用
            
            if (matchedTags.Count == 0 && matchedKeywords.Count == 0)
            {
                detail.FailReason = $"标签'{string.Join(",", tags)}'和内容均未匹配";
            }
            else if (matchedTags.Count == 0)
            {
                detail.FailReason = $"仅内容匹配({matchedKeywords.Count}个关键词)";
            }
            else if (matchedKeywords.Count == 0)
            {
                detail.FailReason = $"仅标签匹配({matchedTags.Count}/{tags.Count})";
            }
            
            return detail;
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
            }
        }

        /// <summary>
        /// 添加常识
        /// </summary>
        public void AddEntry(string tag, string content)
        {
            var entry = new CommonKnowledgeEntry(tag, content);
            entries.Add(entry);
        }

        /// <summary>
        /// 添加常识
        /// </summary>
        public void AddEntry(CommonKnowledgeEntry entry)
        {
            if (entry != null && !entries.Contains(entry))
            {
                entries.Add(entry);
            }
        }

        /// <summary>
        /// 移除常识
        /// </summary>
        public void RemoveEntry(CommonKnowledgeEntry entry)
        {
            entries.Remove(entry);
        }

        /// <summary>
        /// 清空常识库
        /// </summary>
        public void Clear()
        {
            entries.Clear();
        }

        /// <summary>
        /// 从文本导入常识
        /// 格式: [标签]内容\n[标签]内容
        /// </summary>
        public int ImportFromText(string text, bool clearExisting = false)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            if (clearExisting)
                entries.Clear();

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

            return importCount;
        }

        /// <summary>
        /// 解析单行文本
        /// 支持格式:
        /// 1. [标签|重要性]内容  -> 新格式，带重要性
        /// 2. [标签]内容          -> 旧格式，默认重要性0.5
        /// 3. 纯文本              -> 默认标签"通用"，重要性0.5
        /// </summary>
        private CommonKnowledgeEntry ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            // 查找 [标签]
            int tagStart = line.IndexOf('[');
            int tagEnd = line.IndexOf(']');

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
        /// </summary>
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores, out List<KnowledgeScoreDetail> allScores, out KeywordExtractionInfo keywordInfo, Verse.Pawn currentPawn = null, Verse.Pawn targetPawn = null)
        {
            scores = new List<KnowledgeScore>();
            allScores = new List<KnowledgeScoreDetail>();
            keywordInfo = new KeywordExtractionInfo();

            if (entries.Count == 0)
                return string.Empty;

            // 提取上下文关键词
            List<string> contextKeywords = ExtractContextKeywords(context);
            keywordInfo.ContextKeywords = new List<string>(contextKeywords);
            
            int totalPawnKeywords = 0;
            
            // 添加当前角色关键词
            if (currentPawn != null)
            {
                int beforeCount = contextKeywords.Count;
                keywordInfo.PawnInfo = ExtractPawnKeywordsWithDetails(contextKeywords, currentPawn);
                totalPawnKeywords += contextKeywords.Count - beforeCount;
            }
            
            // 添加目标角色关键词（如果存在）
            if (targetPawn != null && targetPawn != currentPawn)
            {
                int beforeCount = contextKeywords.Count;
                ExtractPawnKeywordsWithDetails(contextKeywords, targetPawn);
                totalPawnKeywords += contextKeywords.Count - beforeCount;
            }
            
            keywordInfo.TotalKeywords = contextKeywords.Count;
            keywordInfo.PawnKeywordsCount = totalPawnKeywords;

            // 获取阈值设置
            float threshold = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings?.knowledgeScoreThreshold ?? 0.1f;

            // ⭐ 过滤常识：只保留全局常识(-1)或专属于当前Pawn的常识
            var filteredEntries = entries
                .Where(e => e.isEnabled)
                .Where(e => e.targetPawnId == -1 || // 全局常识
                           (currentPawn != null && e.targetPawnId == currentPawn.thingIDNumber)) // 或专属于当前Pawn
                .ToList();

            // 计算每个常识的相关性分数（包括详细信息）
            allScores = filteredEntries
                .Select(e => e.CalculateRelevanceScoreWithDetails(contextKeywords))
                .OrderByDescending(se => se.TotalScore)
                .ToList();
            
            // 过滤并获取前N条
            var scoredEntries = allScores
                .Where(se => se.TotalScore >= threshold)
                .Take(maxEntries)
                .Select(detail => new KnowledgeScore
                {
                    Entry = detail.Entry,
                    Score = detail.TotalScore
                })
                .ToList();

            scores = scoredEntries;

            // 如果没有常识达到阈值，返回null
            if (scoredEntries.Count == 0)
            {
                return null;
            }

            // ⭐ 检查是否启用Token压缩
            bool enableCompression = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings?.enableKnowledgeCompression ?? false;
            
            if (enableCompression)
            {
                // 使用压缩模式 - 节省Token
                var entries = scoredEntries.Select(s => s.Entry).ToList();
                return KnowledgeCompressor.CompressKnowledge(entries, 300); // 限制300 tokens
            }

            // 格式化为system rule的简洁格式
            var sb = new StringBuilder();

            int index = 1;
            foreach (var scored in scoredEntries)
            {
                var entry = scored.Entry;
                sb.AppendLine($"{index}. [{entry.tag}] {entry.content}");
                index++;
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// 提取角色关键词（带详细信息）- 用于调试预览
        /// </summary>
        private PawnKeywordInfo ExtractPawnKeywordsWithDetails(List<string> keywords, Verse.Pawn pawn)
        {
            var info = new PawnKeywordInfo
            {
                PawnName = pawn.LabelShort
            };
            
            if (pawn == null || keywords == null)
                return info;

            try
            {
                // 1. 名字
                if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                {
                    var name = pawn.Name.ToStringShort;
                    AddAndRecord(name, keywords, info.NameKeywords);
                }

                // 2. 年龄段（合并逻辑，避免重复）
                if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                {
                    float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                    
                    if (ageYears < 3f)
                    {
                        AddAndRecord("婴儿", keywords, info.AgeKeywords);
                        AddAndRecord("宝宝", keywords, info.AgeKeywords);
                        AddAndRecord("小宝", keywords, info.AgeKeywords);
                        AddAndRecord("baby", keywords, info.AgeKeywords);
                    }
                    else if (ageYears < 13f)
                    {
                        AddAndRecord("儿童", keywords, info.AgeKeywords);
                        AddAndRecord("小孩", keywords, info.AgeKeywords);
                        AddAndRecord("孩子", keywords, info.AgeKeywords);
                        AddAndRecord("child", keywords, info.AgeKeywords);
                    }
                    else if (ageYears < 18f)
                    {
                        AddAndRecord("青少年", keywords, info.AgeKeywords);
                        AddAndRecord("teenager", keywords, info.AgeKeywords);
                    }
                    else
                    {
                        AddAndRecord("成人", keywords, info.AgeKeywords);
                        AddAndRecord("adult", keywords, info.AgeKeywords);
                    }
                }

                // 3. 性别
                if (pawn.gender != null)
                {
                    var genderLabel = pawn.gender.GetLabel();
                    AddAndRecord(genderLabel, keywords, info.GenderKeywords);
                }

                // 4. 种族
                if (pawn.def != null)
                {
                    AddAndRecord(pawn.def.label, keywords, info.RaceKeywords);
                }

                // 5. 特质
                if (pawn.story?.traits != null)
                {
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def?.label != null)
                        {
                            AddAndRecord(trait.def.label, keywords, info.TraitKeywords);
                        }
                    }
                }
                
                // 6. 技能（修复重复添加"精通"的问题）
                if (pawn.skills != null)
                {
                    foreach (var skillRecord in pawn.skills.skills)
                    {
                        if (skillRecord.TotallyDisabled)
                            continue;
                        
                        if (skillRecord.def?.label != null)
                        {
                            AddAndRecord(skillRecord.def.label, keywords, info.SkillKeywords);
                            
                            // 添加技能+等级组合
                            int level = skillRecord.Level;
                            string skillWithLevel = $"{skillRecord.def.label}{level}级";
                            AddAndRecord(skillWithLevel, keywords, info.SkillKeywords);
                            
                            // 添加等级数字
                            AddAndRecord($"{level}级", keywords, info.SkillLevelKeywords);
                            
                            // 添加技能水平描述词（修复重复问题）
                            if (level >= 15)
                            {
                                AddAndRecord("专家", keywords, info.SkillLevelKeywords);
                                AddAndRecord("精通", keywords, info.SkillLevelKeywords);
                            }
                            else if (level >= 10)
                            {
                                AddAndRecord("熟练", keywords, info.SkillLevelKeywords);
                            }
                            else if (level >= 6)
                            {
                                AddAndRecord("良好", keywords, info.SkillLevelKeywords);
                            }
                            else if (level >= 3)
                            {
                                AddAndRecord("基础", keywords, info.SkillLevelKeywords);
                            }
                        }
                    }
                }
                
                // 7. 健康状况
                if (pawn.health != null)
                {
                    if (pawn.health.hediffSet.GetInjuredParts().Any())
                    {
                        AddAndRecord("受伤", keywords, info.HealthKeywords);
                        AddAndRecord("伤势", keywords, info.HealthKeywords);
                    }
                    
                    if (pawn.health.hediffSet.HasNaturallyHealingInjury())
                    {
                        AddAndRecord("恢复中", keywords, info.HealthKeywords);
                    }
                    
                    if (!pawn.health.HasHediffsNeedingTend() && pawn.health.capacities.CapableOf(RimWorld.PawnCapacityDefOf.Moving))
                    {
                        AddAndRecord("健康", keywords, info.HealthKeywords);
                        AddAndRecord("良好", keywords, info.HealthKeywords);
                    }
                }
                
                // 8. 关系
                if (pawn.relations != null)
                {
                    var relatedPawns = pawn.relations.RelatedPawns;
                    foreach (var relatedPawn in relatedPawns.Take(5))
                    {
                        if (!string.IsNullOrEmpty(relatedPawn.Name?.ToStringShort))
                        {
                            var relatedName = relatedPawn.Name.ToStringShort;
                            AddAndRecord(relatedName, keywords, info.RelationshipKeywords);
                        }
                        
                        var directRelations = pawn.relations.DirectRelations.Where(r => r.otherPawn == relatedPawn);
                        foreach (var relation in directRelations.Take(2))
                        {
                            if (relation.def?.label != null)
                            {
                                AddAndRecord(relation.def.label, keywords, info.RelationshipKeywords);
                            }
                        }
                    }
                }
                
                // 9. 成年背景
                if (pawn.story?.Adulthood != null)
                {
                    string backstoryTitle = pawn.story.Adulthood.TitleShortFor(pawn.gender);
                    if (!string.IsNullOrEmpty(backstoryTitle))
                    {
                        info.BackstoryKeywords.Add(backstoryTitle);
                        for (int length = 2; length <= 4 && length <= backstoryTitle.Length; length++)
                        {
                            for (int i = 0; i <= backstoryTitle.Length - length; i++)
                            {
                                string word = backstoryTitle.Substring(i, length);
                                if (word.Any(c => char.IsLetterOrDigit(c)))
                                {
                                    AddAndRecord(word, keywords, info.BackstoryKeywords);
                                }
                            }
                        }
                    }
                }
                
                // 10. 童年背景
                if (pawn.story?.Childhood != null)
                {
                    string childhoodTitle = pawn.story.Childhood.TitleShortFor(pawn.gender);
                    if (!string.IsNullOrEmpty(childhoodTitle))
                    {
                        info.ChildhoodKeywords.Add(childhoodTitle);
                        for (int length = 2; length <= 4 && length <= childhoodTitle.Length; length++)
                        {
                            for (int i = 0; i <= childhoodTitle.Length - length; i++)
                            {
                                string word = childhoodTitle.Substring(i, length);
                                if (word.Any(c => char.IsLetterOrDigit(c)))
                                {
                                    AddAndRecord(word, keywords, info.ChildhoodKeywords);
                                }
                            }
                        }
                    }
                }

                info.TotalCount = info.NameKeywords.Count + info.AgeKeywords.Count + info.GenderKeywords.Count + 
                                 info.RaceKeywords.Count + info.TraitKeywords.Count + info.SkillKeywords.Count + 
                                 info.SkillLevelKeywords.Count + info.HealthKeywords.Count + 
                                 info.RelationshipKeywords.Count + info.BackstoryKeywords.Count + info.ChildhoodKeywords.Count;
            }
            catch (Exception ex)
            {
                Log.Error($"[Knowledge] Error extracting pawn keywords: {ex.Message}\n{ex.StackTrace}");
            }
            
            return info;
        }
        
        /// <summary>
        /// 添加关键词并记录（避免重复）
        /// </summary>
        private void AddAndRecord(string keyword, List<string> allKeywords, List<string> categoryKeywords)
        {
            if (!allKeywords.Contains(keyword))
            {
                allKeywords.Add(keyword);
            }
            if (!categoryKeywords.Contains(keyword))
            {
                categoryKeywords.Add(keyword);
            }
        }
        
        /// <summary>
        /// 提取上下文关键词（简单的中文分词）
        /// v2.4.1优化：提升关键词限制到100，进一步减少Pawn关键词被挤掉的情况
        /// ⭐ v2.4.4修复：截断长文本，防止性能问题
        /// </summary>
        private List<string> ExtractContextKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            // ⭐ 修复2: 截断过长文本，防止O(n²)性能问题
            const int MAX_TEXT_LENGTH = 500; // 最多处理500字符
            if (text.Length > MAX_TEXT_LENGTH)
            {
                text = text.Substring(0, MAX_TEXT_LENGTH);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[Knowledge] Context text truncated to {MAX_TEXT_LENGTH} chars for performance");
                }
            }

            var keywords = new HashSet<string>();

            // 简单的中文关键词提取（2-4字词语）
            for (int length = 2; length <= 4; length++)
            {
                for (int i = 0; i <= text.Length - length; i++)
                {
                    string word = text.Substring(i, length);
                    
                    // 过滤纯符号和空白
                    if (word.Any(c => char.IsLetterOrDigit(c)))
                    {
                        keywords.Add(word);
                    }
                }
            }

            // v2.4.0: 从20增加到50
            // v2.4.1: 从50增加到100，性能影响可忽略（+0.5KB内存，+3ms@100条常识）
            // 大幅提高Pawn关键词匹配准确性，尤其是复杂角色信息（技能、背景等）
            return keywords.Take(100).ToList();
        }
        
        /// <summary>
        /// 获取年龄标签
        /// </summary>
        private string GetAgeLabel(Verse.Pawn pawn)
        {
            if (pawn?.ageTracker == null)
                return null;

            float age = pawn.ageTracker.AgeBiologicalYearsFloat;
            
            if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
            {
                if (age < 3f) return "婴儿";
                if (age < 13f) return "儿童";
                if (age < 18f) return "青少年";
                return "成人";
            }

            return null;
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
        public float JaccardScore;
        public float TagScore;
        public float ImportanceScore;
        public int KeywordMatchCount;
        public List<string> MatchedKeywords = new List<string>();
        public List<string> MatchedTags = new List<string>();
        public string FailReason;
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
    
    /// <summary>
    /// 常识库评分权重配置
    /// </summary>
    public static class KnowledgeWeights
    {
        public static float BaseScore = 0.05f;          // 基础分系数（重要性 * 0.05）
        public static float JaccardWeight = 0.7f;       // Jaccard相似度权重（对应UI中的"重要性"）
        public static float TagWeight = 0.3f;           // 标签匹配权重
        public static float MatchCountBonus = 0.08f;    // 每个匹配关键词加分（固定值）
        public static float KeywordWeight = 0.5f;       // 关键词匹配权重（可选）
        
        /// <summary>
        /// 从设置中加载权重
        /// </summary>
        public static void LoadFromSettings(RimTalk.MemoryPatch.RimTalkMemoryPatchSettings settings)
        {
            if (settings == null) return;
            
            BaseScore = settings.knowledgeBaseScore;
            JaccardWeight = settings.knowledgeJaccardWeight;
            TagWeight = settings.knowledgeTagWeight;
            MatchCountBonus = settings.knowledgeMatchBonus;
        }
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public static void ResetToDefault()
        {
            BaseScore = 0.05f;
            JaccardWeight = 0.7f;
            TagWeight = 0.3f;
            MatchCountBonus = 0.08f;
        }
    }
}
