using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 提示词规范化器 - 在发送给AI前自动替换/规范化提示词
    /// ? v3.3.2.37: 支持正则表达式替换规则
    /// </summary>
    public static class PromptNormalizer
    {
        private static List<RimTalk.MemoryPatch.RimTalkMemoryPatchSettings.ReplacementRule> activeRules = new List<RimTalk.MemoryPatch.RimTalkMemoryPatchSettings.ReplacementRule>();
        private static Dictionary<string, Regex> compiledRegexCache = new Dictionary<string, Regex>();
        
        /// <summary>
        /// 更新替换规则列表
        /// </summary>
        public static void UpdateRules(List<RimTalk.MemoryPatch.RimTalkMemoryPatchSettings.ReplacementRule> rules)
        {
            if (rules == null)
            {
                activeRules.Clear();
                compiledRegexCache.Clear();
                return;
            }
            
            activeRules = rules.Where(r => r != null && r.isEnabled).ToList();
            
            // 预编译正则表达式以提升性能
            compiledRegexCache.Clear();
            foreach (var rule in activeRules)
            {
                if (string.IsNullOrEmpty(rule.pattern))
                    continue;
                
                try
                {
                    var regex = new Regex(rule.pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    compiledRegexCache[rule.pattern] = regex;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PromptNormalizer] Invalid regex pattern '{rule.pattern}': {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 规范化提示词文本
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            if (activeRules.Count == 0)
                return text;
            
            string result = text;
            
            foreach (var rule in activeRules)
            {
                if (string.IsNullOrEmpty(rule.pattern) || rule.replacement == null)
                    continue;
                
                try
                {
                    if (compiledRegexCache.TryGetValue(rule.pattern, out var regex))
                    {
                        result = regex.Replace(result, rule.replacement);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PromptNormalizer] Error applying rule '{rule.pattern}': {ex.Message}");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取当前激活的规则数量
        /// </summary>
        public static int GetActiveRuleCount()
        {
            return activeRules.Count;
        }
    }
}
