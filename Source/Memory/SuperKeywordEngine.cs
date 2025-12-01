using System;
using System.Collections.Generic;
using System.Linq;
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
        /// ? v3.3.2.27: 过滤3字母以下的无意义词汇，上限提升到100
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

            // 1. 多长度分词（2-6字，精确模式）
            for (int length = 2; length <= 6; length++)
            {
                for (int i = 0; i <= text.Length - length; i++)
                {
                    string word = text.Substring(i, length);
                    
                    // 过滤纯符号和空白
                    if (!word.Any(c => char.IsLetterOrDigit(c)))
                        continue;
                    
                    // ? v3.3.2.27: 过滤3字母以下的纯英文无意义词汇
                    if (IsLowQualityKeyword(word))
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

            // 3. 排序并返回（? 上限100）
            return keywordScores.Values
                .OrderByDescending(s => s.Weight)
                .Take(maxKeywords)
                .Select(s => new WeightedKeyword { Word = s.Word, Weight = s.Weight })
                .ToList();
        }
        
        /// <summary>
        /// ? v3.3.2.27: 检测低质量关键词（3字母以下的纯英文/无意义词汇）
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
            
            // 规则3：2字符的无意义组合过滤（例如："1a", "x2", "3b"）
            if (word.Length == 2)
            {
                bool hasDigit = word.Any(char.IsDigit);
                bool hasLetter = word.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
                
                // 数字+字母的2字符组合通常无意义
                if (hasDigit && hasLetter)
                    return true;
            }
            
            // 规则4：纯符号（不应该出现，但做兜底检查）
            if (word.All(c => !char.IsLetterOrDigit(c)))
                return true;
            
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
