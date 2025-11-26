using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 记忆压缩器 - 减少Token消耗（改进版）
    /// 策略：智能提取关键信息、合并相似记忆、保留上下文
    /// </summary>
    public static class MemoryCompressor
    {
        /// <summary>
        /// 压缩记忆列表，减少Token消耗
        /// </summary>
        public static string CompressMemories(List<MemoryEntry> memories, int maxTokens = 500)
        {
            if (memories == null || memories.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int estimatedTokens = 0;
            int index = 1;

            // 按类型分组，相同类型的记忆可以合并描述
            var grouped = memories.GroupBy(m => m.type).ToList();

            foreach (var group in grouped)
            {
                var memoryList = group.OrderByDescending(m => m.timestamp).ToList();
                
                // 对话类型：保留核心对话内容
                if (group.Key == MemoryType.Conversation)
                {
                    var recentConversations = CompressConversations(memoryList, ref estimatedTokens, maxTokens, ref index);
                    if (!string.IsNullOrEmpty(recentConversations))
                    {
                        sb.Append(recentConversations);
                    }
                }
                // 行动类型：智能合并
                else if (group.Key == MemoryType.Action)
                {
                    var compressedActions = CompressActions(memoryList, ref estimatedTokens, maxTokens, ref index);
                    if (!string.IsNullOrEmpty(compressedActions))
                    {
                        sb.Append(compressedActions);
                    }
                }
                else
                {
                    // 其他类型：保留完整内容但精简时间
                    foreach (var memory in memoryList.Take(3))
                    {
                        string compressed = $"{index}. {ExtractKeyInfo(memory.content)}";
                        int tokens = EstimateTokens(compressed);
                        
                        if (estimatedTokens + tokens > maxTokens)
                            break;
                        
                        sb.AppendLine(compressed);
                        estimatedTokens += tokens;
                        index++;
                    }
                }
                
                if (estimatedTokens >= maxTokens)
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 压缩对话记忆 - 保留对话核心内容
        /// </summary>
        private static string CompressConversations(List<MemoryEntry> conversations, ref int estimatedTokens, int maxTokens, ref int index)
        {
            var sb = new StringBuilder();
            
            // 直接保留对话内容，移除"Said to"等冗余
            foreach (var conv in conversations.Take(5))
            {
                string compressed = ExtractConversationCore(conv.content);
                if (string.IsNullOrEmpty(compressed))
                    continue;
                
                // 格式：序号. 对话内容
                string formatted = $"{index}. {compressed}";
                int tokens = EstimateTokens(formatted);
                
                if (estimatedTokens + tokens > maxTokens)
                    break;
                
                sb.AppendLine(formatted);
                estimatedTokens += tokens;
                index++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 提取对话核心内容
        /// </summary>
        private static string ExtractConversationCore(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";
            
            // 移除"Said to XXX: " 或 "XXX said: "
            string cleaned = content;
            
            // 匹配 "Said to XXX: "
            int saidToIndex = cleaned.IndexOf("Said to ");
            if (saidToIndex >= 0)
            {
                int colonIndex = cleaned.IndexOf(":", saidToIndex);
                if (colonIndex > 0 && colonIndex < cleaned.Length - 1)
                {
                    // 提取对话对象和内容
                    string target = cleaned.Substring(saidToIndex + 8, colonIndex - saidToIndex - 8).Trim();
                    string dialogue = cleaned.Substring(colonIndex + 1).Trim();
                    return $"→{target}: {dialogue}";
                }
            }
            
            // 匹配 "XXX said: "
            int saidIndex = cleaned.IndexOf(" said: ");
            if (saidIndex > 0)
            {
                string speaker = cleaned.Substring(0, saidIndex).Trim();
                string dialogue = cleaned.Substring(saidIndex + 7).Trim();
                return $"{speaker}: {dialogue}";
            }
            
            // 无法解析，返回原内容
            return cleaned;
        }

        /// <summary>
        /// 压缩行动记忆 - 智能合并重复行动
        /// </summary>
        private static string CompressActions(List<MemoryEntry> actions, ref int estimatedTokens, int maxTokens, ref int index)
        {
            var sb = new StringBuilder();
            
            // 按行动类型分组
            var grouped = actions
                .GroupBy(a => ExtractActionType(a.content))
                .OrderByDescending(g => g.Count())
                .Take(5);

            foreach (var group in grouped)
            {
                if (group.Count() == 1)
                {
                    // 单次行动：保留完整信息
                    string content = ExtractKeyInfo(group.First().content);
                    string formatted = $"{index}. {content}";
                    int tokens = EstimateTokens(formatted);
                    
                    if (estimatedTokens + tokens > maxTokens)
                        break;
                    
                    sb.AppendLine(formatted);
                    estimatedTokens += tokens;
                }
                else
                {
                    // 重复行动：合并显示，保留关键信息
                    var first = group.First();
                    string actionType = ExtractActionType(first.content);
                    string target = ExtractActionTarget(first.content);
                    
                    string formatted;
                    if (!string.IsNullOrEmpty(target))
                    {
                        formatted = $"{index}. {actionType} - {target} (×{group.Count()})";
                    }
                    else
                    {
                        formatted = $"{index}. {actionType} (×{group.Count()})";
                    }
                    
                    int tokens = EstimateTokens(formatted);
                    
                    if (estimatedTokens + tokens > maxTokens)
                        break;
                    
                    sb.AppendLine(formatted);
                    estimatedTokens += tokens;
                }
                
                index++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 提取关键信息（智能提取，非简单截断）
        /// </summary>
        private static string ExtractKeyInfo(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";
            
            // 如果内容较短，直接返回
            if (content.Length <= 40)
                return content;
            
            // 尝试在句号、逗号等位置智能截断
            int cutPoint = 40;
            for (int i = 35; i < Math.Min(50, content.Length); i++)
            {
                char c = content[i];
                if (c == '。' || c == '.' || c == '，' || c == ',')
                {
                    cutPoint = i + 1;
                    break;
                }
            }
            
            // 如果没找到合适的截断点，直接截取
            if (cutPoint >= content.Length)
                return content;
            
            return content.Substring(0, cutPoint).Trim();
        }

        /// <summary>
        /// 提取行动类型（用于分组）
        /// </summary>
        private static string ExtractActionType(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "未知";
            
            // 查找 " - " 分隔符
            int dashIndex = content.IndexOf(" - ");
            if (dashIndex > 0 && dashIndex < 20)
            {
                return content.Substring(0, dashIndex).Trim();
            }
            
            // 查找 "×" 符号
            int multiplyIndex = content.IndexOf('×');
            if (multiplyIndex > 0 && multiplyIndex < 20)
            {
                return content.Substring(0, multiplyIndex).Trim();
            }
            
            // 提取前10个字符
            int length = Math.Min(10, content.Length);
            return content.Substring(0, length);
        }

        /// <summary>
        /// 提取行动目标（如"土豆"、"木材"等）
        /// </summary>
        private static string ExtractActionTarget(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";
            
            // 查找 " - " 后面的内容
            int dashIndex = content.IndexOf(" - ");
            if (dashIndex > 0 && dashIndex < content.Length - 3)
            {
                string afterDash = content.Substring(dashIndex + 3).Trim();
                
                // 提取第一个词或短语（最多15字符）
                int length = Math.Min(15, afterDash.Length);
                
                // 尝试在"×"处截断
                int multiplyIndex = afterDash.IndexOf('×');
                if (multiplyIndex > 0 && multiplyIndex < length)
                {
                    return afterDash.Substring(0, multiplyIndex).Trim();
                }
                
                return afterDash.Substring(0, length).Trim();
            }
            
            return "";
        }

        /// <summary>
        /// 估算Token数量（粗略估计：中文1字≈2tokens，英文1词≈1.3tokens）
        /// </summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            
            // 简单估计：中文字符×2 + 英文字符×1.3
            int chineseChars = text.Count(c => c >= 0x4e00 && c <= 0x9fff);
            int otherChars = text.Length - chineseChars;
            
            return chineseChars * 2 + (int)(otherChars * 1.3);
        }
    }
}
