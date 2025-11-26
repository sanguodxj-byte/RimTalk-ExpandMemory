using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimTalk.Memory
{
    /// <summary>
    /// 常识库压缩器 - 减少Token消耗（改进版）
    /// 策略：保留完整核心信息，只移除冗余格式
    /// </summary>
    public static class KnowledgeCompressor
    {
        /// <summary>
        /// 压缩常识库条目
        /// </summary>
        public static string CompressKnowledge(List<CommonKnowledgeEntry> entries, int maxTokens = 300)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int estimatedTokens = 0;
            int index = 1;

            // 按重要性排序，优先保留重要的常识
            var sorted = entries
                .OrderByDescending(e => e.importance)
                .ToList();

            foreach (var entry in sorted)
            {
                // 压缩策略：移除标签，保留完整内容
                string compressed = CompressSingleEntry(entry, index);
                int tokens = EstimateTokens(compressed);
                
                if (estimatedTokens + tokens > maxTokens)
                    break;
                
                sb.AppendLine(compressed);
                estimatedTokens += tokens;
                index++;
                
                if (estimatedTokens >= maxTokens)
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 压缩单条常识（改进版）
        /// </summary>
        private static string CompressSingleEntry(CommonKnowledgeEntry entry, int index)
        {
            // 策略1: 移除标签（标签往往是冗余的分类信息）
            // 策略2: 保留完整内容（常识本身就是精炼的，不应再截断）
            string content = entry.content;
            
            // 如果内容过长（超过80字），才进行智能截断
            if (content.Length > 80)
            {
                content = SmartTruncate(content, 80);
            }
            
            return $"{index}. {content}";
        }

        /// <summary>
        /// 智能截断（在标点符号处截断，保留完整语义）
        /// </summary>
        private static string SmartTruncate(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
                return content;
            
            // 尝试在句号、逗号等位置截断
            int cutPoint = maxLength;
            
            // 优先在句号处截断
            for (int i = maxLength - 1; i > maxLength - 20 && i > 0; i--)
            {
                if (content[i] == '。' || content[i] == '.')
                {
                    return content.Substring(0, i + 1);
                }
            }
            
            // 其次在逗号处截断
            for (int i = maxLength - 1; i > maxLength - 20 && i > 0; i--)
            {
                if (content[i] == '，' || content[i] == ',' || content[i] == '；' || content[i] == ';')
                {
                    return content.Substring(0, i + 1);
                }
            }
            
            // 最后在空格处截断
            for (int i = maxLength - 1; i > maxLength - 10 && i > 0; i--)
            {
                if (content[i] == ' ')
                {
                    return content.Substring(0, i) + "...";
                }
            }
            
            // 实在找不到合适的截断点，直接截取
            return content.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// 估算Token数量
        /// </summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            
            int chineseChars = text.Count(c => c >= 0x4e00 && c <= 0x9fff);
            int otherChars = text.Length - chineseChars;
            
            return chineseChars * 2 + (int)(otherChars * 1.3);
        }
    }
}
