using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 主动记忆召回系统 - v3.0实验性功能
    /// 让AI主动从记忆中提及相关内容，增强对话连贯性
    /// </summary>
    public static class ProactiveMemoryRecall
    {
        /// <summary>
        /// 触发概率配置
        /// </summary>
        public static class TriggerProbability
        {
            public static float BaseChance = 0.15f;           // 基础触发概率 15%
            public static float HighImportanceBonus = 0.20f;  // 高重要性记忆加成
            public static float RecentMemoryBonus = 0.15f;    // 近期记忆加成
            public static float EmotionalBonus = 0.10f;       // 情感记忆加成
        }

        /// <summary>
        /// 尝试主动召回记忆
        /// </summary>
        /// <param name="pawn">说话者</param>
        /// <param name="context">对话上下文</param>
        /// <param name="listener">听众（可选）</param>
        /// <returns>召回的记忆提示，如果不触发则返回null</returns>
        public static string TryRecallMemory(Pawn pawn, string context, Pawn listener = null)
        {
            // 检查是否启用
            var settings = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings;
            if (settings?.enableProactiveRecall != true)
                return null;

            // 获取记忆组件
            var memoryComp = pawn?.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null)
                return null;

            // 收集候选记忆（SCM + ELS，不包括ABM）
            var candidates = new List<MemoryEntry>();
            candidates.AddRange(memoryComp.SituationalMemories.Take(10));
            candidates.AddRange(memoryComp.EventLogMemories.Take(5));

            if (candidates.Count == 0)
                return null;

            // 提取上下文关键词
            var contextKeywords = ExtractKeywords(context);
            if (contextKeywords.Count == 0)
                return null;

            // 计算每个记忆的召回分数
            var scored = candidates
                .Select(m => new
                {
                    Memory = m,
                    Score = CalculateRecallScore(m, contextKeywords, listener)
                })
                .Where(s => s.Score > 0.3f) // 只考虑相关性较高的
                .OrderByDescending(s => s.Score)
                .ToList();

            if (scored.Count == 0)
                return null;

            // 随机选择是否触发（基于最高分记忆）
            var best = scored.First();
            float triggerChance = CalculateTriggerChance(best.Memory);

            if (UnityEngine.Random.value > triggerChance)
                return null; // 不触发

            // 触发！生成召回提示
            string recallPrompt = GenerateRecallPrompt(best.Memory, context, listener);

            if (Prefs.DevMode)
            {
                Log.Message($"[Proactive Recall] {pawn.LabelShort} recalled memory: {best.Memory.content.Substring(0, Math.Min(50, best.Memory.content.Length))} (Score: {best.Score:F2}, Chance: {triggerChance:P0})");
            }

            return recallPrompt;
        }

        /// <summary>
        /// 计算召回分数（与注入评分不同，更注重情感和重要性）
        /// </summary>
        private static float CalculateRecallScore(MemoryEntry memory, List<string> contextKeywords, Pawn listener)
        {
            float score = 0f;

            // 1. 关键词匹配度（权重40%）
            float keywordMatch = 0f;
            if (memory.keywords != null && contextKeywords != null)
            {
                int matches = memory.keywords.Intersect(contextKeywords).Count();
                if (matches > 0)
                {
                    keywordMatch = Math.Min((float)matches / contextKeywords.Count, 1f);
                }
            }
            score += keywordMatch * 0.4f;

            // 2. 重要性（权重30%）
            score += memory.importance * 0.3f;

            // 3. 新鲜度（权重20%）
            int age = Find.TickManager.TicksGame - memory.timestamp;
            float freshness = UnityEngine.Mathf.Exp(-age / 120000f); // 2天半衰期
            score += freshness * 0.2f;

            // 4. 听众相关性（权重10%）
            if (listener != null && !string.IsNullOrEmpty(memory.relatedPawnName))
            {
                if (memory.relatedPawnName == listener.LabelShort)
                {
                    score += 0.1f;
                }
            }

            // 5. 特殊加成
            if (memory.type == MemoryType.Emotion)
                score += 0.15f; // 情感记忆更容易被主动提及

            if (memory.isPinned)
                score += 0.2f; // 固定记忆优先

            return score;
        }

        /// <summary>
        /// 计算触发概率
        /// </summary>
        private static float CalculateTriggerChance(MemoryEntry memory)
        {
            float chance = TriggerProbability.BaseChance;

            // 重要性加成
            if (memory.importance > 0.7f)
                chance += TriggerProbability.HighImportanceBonus;

            // 近期记忆加成
            int age = Find.TickManager.TicksGame - memory.timestamp;
            if (age < 60000) // 1天内
                chance += TriggerProbability.RecentMemoryBonus;

            // 情感记忆加成
            if (memory.type == MemoryType.Emotion)
                chance += TriggerProbability.EmotionalBonus;

            return Math.Min(chance, 0.6f); // 最高60%触发率
        }

        /// <summary>
        /// 生成召回提示（注入到System Rule）
        /// </summary>
        private static string GenerateRecallPrompt(MemoryEntry memory, string context, Pawn listener)
        {
            var sb = new StringBuilder();

            sb.AppendLine("## ?? Active Memory Recall");
            sb.AppendLine("(AI Instruction: The character spontaneously recalls this memory. Naturally mention or reference it in the response.)");
            sb.AppendLine();

            // 记忆内容
            string typeTag = GetMemoryTypeTag(memory.type);
            string timeStr = memory.TimeAgoString;
            
            sb.AppendLine($"**Recalled Memory:** [{typeTag}] {memory.content}");
            sb.AppendLine($"**When:** {timeStr}");

            // 如果有相关Pawn，标注
            if (!string.IsNullOrEmpty(memory.relatedPawnName))
            {
                sb.AppendLine($"**Related to:** {memory.relatedPawnName}");
            }

            // 情感提示
            if (memory.type == MemoryType.Emotion)
            {
                sb.AppendLine($"**Emotional weight:** High (importance: {memory.importance:P0})");
            }

            sb.AppendLine();
            sb.AppendLine("(Use this memory to add depth and continuity to your response. Don't just repeat it - weave it naturally into the conversation.)");

            return sb.ToString();
        }

        /// <summary>
        /// 获取记忆类型标签
        /// </summary>
        private static string GetMemoryTypeTag(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Conversation:
                    return "Conversation";
                case MemoryType.Action:
                    return "Action";
                case MemoryType.Event:
                    return "Event";
                case MemoryType.Emotion:
                    return "Emotion";
                case MemoryType.Relationship:
                    return "Relationship";
                default:
                    return "Memory";
            }
        }

        /// <summary>
        /// 提取关键词（简化版）
        /// </summary>
        private static List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var keywords = new HashSet<string>();

            // 分词：2-4字
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

            return keywords.Take(15).ToList();
        }

        /// <summary>
        /// 获取诊断信息
        /// </summary>
        public static RecallDiagnostics GetDiagnostics(Pawn pawn)
        {
            var diagnostics = new RecallDiagnostics
            {
                PawnName = pawn?.LabelShort ?? "Unknown",
                IsEnabled = RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings?.enableProactiveRecall ?? false
            };

            var memoryComp = pawn?.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp != null)
            {
                diagnostics.CandidateMemories = memoryComp.SituationalMemories.Count + memoryComp.EventLogMemories.Count;
                
                // 统计高分记忆
                var highScore = memoryComp.SituationalMemories
                    .Concat(memoryComp.EventLogMemories)
                    .Where(m => m.importance > 0.7f)
                    .Count();
                
                diagnostics.HighImportanceMemories = highScore;
            }

            return diagnostics;
        }

        /// <summary>
        /// 诊断信息
        /// </summary>
        public class RecallDiagnostics
        {
            public string PawnName;
            public bool IsEnabled;
            public int CandidateMemories;
            public int HighImportanceMemories;
            public float EstimatedTriggerRate;
        }
    }
}
