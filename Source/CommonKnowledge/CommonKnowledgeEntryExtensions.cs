using System;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// CommonKnowledgeEntry扩展方法
    /// ★ v3.3.20: 拆分辅助方法，减少主文件复杂度
    /// </summary>
    public static class CommonKnowledgeEntryExtensions
    {
        /// <summary>
        /// ? v3.3.20: 检查是否为完整词匹配（避免子字符串误匹配）
        /// 例如：
        /// - IsCompleteWordMatch("绮罗折纸", "折纸") = true  ? (边界正确)
        /// - IsCompleteWordMatch("绮罗折纸", "绮罗") = false ? (不是完整词)
        /// - IsCompleteWordMatch("机械绮罗", "绮罗") = true  ? (边界正确)
        /// </summary>
        public static bool IsCompleteWordMatch(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
                return false;
            
            // 完全相等
            if (string.Equals(text, word, StringComparison.OrdinalIgnoreCase))
                return true;
            
            // 查找所有出现位置
            int index = 0;
            while ((index = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                // 检查前面是否是边界
                bool frontBoundary = (index == 0) || IsWordBoundary(text[index - 1]);
                
                // 检查后面是否是边界
                int endIndex = index + word.Length;
                bool backBoundary = (endIndex == text.Length) || IsWordBoundary(text[endIndex]);
                
                // 前后都是边界才算完整词匹配
                if (frontBoundary && backBoundary)
                    return true;
                
                index += word.Length;
            }
            
            return false;
        }
        
        /// <summary>
        /// ? v3.3.20: 判断字符是否为词边界
        /// 词边界包括：空格、标点符号、分隔符等
        /// </summary>
        public static bool IsWordBoundary(char c)
        {
            // 空格和常见分隔符
            if (char.IsWhiteSpace(c) || c == ',' || c == '，' || c == '、' || c == ';' || c == '；' ||
                c == '.' || c == '。' || c == '!' || c == '！' || c == '?' || c == '？' ||
                c == ':' || c == '：' || c == '-' || c == '_' || c == '/' || c == '\\' ||
                c == '(' || c == ')' || c == '（' || c == '）' || c == '[' || c == ']' ||
                c == '{' || c == '}' || c == '<' || c == '>' || c == '「' || c == '」' ||
                c == '『' || c == '』' || c == '【' || c == '】')
            {
                return true;
            }
            
            // 其他标点符号
            if (char.IsPunctuation(c) || char.IsSymbol(c))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// ? v3.3.22: 判断当前条目是否为规则类常识
        /// 标签包含"规则"、"Instructions"、"rule"（不区分大小写）
        /// </summary>
        public static bool IsRuleKnowledge(this CommonKnowledgeEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.tag))
                return false;
            
            string lowerTag = entry.tag.ToLower();
            return lowerTag.Contains("规则") || 
                   lowerTag.Contains("instructions") || 
                   lowerTag.Contains("rule");
        }
    }
}
