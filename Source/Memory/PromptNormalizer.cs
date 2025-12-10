using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 提示词规范化引擎
    /// 负责安全、快速地执行用户自定义的文本替换规则
    /// ? v3.3.2.37: 新增功能
    /// </summary>
    public static class PromptNormalizer
    {
        // 预编译的正则表达式缓存
        private static List<(Regex regex, string replacement)> compiledRules = new List<(Regex, string)>();
        
        // 超时保护（20ms）
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(20);
        
        /// <summary>
        /// 更新规则（从设置中加载并预编译）
        /// </summary>
        public static void UpdateRules(List<RimTalk.MemoryPatch.RimTalkMemoryPatchSettings.ReplacementRule> rules)
        {
            if (rules == null)
            {
                compiledRules.Clear();
                return;
            }
            
            var newCompiledRules = new List<(Regex, string)>();
            int successCount = 0;
            int errorCount = 0;
            
            foreach (var rule in rules)
            {
                // 跳过禁用的规则
                if (!rule.isEnabled)
                    continue;
                
                // 跳过空规则
                if (string.IsNullOrEmpty(rule.pattern))
                    continue;
                
                try
                {
                    // 预编译正则表达式（启用编译优化 + 忽略大小写 + 超时保护）
                    var regex = new Regex(
                        rule.pattern,
                        RegexOptions.Compiled | RegexOptions.IgnoreCase,
                        RegexTimeout
                    );
                    
                    newCompiledRules.Add((regex, rule.replacement ?? ""));
                    successCount++;
                }
                catch (ArgumentException ex)
                {
                    // 捕获无效的正则表达式
                    Log.Warning($"[PromptNormalizer] Invalid regex pattern '{rule.pattern}': {ex.Message}");
                    errorCount++;
                }
                catch (Exception ex)
                {
                    // 捕获其他异常
                    Log.Error($"[PromptNormalizer] Failed to compile regex '{rule.pattern}': {ex.Message}");
                    errorCount++;
                }
            }
            
            // 更新缓存
            compiledRules = newCompiledRules;
            
            // 日志输出（仅开发模式）
            if (Prefs.DevMode)
            {
                Log.Message($"[PromptNormalizer] Updated rules: {successCount} compiled, {errorCount} errors");
            }
        }
        
        /// <summary>
        /// 规范化输入文本（应用所有规则）
        /// </summary>
        public static string Normalize(string input)
        {
            // 空值检查
            if (string.IsNullOrEmpty(input))
                return input;
            
            // 如果没有规则，直接返回
            if (compiledRules.Count == 0)
                return input;
            
            string result = input;
            
            // 依次应用所有规则
            foreach (var (regex, replacement) in compiledRules)
            {
                try
                {
                    result = regex.Replace(result, replacement);
                }
                catch (RegexMatchTimeoutException)
                {
                    // 超时保护：跳过当前规则，继续处理
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[PromptNormalizer] Regex timeout for pattern '{regex}', skipping...");
                    }
                    continue;
                }
                catch (Exception ex)
                {
                    // 其他异常：跳过当前规则
                    Log.Warning($"[PromptNormalizer] Regex replace failed: {ex.Message}");
                    continue;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取当前激活的规则数量
        /// </summary>
        public static int GetActiveRuleCount()
        {
            return compiledRules.Count;
        }
    }
}
