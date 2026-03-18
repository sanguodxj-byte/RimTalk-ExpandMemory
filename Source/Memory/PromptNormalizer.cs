using RimTalk.MemoryPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 提示词规范化器 - 在发送给AI前自动替换/规范化提示词
    /// v3.3.2.37: 支持正则表达式替换规则
    /// v5.1: 线程安全重构（不可变快照模式）
    /// </summary>
    public static class PromptNormalizer
    {
        // ⭐ v5.1: 使用不可变快照保证线程安全。
        // UpdateRules 原子替换整个引用，Normalize 读取本地快照，无需加锁。
        private static volatile NormalizerSnapshot _snapshot = new NormalizerSnapshot(
            new List<RimTalkMemoryPatchSettings.ReplacementRule>(),
            new Dictionary<string, Regex>());

        private sealed class NormalizerSnapshot
        {
            public readonly List<RimTalkMemoryPatchSettings.ReplacementRule> Rules;
            public readonly Dictionary<string, Regex> Cache;

            public NormalizerSnapshot(
                List<RimTalkMemoryPatchSettings.ReplacementRule> rules,
                Dictionary<string, Regex> cache)
            {
                Rules = rules;
                Cache = cache;
            }
        }

        /// <summary>
        /// 更新替换规则列表（线程安全：原子替换快照）
        /// </summary>
        public static void UpdateRules(List<RimTalkMemoryPatchSettings.ReplacementRule> rules)
        {
            var newRules = rules == null
                ? new List<RimTalkMemoryPatchSettings.ReplacementRule>()
                : rules.Where(r => r != null && r.isEnabled).ToList();

            var newCache = new Dictionary<string, Regex>();
            foreach (var rule in newRules)
            {
                if (string.IsNullOrEmpty(rule.pattern))
                    continue;
                try
                {
                    var regex = new Regex(rule.pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    newCache[rule.pattern] = regex;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PromptNormalizer] Invalid regex pattern '{rule.pattern}': {ex.Message}");
                }
            }

            // 原子替换：Normalize 读到的要么是旧快照，要么是新快照，不会读到中间状态
            _snapshot = new NormalizerSnapshot(newRules, newCache);
        }

        /// <summary>
        /// 规范化提示词文本（线程安全）
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // 取本地引用，整个调用期间使用同一快照，避免 UpdateRules 并发时的竞态
            var snap = _snapshot;
            if (snap.Rules.Count == 0)
                return text;

            string result = text;
            foreach (var rule in snap.Rules)
            {
                if (string.IsNullOrEmpty(rule.pattern) || rule.replacement == null)
                    continue;
                try
                {
                    if (snap.Cache.TryGetValue(rule.pattern, out var regex))
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
            return _snapshot.Rules.Count;
        }
    }
}
