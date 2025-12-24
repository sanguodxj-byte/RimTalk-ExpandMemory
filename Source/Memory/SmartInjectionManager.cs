using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 智能注入管理器 v3.3.2.25
    /// 直接使用CommonKnowledgeLibrary和关键词检索
    /// ? 完全移除RAG依赖
    /// ? v3.3.20: 支持指令分区（Instruction Partitioning）
    /// ? v3.3.20: 调整注入顺序 - 规则 → 常识 → 记忆
    /// </summary>
    public static class SmartInjectionManager
    {
        private static int callCount = 0;
        
        /// <summary>
        /// 智能注入上下文
        /// ? v3.3.20: 重构知识注入逻辑，支持指令分区
        /// ? 注入顺序：
        ///   1. Current Guidelines（规则/指令）- 强制约束
        ///   2. World Knowledge（常识/背景）- 世界观知识
        ///   3. Character Memories（记忆）- 角色个人经历
        /// </summary>
        public static string InjectSmartContext(
            Pawn speaker,
            Pawn listener,
            string context,
            int maxMemories = 10,
            int maxKnowledge = 5)
        {
            callCount++;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[SmartInjection] ?? Call #{callCount}: Speaker={speaker?.LabelShort ?? "null"}, Listener={listener?.LabelShort ?? "null"}");
            }
            
            if (speaker == null || string.IsNullOrEmpty(context))
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[SmartInjection] ?? Null input");
                }
                return null;
            }

            try
            {
                var sb = new StringBuilder();
                
                // ? 第一优先级：注入常识（分区为规则和背景知识）
                var memoryManager = Find.World?.GetComponent<MemoryManager>();
                if (memoryManager != null)
                {
                    // 调用InjectKnowledgeWithDetails获取详细的评分信息
                    string knowledgeText = memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
                        context,
                        maxKnowledge,
                        out var knowledgeScores,
                        speaker,
                        listener
                    );
                    
                    if (!string.IsNullOrEmpty(knowledgeText) && knowledgeScores != null && knowledgeScores.Count > 0)
                    {
                        // ? 步骤1：根据标签分类条目
                        var instructionEntries = new List<KnowledgeScore>();
                        var loreEntries = new List<KnowledgeScore>();
                        
                        // 指令标签关键词（行为、指令、规则、System）
                        var instructionTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "行为", "指令", "规则", "System", 
                            "Behavior", "Instruction", "Rule",
                            "行为-", "指令-", "规则-" // 支持前缀匹配（如"行为-战斗"）
                        };
                        
                        foreach (var knowledgeScore in knowledgeScores)
                        {
                            var entry = knowledgeScore.Entry;
                            var tags = entry.GetTags(); // 获取标签列表
                            
                            // 检查是否包含指令标签
                            bool isInstruction = tags.Any(tag => 
                                instructionTags.Contains(tag) || 
                                instructionTags.Any(instructionTag => tag.StartsWith(instructionTag, StringComparison.OrdinalIgnoreCase))
                            );
                            
                            if (isInstruction)
                            {
                                instructionEntries.Add(knowledgeScore);
                            }
                            else
                            {
                                loreEntries.Add(knowledgeScore);
                            }
                        }
                        
                        // ? 步骤2：优先注入指令部分（Current Guidelines）
                        if (instructionEntries.Count > 0)
                        {
                            sb.AppendLine("## Current Guidelines");
                            int index = 1;
                            foreach (var scored in instructionEntries)
                            {
                                var entry = scored.Entry;
                                sb.AppendLine($"{index}. [{entry.tag}] {entry.content}");
                                index++;
                            }
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[SmartInjection]   ? Injected {instructionEntries.Count} instruction entries (Current Guidelines)");
                            }
                        }
                        
                        // ? 步骤3：然后注入背景知识部分（World Knowledge）
                        if (loreEntries.Count > 0)
                        {
                            if (sb.Length > 0)
                                sb.AppendLine();
                            
                            sb.AppendLine("## World Knowledge");
                            int index = 1;
                            foreach (var scored in loreEntries)
                            {
                                var entry = scored.Entry;
                                sb.AppendLine($"{index}. [{entry.tag}] {entry.content}");
                                index++;
                            }
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[SmartInjection]   ?? Injected {loreEntries.Count} lore entries (World Knowledge)");
                            }
                        }
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[SmartInjection]   Total knowledge: {knowledgeScores.Count} entries ({instructionEntries.Count} instructions + {loreEntries.Count} lore)");
                        }
                    }
                }
                
                // ? 第二优先级：注入记忆（放在最后，作为角色个人经历补充）
                var memoryComp = speaker.TryGetComp<FourLayerMemoryComp>();
                if (memoryComp != null)
                {
                    string memoriesText = DynamicMemoryInjection.InjectMemoriesWithDetails(
                        memoryComp, 
                        context, 
                        maxMemories, 
                        out var memoryScores
                    );
                    
                    if (!string.IsNullOrEmpty(memoriesText))
                    {
                        if (sb.Length > 0)
                            sb.AppendLine();
                        
                        sb.AppendLine("## Character Memories");
                        sb.AppendLine(memoriesText);
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[SmartInjection]   ?? Injected {memoryScores.Count} memories");
                        }
                    }
                }
                
                string result = sb.ToString();
                
                // ? v3.3.2.37: 应用提示词规范化规则
                if (!string.IsNullOrEmpty(result))
                {
                    string normalizedResult = PromptNormalizer.Normalize(result);
                    
                    if (Prefs.DevMode && normalizedResult != result)
                    {
                        Log.Message($"[SmartInjection] ? Applied prompt normalization rules");
                        Log.Message($"[SmartInjection]   Original: {result.Substring(0, Math.Min(100, result.Length))}...");
                        Log.Message($"[SmartInjection]   Normalized: {normalizedResult.Substring(0, Math.Min(100, normalizedResult.Length))}...");
                    }
                    
                    result = normalizedResult;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[SmartInjection] ? Success: {result.Length} chars formatted");
                    Log.Message($"[SmartInjection] ?? Injection Order: Guidelines → Knowledge → Memories");
                }
                
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartInjection] ? Exception: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
        
        public static int GetCallCount() => callCount;
    }
}
