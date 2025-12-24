using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 超级关键词检索引擎 v1.0
    /// 
    /// 技术栈：
    /// - TF-IDF权重
    /// - BM25排序
    /// - 停用词过滤
    /// - 长词优先
    /// - 位置权重
    /// - 模糊匹配
    /// 
    /// 目标：准确率从88% → 95%+
    /// 性能：<10ms，完全同步
    /// </summary>
    public static class SuperKeywordEngine
    {
        // ? v3.3.15: 优化正则表达式 - 支持字母数字混合单词（必须以字母开头）
        // 匹配：M16, AK47, T5, V2（字母开头 + 至少1个字母或数字）
        // 不匹配：123, 2b（数字开头）
        private static readonly Regex EnglishWordRegex = new Regex(
            @"\b[a-zA-Z][a-zA-Z0-9]{1,}\b",
            RegexOptions.Compiled
        );
        
        // 中文停用词表（高频但无意义的词）
        private static readonly HashSet<string> StopWords = new HashSet<string>
        {
            "的", "了", "是", "在", "我", "有", "和", "就", "不", "人", "都", "一", "个", "也", "上",
            "他", "们", "到", "说", "要", "去", "你", "会", "着", "没有", "看", "好", "自己", "这",
            "那", "为", "来", "而", "能", "可以", "与", "但", "很", "吗", "吧", "啊", "呢", "么",
            "什么", "怎么", "为什么", "哪里", "谁", "多少", "几个", "一些", "一点", "有点", "太",
            "非常", "比较", "还", "更", "最", "大", "小", "多", "少", "新", "旧", "好", "坏"
        };

        // 高权重词前缀（这些词开头的词语更重要）
        private static readonly HashSet<string> ImportantPrefixes = new HashSet<string>
        {
            "龙王", "索拉克", "梅菲斯特", "殖民", "战斗", "受伤", "死亡", "爱情", "友谊", "仇恨",
            "任务", "建造", "种植", "采矿", "研究", "医疗", "袭击", "防御", "贸易", "谈判"
        };

        /// <summary>
        /// 超级关键词提取（优化版）
        /// ? v3.3.2.28: 修复英文单词被切割的问题，使用智能分词
        /// </summary>
        public static List<WeightedKeyword> ExtractKeywords(string text, int maxKeywords = 100)
        {
            if (string.IsNullOrEmpty(text))
                return new List<WeightedKeyword>();

            // 截断过长文本
            const int MAX_TEXT_LENGTH = 500;
            if (text.Length > MAX_TEXT_LENGTH)
                text = text.Substring(0, MAX_TEXT_LENGTH);

            var keywordScores = new Dictionary<string, KeywordScore>();

            // ? v3.3.2.28: 先提取英文单词（完整单词，不切割）
            ExtractEnglishWords(text, keywordScores);

            // ? v3.3.2.28: 再提取中文词组（2-6字滑动窗口）
            ExtractChineseWords(text, keywordScores);

            // 2. 计算TF-IDF权重
            int totalWords = keywordScores.Values.Sum(s => s.Frequency);
            
            foreach (var score in keywordScores.Values)
            {
                // TF (Term Frequency)
                float tf = (float)score.Frequency / totalWords;
                
                // 长度权重：长词更重要
                float lengthWeight = 1.0f + (score.Length - 2) * 0.3f; // 2字=1.0, 3字=1.3, 4字=1.6, 5字=1.9, 6字=2.2
                
                // 位置权重：靠前的词更重要
                float positionWeight = 1.0f - ((float)score.FirstPosition / text.Length) * 0.3f;
                
                // 重要词加成（特定前缀）
                float importanceBonus = 1.0f;
                foreach (var prefix in ImportantPrefixes)
                {
                    if (score.Word.StartsWith(prefix))
                    {
                        importanceBonus = 1.5f;
                        break;
                    }
                }
                
                // 综合权重
                score.Weight = tf * lengthWeight * positionWeight * importanceBonus;
            }

            // 3. 排序并返回，? 最多100个
            // ? v3.3.2.29: 添加确定性 tie-breaker（权重降序 + 词语字母顺序升序）
            return keywordScores.Values
                .OrderByDescending(s => s.Weight)
                .ThenBy(s => s.Word, StringComparer.Ordinal) // ? 确定性 tie-breaker
                .Take(maxKeywords)
                .Select(s => new WeightedKeyword { Word = s.Word, Weight = s.Weight })
                .ToList();
        }

        /// <summary>
        /// ? v3.3.2.35: 优化版 - 使用静态编译的正则表达式
        /// </summary>
        private static void ExtractEnglishWords(string text, Dictionary<string, KeywordScore> keywordScores)
        {
            // ? 使用预编译的正则表达式提取完整的英文单词（2个字母以上）
            var matches = EnglishWordRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                string word = match.Value;
                int position = match.Index;
                
                // 过滤低质量关键词
                if (IsLowQualityKeyword(word))
                    continue;
                
                // 停用词过滤
                if (StopWords.Contains(word.ToLower()))
                    continue;

                if (!keywordScores.ContainsKey(word))
                {
                    keywordScores[word] = new KeywordScore
                    {
                        Word = word,
                        Length = word.Length,
                        FirstPosition = position
                    };
                }
                
                keywordScores[word].Frequency++;
            }
        }

        /// <summary>
        /// ? v3.3.2.28: 提取中文词组（2-6字滑动窗口）
        /// </summary>
        private static void ExtractChineseWords(string text, Dictionary<string, KeywordScore> keywordScores)
        {
            // 只对中文字符使用滑动窗口
            for (int length = 2; length <= 6; length++)
            {
                for (int i = 0; i <= text.Length - length; i++)
                {
                    string word = text.Substring(i, length);
                    
                    // 只提取包含中文字符的词组
                    if (!ContainsChinese(word))
                        continue;
                    
                    // 停用词过滤
                    if (StopWords.Contains(word))
                        continue;

                    if (!keywordScores.ContainsKey(word))
                    {
                        keywordScores[word] = new KeywordScore
                        {
                            Word = word,
                            Length = length,
                            FirstPosition = i
                        };
                    }
                    
                    keywordScores[word].Frequency++;
                }
            }
        }

        /// <summary>
        /// ? v3.3.2.28: 检查字符串是否包含中文字符
        /// </summary>
        private static bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            foreach (char c in text)
            {
                // 中文字符Unicode范围：\u4e00-\u9fa5
                if (c >= 0x4e00 && c <= 0x9fa5)
                    return true;
            }
            
            return false;
        }
        /// <summary>
        /// BM25评分（行业标准的相关性排序算法）
        /// </summary>
        public static float CalculateBM25Score(
            List<WeightedKeyword> queryKeywords,
            string document,
            List<string> documentKeywords,
            float k1 = 1.5f,  // TF饱和参数
            float b = 0.75f)  // 文档长度归一化
        {
            if (queryKeywords.Count == 0 || string.IsNullOrEmpty(document))
                return 0f;

            float score = 0f;
            int docLength = document.Length;
            float avgDocLength = 100f; // 假设平均文档长度

            foreach (var queryKw in queryKeywords)
            {
                // 计算词频
                int freq = documentKeywords.Count(kw => kw == queryKw.Word);
                if (freq == 0)
                    continue;

                // BM25公式
                float idf = (float)Math.Log(1.0 + (1.0 / (freq + 0.5)));
                float tf = (freq * (k1 + 1)) / (freq + k1 * (1 - b + b * docLength / avgDocLength));
                
                score += idf * tf * queryKw.Weight;
            }

            return score;
        }

        /// <summary>
        /// 模糊匹配（处理同义词、拼写变体）
        /// </summary>
        public static bool FuzzyMatch(string word1, string word2, float threshold = 0.8f)
        {
            if (word1 == word2)
                return true;

            // 编辑距离（Levenshtein距离）
            int distance = LevenshteinDistance(word1, word2);
            int maxLen = Math.Max(word1.Length, word2.Length);
            
            float similarity = 1.0f - ((float)distance / maxLen);
            return similarity >= threshold;
        }

        /// <summary>
        /// 编辑距离算法
        /// </summary>
        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; i++)
                d[i, 0] = i;
            for (int j = 0; j <= m; j++)
                d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// ? v3.3.2.28: 检测低质量关键词（增强版，过滤英文碎片和后缀）
        /// </summary>
        private static bool IsLowQualityKeyword(string word)
        {
            if (string.IsNullOrEmpty(word))
                return true;
            
            // 规则1：纯数字（1-2位）过滤
            if (word.Length <= 2 && word.All(char.IsDigit))
                return true;
            
            // 规则2：3字母以下的纯英文单词过滤（例如："the", "is", "of", "to", "and"等）
            if (word.Length <= 3 && word.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                // 例外：保留常见的重要英文缩写
                var importantAbbreviations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "AI", "HP", "DPS", "XP", "UI", "API", "CPU", "GPU", "RAM", "SSD"
                };
                
                if (!importantAbbreviations.Contains(word))
                    return true;
            }
            
            // ? v3.3.2.28: 规则3：过滤常见的无意义英文后缀/前缀
            var meaninglessSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tion", "ment", "ing", "ed", "er", "ly", "ness", "ity", "able", "ible",
                "al", "ful", "less", "ous", "ive", "ant", "ent", "ism", "ist", "ship"
            };
            
            if (word.Length <= 4 && meaninglessSuffixes.Contains(word))
                return true;
            
            // 规则4：2字符的无意义组合过滤（例如："1a", "x2", "3b"）
            if (word.Length == 2)
            {
                bool hasDigit = word.Any(char.IsDigit);
                bool hasLetter = word.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
                
                // 数字+字母的2字符组合通常无意义
                if (hasDigit && hasLetter)
                    return true;
            }
            
            // 规则5：纯符号（不应该出现，但做兜底检查）
            if (word.All(c => !char.IsLetterOrDigit(c)))
                return true;
            
            // ? v3.3.2.28: 规则6：过滤包含空格的单字符（例如：" c", " r"）
            if (word.Trim().Length == 1)
                return true;
            
            return false;
        }
    }

    /// <summary>
    /// 关键词评分详情
    /// </summary>
    internal class KeywordScore
    {
        public string Word;
        public int Length;
        public int Frequency;
        public int FirstPosition;
        public float Weight;
    }

    /// <summary>
    /// 带权重的关键词
    /// </summary>
    public class WeightedKeyword
    {
        public string Word;
        public float Weight;
    }
}
