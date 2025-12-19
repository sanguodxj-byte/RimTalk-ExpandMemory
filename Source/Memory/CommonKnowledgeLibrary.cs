using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld; // ⭐ v3.3.3: 添加RimWorld命名空间（用于GenDate）

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
        
        // ⭐ v3.3.3: 新增创建时间戳和原始事件文本（用于动态更新时间前缀）
        public int creationTick = -1;       // -1表示永久，>=0表示创建时的游戏tick
        public string originalEventText = "";  // 保存不带时间前缀的原始事件文本
        
        private List<string> cachedTags; // 缓存分割后的标签列表

        public CommonKnowledgeEntry()
        {
            id = "ck-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            keywords = new List<string>();
            isEnabled = true;
            importance = 0.5f;
            targetPawnId = -1; // 默认全局
            creationTick = -1; // 默认永久
            originalEventText = "";
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (keywords == null) keywords = new List<string>();
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
        /// 计算与上下文的相关性分数（标签匹配 + 内容匹配）
        /// ⭐ v3.3.2.25: 优化长关键词权重 + 精确匹配加成
        /// ⭐ v3.3.2.31: 提高名字关键词的匹配分数
        /// ⭐ v3.3.2.38: 接受角色名字列表参数，改进名字识别
        /// ⭐ v3.3.10: 修改标签评分机制 - 单标签0.15分，多标签累加
        /// </summary>
        public float CalculateRelevanceScore(List<string> contextKeywords, HashSet<string> pawnNames = null)
        {
            if (!isEnabled)
                return 0f;

            // 基础分：基于重要性
            float baseScore = importance * KnowledgeWeights.BaseScore;

            // 如果无上下文，只返回基础分
            if (contextKeywords == null || contextKeywords.Count == 0)
                return baseScore;

            // 1. ⭐ 标签匹配（每个匹配的标签独立计分，累加）
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
            
            // ⭐ v3.3.10: 每个匹配的标签贡献0.15分，累加
            float tagPart = tagMatchCount * 0.15f;

            // ⭐ v3.3.22: 规则类常识只返回基础分+标签分，不计算内容分
            bool isRule = IsRuleKnowledge();
            if (isRule)
            {
                return baseScore + tagPart;
            }

            // 2. ⭐ 内容匹配（长关键词加权 + 名字特殊加成）
            float contentMatchScore = 0f;
            float nameMatchBonus = 0f; // ⭐ v3.3.2.31: 名字匹配额外加成
            
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var keyword in contextKeywords)
                {
                    // 直接在内容中查找关键词
                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // ⭐ v3.3.2.38: 改进名字检测 - 直接检查是否在角色名字列表中
                        bool isNameKeyword = false;
                        if (keyword.Length >= 2 && keyword.Length <= 6 && pawnNames != null)
                        {
                            isNameKeyword = pawnNames.Contains(keyword);
                        }
                        
                        // 基础长关键词权重
                        if (keyword.Length >= 6)
                            contentMatchScore += 0.35f;  // 6字+（降低 0.40→0.35）
                        else if (keyword.Length >= 5)
                            contentMatchScore += 0.28f;  // 5字（降低 0.30→0.28）
                        else if (keyword.Length >= 4)
                            contentMatchScore += 0.22f;  // 4字（提升 0.20→0.22）
                        else if (keyword.Length == 3)
                            contentMatchScore += 0.16f;  // 3字（提升 0.12→0.16）✅
                        else if (keyword.Length == 2)
                            contentMatchScore += 0.10f;  // 2字（提升 0.05→0.10）✅
                        else
                            contentMatchScore += 0.05f;  // 1字（保持）
                        
                        // ⭐ v3.3.2.31: 名字额外加成（0.3分）
                        if (isNameKeyword)
                        {
                            nameMatchBonus += 0.30f;
                        }
                    }
                }
            }
            
            // 限制最高分
            contentMatchScore = Math.Min(contentMatchScore, 1.5f);
            nameMatchBonus = Math.Min(nameMatchBonus, 0.6f); // 最多2个名字 * 0.3

            // 3. ⭐ 完全匹配加成（内容包含连续的长查询串）
            // 3. ⭐ v3.3.12: 完全匹配加成 - 包含2字关键词
            float exactMatchBonus = 0f;
            
            if (!string.IsNullOrEmpty(content))
            {
                // ⭐ 检查最长的关键词（包含2字）
                var longestKeywords = contextKeywords
                    .Where(k => k.Length >= 2)  // ✅ 改为 >= 2（原 >= 3）
                    .OrderByDescending(k => k.Length)
                    .Take(5);
                
                foreach (var keyword in longestKeywords)
                {
                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (keyword.Length >= 6)
                            exactMatchBonus += 0.6f;   // 6字+（降低 0.8→0.6）
                        else if (keyword.Length >= 5)
                            exactMatchBonus += 0.4f;   // 5字（降低 0.5→0.4）
                        else if (keyword.Length >= 4)
                            exactMatchBonus += 0.25f;  // 4字（降低 0.3→0.25）
                        else if (keyword.Length == 3)
                            exactMatchBonus += 0.15f;  // 3字（新增）✅
                        else if (keyword.Length == 2)
                            exactMatchBonus += 0.10f;  // 2字（新增）✅
                    }
                }
            }
            
            exactMatchBonus = Math.Min(exactMatchBonus, 0.8f);  // 降低上限（1.0→0.8）

            // 综合评分
            float contentPart = contentMatchScore;
            float exactPart = exactMatchBonus;
            float namePart = nameMatchBonus;
            float totalScore = baseScore + tagPart + contentPart + exactPart + namePart;

            return totalScore;
        }
        
        /// <summary>
        /// 计算与上下文的相关性分数（带详细信息）- 用于调试
        /// ⭐ v3.3.2.25: 同步优化长关键词权重 + 精确匹配加成
        /// ⭐ v3.3.2.31: 提高名字关键词的匹配分数
        /// ⭐ v3.3.2.38: 接受角色名字列表参数，改进名字识别
        /// ⭐ v3.3.10: 修改标签评分机制 - 单标签0.15分，多标签累加
        /// </summary>
        public KnowledgeScoreDetail CalculateRelevanceScoreWithDetails(List<string> contextKeywords, HashSet<string> pawnNames = null)
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

            // 1. ⭐ 标签匹配（每个匹配的标签独立计分，累加）
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

            // ⭐ v3.3.10: 每个匹配的标签贡献0.15分，累加
            int tagMatchCount = matchedTags.Count;
            float tagPart = tagMatchCount * 0.15f;
            detail.MatchedTags = matchedTags;
            detail.TagScore = tagPart; // 直接记录标签总分，而不是比例

            // ⭐ v3.3.22: 规则类常识只返回基础分+标签分
            bool isRule = IsRuleKnowledge();
            if (isRule)
            {
                detail.TotalScore = baseScore + tagPart;
                detail.JaccardScore = 0f;
                detail.KeywordMatchCount = 0;
                detail.MatchedKeywords = new List<string>();
                
                if (matchedTags.Count == 0)
                {
                    detail.FailReason = $"[规则类] 标签'{string.Join(",", tags)}'未匹配";
                }
                else
                {
                    detail.FailReason = $"[规则类] 仅标签匹配({matchedTags.Count}个标签，总分{tagPart:F2})";
                }
                
                return detail;
            }

            // 2. ⭐ 内容匹配（长关键词加权 + 名字特殊加成）
            var matchedKeywords = new List<string>();
            var matchedNameKeywords = new List<string>(); // ⭐ v3.3.2.38: 记录名字关键词
            float contentMatchScore = 0f;
            float nameMatchBonus = 0f;
            
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var keyword in contextKeywords)
                {
                    // 直接在内容中查找关键词
                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedKeywords.Add(keyword);
                        
                        // ⭐ v3.3.2.38: 改进名字检测 - 直接检查是否在角色名字列表中
                        bool isNameKeyword = false;
                        if (keyword.Length >= 2 && keyword.Length <= 6 && pawnNames != null)
                        {
                            isNameKeyword = pawnNames.Contains(keyword);
                            if (isNameKeyword)
                            {
                                matchedNameKeywords.Add(keyword);
                            }
                        }
                        
                        // 基础长关键词权重
                        if (keyword.Length >= 6)
                            contentMatchScore += 0.35f;  // 6字+（降低 0.40→0.35）
                        else if (keyword.Length >= 5)
                            contentMatchScore += 0.28f;  // 5字（降低 0.30→0.28）
                        else if (keyword.Length >= 4)
                            contentMatchScore += 0.22f;  // 4字（提升 0.20→0.22）
                        else if (keyword.Length == 3)
                            contentMatchScore += 0.16f;  // 3字（提升 0.12→0.16）✅
                        else if (keyword.Length == 2)
                            contentMatchScore += 0.10f;  // 2字（提升 0.05→0.10）✅
                        else
                            contentMatchScore += 0.05f;  // 1字（保持）
                        
                        // ⭐ v3.3.2.31: 名字额外加成（0.3分）
                        if (isNameKeyword)
                        {
                            nameMatchBonus += 0.30f;
                        }
                    }
                }
            }
            
            contentMatchScore = Math.Min(contentMatchScore, 1.5f);
            nameMatchBonus = Math.Min(nameMatchBonus, 0.6f);

            // 3. ⭐ 完全匹配加成
            // 3. ⭐ v3.3.12: 完全匹配加成 - 包含2字关键词
            float exactMatchBonus = 0f;
            
            if (!string.IsNullOrEmpty(content))
            {
                // ⭐ 检查最长的关键词（包含2字）
                var longestKeywords = contextKeywords
                    .Where(k => k.Length >= 2)  // ✅ 改为 >= 2（原 >= 3）
                    .OrderByDescending(k => k.Length)
                    .Take(5);
                
                foreach (var keyword in longestKeywords)
                {
                    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (keyword.Length >= 6)
                            exactMatchBonus += 0.6f;   // 6字+（降低 0.8→0.6）
                        else if (keyword.Length >= 5)
                            exactMatchBonus += 0.4f;   // 5字（降低 0.5→0.4）
                        else if (keyword.Length >= 4)
                            exactMatchBonus += 0.25f;  // 4字（降低 0.3→0.25）
                        else if (keyword.Length == 3)
                            exactMatchBonus += 0.15f;  // 3字（新增）✅
                        else if (keyword.Length == 2)
                            exactMatchBonus += 0.10f;  // 2字（新增）✅
                    }
                }
            }
            
            exactMatchBonus = Math.Min(exactMatchBonus, 0.8f);  // 降低上限（1.0→0.8）

            // 综合评分
            float contentPart = contentMatchScore;
            float exactPart = exactMatchBonus;
            float totalScore = baseScore + tagPart + contentPart + exactPart + nameMatchBonus;

            detail.TotalScore = totalScore;
            detail.JaccardScore = exactMatchBonus;
            detail.KeywordMatchCount = matchedKeywords.Count;
            detail.MatchedKeywords = matchedKeywords;
            
            if (matchedTags.Count == 0 && matchedKeywords.Count == 0)
            {
                detail.FailReason = $"标签'{string.Join(",", tags)}'和内容均未匹配";
            }
            else if (matchedTags.Count == 0)
            {
                // ⭐ v3.3.2.38: 详细显示名字匹配信息
                string nameInfo = nameMatchBonus > 0 ? $"+名字加成{nameMatchBonus:F2}({string.Join(",", matchedNameKeywords)})" : "";
                detail.FailReason = $"仅内容匹配({matchedKeywords.Count}个关键词，长关键词加成{nameInfo})";
            }
            else if (matchedKeywords.Count == 0)
            {
                // ⭐ v3.3.10: 显示标签累加分数
                detail.FailReason = $"仅标签匹配({matchedTags.Count}个标签，总分{tagPart:F2})";
            }
            else
            {
                // ⭐ v3.3.10: 显示标签累加分数
                string tagInfo = tagMatchCount > 0 ? $"标签{tagMatchCount}个({tagPart:F2}分)" : "";
                string nameInfo = nameMatchBonus > 0 ? $"+名字{nameMatchBonus:F2}({string.Join(",", matchedNameKeywords)})" : "";
                string combinedInfo = string.IsNullOrEmpty(tagInfo) ? nameInfo : 
                                     string.IsNullOrEmpty(nameInfo) ? tagInfo : 
                                     $"{tagInfo} {nameInfo}";
                detail.FailReason = exactMatchBonus > 0 ? $"精确匹配加成{exactMatchBonus:F2} {combinedInfo}" : combinedInfo;
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
            }
        }

        /// <summary>
        /// 添加常识
        /// ⭐ v3.3.2.25: 完全移除向量化代码
        /// </summary>
        public void AddEntry(string tag, string content)
        {
            var entry = new CommonKnowledgeEntry(tag, content);
            entries.Add(entry);
        }

        /// <summary>
        /// 添加常识
        /// ⭐ v3.3.2.25: 完全移除向量化代码
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
            if (entry != null)
            {
                entries.Remove(entry);
            }
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
        /// ⭐ v3.3.2.25: 完全移除向量化代码
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
            
            // ⭐ 移除日志输出
            // Log.Message($"[Knowledge] Imported {importCount} knowledge entries");

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
            
            // ⭐ v3.3.2.38: 收集角色名字列表（用于名字识别）
            var pawnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // 添加当前角色关键词
            if (currentPawn != null)
            {
                int beforeCount = contextKeywords.Count;
                keywordInfo.PawnInfo = ExtractPawnKeywordsWithDetails(contextKeywords, currentPawn);
                totalPawnKeywords += contextKeywords.Count - beforeCount;
                
                // 收集当前角色名字
                if (!string.IsNullOrEmpty(currentPawn.Name?.ToStringShort))
                {
                    pawnNames.Add(currentPawn.Name.ToStringShort);
                }
            }
            
            // ⭐ 添加目标角色关键词（如果存在）
            if (targetPawn != null && targetPawn != currentPawn)
            {
                int beforeCount = contextKeywords.Count;
                var targetPawnInfo = ExtractPawnKeywordsWithDetails(contextKeywords, targetPawn);
                totalPawnKeywords += contextKeywords.Count - beforeCount;
                
                // 收集目标角色名字
                if (!string.IsNullOrEmpty(targetPawn.Name?.ToStringShort))
                {
                    pawnNames.Add(targetPawn.Name.ToStringShort);
                }
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

            // ⭐ v3.3.2.38: 计算每个常识的相关性分数时传递角色名字列表
            allScores = filteredEntries
                .Select(e => e.CalculateRelevanceScoreWithDetails(contextKeywords, pawnNames))
                .OrderByDescending(se => se.TotalScore)
                .ThenByDescending(se => se.KeywordMatchCount) // ⭐ 优先按匹配数量（高到低）
                .ThenBy(se => se.Entry.id, StringComparer.Ordinal) // ⭐ 最后按 ID（稳定排序）
                .ToList();
            
            // ⭐ v3.3.2.29: 过滤并获取前N条（已排序，无需再次排序）
            var scopedEntries = allScores
                .Where(se => se.TotalScore >= threshold)
                .Take(maxEntries)
                .Select(detail => new KnowledgeScore
                {
                    Entry = detail.Entry,
                    Score = detail.TotalScore
                })
                .ToList();

            scores = scopedEntries;

            // 如果没有常识达到阈值，返回null
            if (scopedEntries.Count == 0)
            {
                return null;
            }
            
            // 格式化为system rule的简洁格式
            var sb = new StringBuilder();

            int index = 1;
            foreach (var scored in scopedEntries)
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

                // 4. 种族（⭐ v3.3.2.26: 添加亚种关键词提取）
                if (pawn.def != null)
                {
                    // 主种族
                    AddAndRecord(pawn.def.label, keywords, info.RaceKeywords);
                    
                    // ⭐ 亚种信息（Biotech DLC / Mod添加的种族）
                    try
                    {
                        // 方法A：检查pawn.genes.Xenotype
                        if (pawn.genes != null && pawn.genes.Xenotype != null)
                        {
                            string xenotypeName = pawn.genes.Xenotype.label ?? pawn.genes.Xenotype.defName;
                            if (!string.IsNullOrEmpty(xenotypeName))
                            {
                                AddAndRecord(xenotypeName, keywords, info.RaceKeywords);
                                
                                // 添加组合关键词（例如："人类-基准人"、"龙王种-索拉克"）
                                string combinedRace = $"{pawn.def.label}-{xenotypeName}";
                                AddAndRecord(combinedRace, keywords, info.RaceKeywords);
                            }
                        }
                        
                        // 方法B：检查CustomXenotype（自定义亚种名）
                        if (pawn.genes != null)
                        {
                            var customXenotypeField = pawn.genes.GetType().GetField("xenotypeName");
                            if (customXenotypeField != null)
                            {
                                string customName = customXenotypeField.GetValue(pawn.genes) as string;
                                if (!string.IsNullOrEmpty(customName))
                                {
                                    AddAndRecord(customName, keywords, info.RaceKeywords);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 兼容性：如果没有Biotech DLC或基因系统不可用，跳过
                    }
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
                    // ⭐ 重构：优先选择重要关系，按 thingIDNumber 排序保证稳定
                    var allRelatedPawns = pawn.relations.RelatedPawns.ToList();
                    
                    // 定义重要关系类型（配偶、恋人、未婚妻；父母、子女、兄弟姐妹；羁绊动物）
                    var importantRelationDefs = new HashSet<PawnRelationDef>
                    {
                        PawnRelationDefOf.Spouse,
                        PawnRelationDefOf.Lover,
                        PawnRelationDefOf.Fiance,
                        PawnRelationDefOf.Parent,
                        PawnRelationDefOf.Child
                    };
					
					// 过滤掉无效的关系
					allRelatedPawns = allRelatedPawns
						.Where(rp => rp != null && rp.thingIDNumber >= 0)
						.ToList();
                    
                    // ⭐ 步骤1：选择重要关系，按 thingIDNumber 排序（稳定排序）
                    var importantPawns = allRelatedPawns
                        .Where(rp => pawn.relations.DirectRelations
                            .Any(dr => dr.otherPawn == rp && importantRelationDefs.Contains(dr.def)))
                        .OrderBy(rp => rp.thingIDNumber) // 稳定排序
                        .ToList();
                    
                    // ⭐ 步骤2：检查是否有羁绊动物（Biotech DLC）
                    try
                    {
                        var bondedAnimals = new List<Verse.Pawn>();
                        foreach (var map in Find.Maps)
                        {
                            if (map.mapPawns == null) continue;
                            
                            foreach (var animal in map.mapPawns.AllPawns.Where(p => p.RaceProps != null && p.RaceProps.Animal))
                            {
                                if (animal.relations != null && animal.relations.DirectRelationExists(PawnRelationDefOf.Bond, pawn))
                                {
                                    bondedAnimals.Add(animal);
                                }
                            }
                        }
                        
                        // 将羁绊动物添加到重要关系列表（也按 thingIDNumber 排序）
                        bondedAnimals = bondedAnimals.OrderBy(ba => ba.thingIDNumber).ToList();
                        importantPawns.AddRange(bondedAnimals);
                    }
                    catch
                    {
                        // 兼容性：如果没有 Bond 关系或系统不可用，跳过
                    }
                    
                    // ⭐ 步骤3：如果凑不够5个人，从剩余关系中随机抽取填充
                    var selectedPawns = importantPawns.Take(5).ToList();
                    
                    if (selectedPawns.Count < 5)
                    {
                        var remainingPawns = allRelatedPawns
                            .Except(importantPawns)
                            .ToList();
                        
                        // 随机打乱剩余关系
                        var random = new System.Random(pawn.thingIDNumber); // 使用 pawn.thingIDNumber 作为随机种子保证稳定
                        remainingPawns = remainingPawns.OrderBy(x => random.Next()).ToList();
                        
                        // 填充到5个人
                        int needed = 5 - selectedPawns.Count;
                        selectedPawns.AddRange(remainingPawns.Take(needed));
                    }
                    
                    // ⭐ 步骤4：遍历最多5人，提取关键词
                    foreach (var relatedPawn in selectedPawns)
                    {
                        // 提取相关人物的名字
                        if (!string.IsNullOrEmpty(relatedPawn.Name?.ToStringShort))
                        {
                            var relatedName = relatedPawn.Name.ToStringShort;
                            AddAndRecord(relatedName, keywords, info.RelationshipKeywords);
                        }
                        
                        // ⭐ 提取关系类型标签（按优先级排序，最多2个）
                        var directRelations = pawn.relations.DirectRelations
                            .Where(r => r.otherPawn == relatedPawn)
                            .OrderBy(r => GetRelationPriority(r.def)) // 按优先级排序
                            .ToList();
                        
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
        /// 提取上下文关键词（核心 + 模糊双重策略）
        /// ⭐ v3.3.2.34: 修复非确定性行为 - 使用完全确定性排序
        /// ⭐ v3.3.20: 优化数量和质量 - 增加到50个，过滤格式词
        /// </summary>
        private List<string> ExtractContextKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            // 截断过长文本，防止性能问题
            const int MAX_TEXT_LENGTH = 800; // ⭐ 增加上限 500→800
            if (text.Length > MAX_TEXT_LENGTH)
            {
                text = text.Substring(0, MAX_TEXT_LENGTH);
            }

            // ⭐ 格式词黑名单（过滤无意义的格式符号和常见词）
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "的", "了", "是", "在", "有", "和", "就", "不", "人", "都", "一", "我", "他", "她", "它", "们",
                "你", "我们", "他们", "这", "那", "什么", "怎么", "为什么", "吗", "呢", "啊", "吧",
                "...", "---", "===", "***", "###", "```", "===", "---",
                "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "can", "could"
            };

            // 使用超级关键词引擎获取候选词（增加上限）
            var weightedKeywords = SuperKeywordEngine.ExtractKeywords(text, 200); // ⭐ 增加 100→200
            
            // ⭐ 过滤格式词和无效词
            weightedKeywords = weightedKeywords
                .Where(kw => !stopWords.Contains(kw.Word))  // 过滤黑名单
                .Where(kw => kw.Word.Length >= 2)           // 至少2个字符
                .Where(kw => !string.IsNullOrWhiteSpace(kw.Word))
                .Where(kw => kw.Word.Any(c => char.IsLetterOrDigit(c))) // 至少包含一个字母或数字
                .ToList();
            
            if (weightedKeywords.Count == 0)
            {
                // ⭐ 移除警告日志
                return new List<string>();
            }
            
            // ⭐ 策略1：核心词 - 按长度降序 + 字母顺序升序，取前25个（增加）
            var sortedByLength = weightedKeywords
                .OrderByDescending(kw => kw.Word.Length)
                .ThenBy(kw => kw.Word, StringComparer.Ordinal)
                .ToList();
            
            var coreKeywords = sortedByLength.Take(25).ToList(); // ⭐ 增加 10→25
            
            // ⭐ 策略2：模糊词 - 从剩余池按字母顺序选25个（增加）
            var remainingPool = sortedByLength.Skip(25).ToList();
            var fuzzyKeywords = new List<WeightedKeyword>();
            
            if (remainingPool.Count > 0)
            {
                fuzzyKeywords = remainingPool
                    .OrderBy(kw => kw.Word, StringComparer.Ordinal)
                    .Take(25) // ⭐ 增加 10→25
                    .ToList();
            }
            
            // ⭐ 策略3：合并核心词 + 模糊词（最多50个）
            var finalKeywords = new List<string>();
            finalKeywords.AddRange(coreKeywords.Select(kw => kw.Word));
            finalKeywords.AddRange(fuzzyKeywords.Select(kw => kw.Word));
            
            return finalKeywords;
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
        
        /// <summary>
        /// ⭐ 获取关系优先级（数字越小优先级越高）
        /// 第一级：配偶、恋人、未婚妻
        /// 第二级：父母、子女、兄弟姐妹
        /// 第三级：羁绊
        /// 其他关系：默认优先级
        /// </summary>
        private int GetRelationPriority(PawnRelationDef relationDef)
        {
            if (relationDef == null)
                return 999; // 无效关系，最低优先级
            
            // 第一级：配偶、恋人、未婚妻（优先级 0-2）
            if (relationDef == PawnRelationDefOf.Spouse) return 0;
            if (relationDef == PawnRelationDefOf.Lover) return 1;
            if (relationDef == PawnRelationDefOf.Fiance) return 2;
            
            // 第二级：父母、子女、兄弟姐妹（优先级 10-19）
            if (relationDef == PawnRelationDefOf.Parent) return 10;
            if (relationDef == PawnRelationDefOf.Child) return 11;
            if (relationDef == PawnRelationDefOf.Sibling) return 12;
            
            // 第三级：羁绊（优先级 20-29）
            if (relationDef == PawnRelationDefOf.Bond) return 20;
            
            // 其他关系（优先级 100+）
            return 100;
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
