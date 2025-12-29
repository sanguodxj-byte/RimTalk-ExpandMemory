using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 智能注入管理器 v3.3.38
    /// 直接使用CommonKnowledgeLibrary和关键词检索
    /// ⭐ 完全移除RAG依赖
    /// ⭐ v3.3.20: 支持指令分区（Instruction Partitioning）
    /// ⭐ v3.3.20: 调整注入顺序 - 规则 → 常识 → 记忆
    /// ⭐ v3.3.38: 注入位置改为 context（用户消息）而非 prompts（系统提示）
    ///
    /// 设计理念：
    /// - 记忆和常识注入到用户消息末尾，作为对话上下文补充
    /// - 保持 system prompt 简洁，只包含角色设定和对话规则
    /// - 提高 AI 对上下文信息的敏感度
    /// </summary>
    public static class SmartInjectionManager
    {
        private static int callCount = 0;
        
        /// <summary>
        /// 智能注入上下文
        /// ⭐ v3.3.38: 返回格式化的上下文文本，用于注入到用户消息（context）
        /// ⭐ 注入顺序：
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
                        
                        // ? 步骤2：优先注入指令部分（Current Guidelines）- 使用 XML 格式
                        if (instructionEntries.Count > 0)
                        {
                            sb.AppendLine("## Current Guidelines");
                            int index = 1;
                            foreach (var scored in instructionEntries)
                            {
                                var entry = scored.Entry;
                                // 使用 XML 格式标签: <Tag>content</Tag>
                                sb.AppendLine($"{index}. <{entry.tag}>{entry.content}</{entry.tag}>");
                                index++;
                            }
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[SmartInjection]   ? Injected {instructionEntries.Count} instruction entries (Current Guidelines) with XML format");
                            }
                        }
                        
                        // ? 步骤3：然后注入背景知识部分（World Knowledge）- 使用 XML 格式
                        if (loreEntries.Count > 0)
                        {
                            if (sb.Length > 0)
                                sb.AppendLine();
                            
                            sb.AppendLine("## World Knowledge");
                            int index = 1;
                            foreach (var scored in loreEntries)
                            {
                                var entry = scored.Entry;
                                // 使用 XML 格式标签: <Tag>content</Tag>
                                sb.AppendLine($"{index}. <{entry.tag}>{entry.content}</{entry.tag}>");
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
