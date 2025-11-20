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
        public string tag;          // 标签
        public string content;      // 内容
        public float importance;    // 重要性
        public List<string> keywords; // 关键词
        public bool isEnabled;      // 是否启用

        public CommonKnowledgeEntry()
        {
            id = "ck-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            keywords = new List<string>();
            isEnabled = true;
            importance = 0.5f;
        }

        public CommonKnowledgeEntry(string tag, string content) : this()
        {
            this.tag = tag;
            this.content = content;
            ExtractKeywords();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref tag, "tag");
            Scribe_Values.Look(ref content, "content");
            Scribe_Values.Look(ref importance, "importance", 0.5f);
            Scribe_Values.Look(ref isEnabled, "isEnabled", true);
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (keywords == null) keywords = new List<string>();
            }
        }

        /// <summary>
        /// 提取关键词
        /// </summary>
        public void ExtractKeywords()
        {
            keywords.Clear();
            
            if (string.IsNullOrEmpty(content))
                return;

            // 简单的中文分词（2-4字）
            for (int length = 2; length <= 4; length++)
            {
                for (int i = 0; i <= content.Length - length; i++)
                {
                    string word = content.Substring(i, length);
                    if (word.Any(c => char.IsLetterOrDigit(c)) && !keywords.Contains(word))
                    {
                        keywords.Add(word);
                    }
                }
            }

            // 限制关键词数量
            if (keywords.Count > 20)
            {
                keywords = keywords.Take(20).ToList();
            }
        }

        /// <summary>
        /// 计算与上下文的相关性分数
        /// </summary>
        public float CalculateRelevanceScore(List<string> contextKeywords)
        {
            if (!isEnabled)
                return 0f;

            if (contextKeywords == null || contextKeywords.Count == 0)
                return importance * 0.3f; // 基础分数

            if (keywords == null || keywords.Count == 0)
                return importance * 0.3f;

            // 计算关键词匹配
            var intersection = keywords.Intersect(contextKeywords).Count();
            var union = keywords.Union(contextKeywords).Count();

            float jaccardScore = union > 0 ? (float)intersection / union : 0f;

            // 检查标签是否匹配
            float tagScore = 0f;
            if (!string.IsNullOrEmpty(tag))
            {
                foreach (var keyword in contextKeywords)
                {
                    if (tag.Contains(keyword) || keyword.Contains(tag))
                    {
                        tagScore = 0.3f;
                        break;
                    }
                }
            }

            // 综合评分
            float score = (jaccardScore * 0.7f + tagScore) * importance;
            return score;
        }

        /// <summary>
        /// 格式化为导出格式
        /// </summary>
        public string FormatForExport()
        {
            return $"[{tag}]{content}";
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
                // 没有标签，整行作为内容
                return new CommonKnowledgeEntry("通用", line);
            }

            string tag = line.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
            string content = line.Substring(tagEnd + 1).Trim();

            if (string.IsNullOrEmpty(content))
                return null;

            return new CommonKnowledgeEntry(tag, content);
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
        /// </summary>
        public string InjectKnowledgeWithDetails(string context, int maxEntries, out List<KnowledgeScore> scores)
        {
            scores = new List<KnowledgeScore>();

            if (entries.Count == 0)
                return string.Empty;

            // 提取上下文关键词
            List<string> contextKeywords = ExtractKeywords(context);

            // 计算每个常识的相关性分数
            var scoredEntries = entries
                .Where(e => e.isEnabled)
                .Select(e => new KnowledgeScore
                {
                    Entry = e,
                    Score = e.CalculateRelevanceScore(contextKeywords)
                })
                .Where(se => se.Score > 0.1f) // 过滤低分
                .OrderByDescending(se => se.Score)
                .Take(maxEntries)
                .ToList();

            scores = scoredEntries;

            if (scoredEntries.Count == 0)
                return string.Empty;

            // 格式化为注入文本
            var sb = new StringBuilder();
            sb.AppendLine("## 背景常识");
            sb.AppendLine();

            foreach (var scored in scoredEntries)
            {
                var entry = scored.Entry;
                sb.AppendLine($"- [{entry.tag}] {entry.content}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 提取关键词
        /// </summary>
        private List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var keywords = new HashSet<string>();

            // 简单的中文分词（2-4字）
            for (int length = 2; length <= 4; length++)
            {
                for (int i = 0; i <= text.Length - length; i++)
                {
                    string word = text.Substring(i, length);
                    if (word.Any(c => char.IsLetterOrDigit(c)))
                    {
                        keywords.Add(word);
                    }
                }
            }

            return keywords.Take(20).ToList();
        }

        /// <summary>
        /// 获取按标签分组的常识
        /// </summary>
        public Dictionary<string, List<CommonKnowledgeEntry>> GetEntriesByTag()
        {
            return entries
                .GroupBy(e => string.IsNullOrEmpty(e.tag) ? "未分类" : e.tag)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    /// <summary>
    /// 常识评分详情
    /// </summary>
    public class KnowledgeScore
    {
        public CommonKnowledgeEntry Entry;
        public float Score;
    }
}
