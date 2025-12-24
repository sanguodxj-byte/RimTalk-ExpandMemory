using System.Collections.Generic;
using System.Linq;
using RimTalk.Memory;
using RimTalk.MemoryPatch;
using Verse;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// MainTabWindow_Memory 辅助方法（部分类）
    /// </summary>
    public partial class MainTabWindow_Memory
    {
        /// <summary>
        /// ? 通用记忆聚合方法 - 支持AI总结
        /// </summary>
        private void AggregateMemories(
            List<MemoryEntry> memories, 
            MemoryLayer targetLayer,
            List<MemoryEntry> sourceList,
            List<MemoryEntry> targetList,
            string promptTemplate)
        {
            var byType = memories.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var items = typeGroup.ToList();
                
                // 创建聚合条目
                var aggregated = new MemoryEntry(
                    content: targetLayer == MemoryLayer.Archive 
                        ? CreateArchiveSummary(items, typeGroup.Key)
                        : CreateSimpleSummary(items, typeGroup.Key),
                    type: typeGroup.Key,
                    layer: targetLayer,
                    importance: items.Average(m => m.importance) + (targetLayer == MemoryLayer.Archive ? 0.3f : 0.2f)
                );
                
                // 合并元数据
                aggregated.keywords.AddRange(items.SelectMany(m => m.keywords).Distinct());
                aggregated.tags.AddRange(items.SelectMany(m => m.tags).Distinct());
                aggregated.AddTag(targetLayer == MemoryLayer.Archive ? "手动归档" : "手动总结");
                if (targetLayer == MemoryLayer.Archive)
                {
                    aggregated.AddTag($"源自{items.Count}条ELS");
                }
                
                // ? AI总结（如果可用）
                var settings = RimTalkMemoryPatchMod.Settings;
                if (settings.useAISummarization && AI.IndependentAISummarizer.IsAvailable())
                {
                    string cacheKey = AI.IndependentAISummarizer.ComputeCacheKey(selectedPawn, items);
                    
                    AI.IndependentAISummarizer.RegisterCallback(cacheKey, (aiSummary) =>
                    {
                        if (!string.IsNullOrEmpty(aiSummary))
                        {
                            aggregated.content = aiSummary;
                            aggregated.RemoveTag("简单总结");
                            aggregated.RemoveTag("简单归档");
                            aggregated.AddTag(targetLayer == MemoryLayer.Archive ? "AI归档" : "AI总结");
                            aggregated.notes = $"AI {(targetLayer == MemoryLayer.Archive ? "深度归档" : "总结")}已完成";
                        }
                    });
                    
                    AI.IndependentAISummarizer.SummarizeMemories(selectedPawn, items, promptTemplate);
                    
                    aggregated.AddTag("简单" + (targetLayer == MemoryLayer.Archive ? "归档" : "总结"));
                    aggregated.AddTag("待AI更新");
                    aggregated.notes = $"AI {(targetLayer == MemoryLayer.Archive ? "深度归档" : "总结")}正在后台处理中...";
                }
                
                targetList.Insert(0, aggregated);
            }
            
            // 从源列表移除
            foreach (var memory in memories)
            {
                sourceList.Remove(memory);
            }
        }
        
        /// <summary>
        /// 创建简单总结（用于手动总结时的占位符）
        /// </summary>
        private string CreateSimpleSummary(List<MemoryEntry> memories, MemoryType type)
        {
            if (memories == null || memories.Count == 0)
                return "";
            
            var sb = new System.Text.StringBuilder();
            
            if (type == MemoryType.Conversation)
            {
                var byPerson = memories
                    .Where(m => !string.IsNullOrEmpty(m.relatedPawnName))
                    .GroupBy(m => m.relatedPawnName)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in byPerson.Take(5))
                {
                    if (shown > 0) sb.Append("；");
                    sb.Append($"与{group.Key}对话×{group.Count()}");
                    shown++;
                }
                
                if (shown == 0)
                    sb.Append($"对话{memories.Count}次");
            }
            else if (type == MemoryType.Action)
            {
                var grouped = memories
                    .Select(m => m.content.Length > 15 ? m.content.Substring(0, 15) : m.content)
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(3))
                {
                    if (shown > 0) sb.Append("；");
                    sb.Append(group.Count() > 1 ? $"{group.Key}×{group.Count()}" : group.Key);
                    shown++;
                }
            }
            else
            {
                var grouped = memories
                    .GroupBy(m => m.content.Length > 20 ? m.content.Substring(0, 20) : m.content)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(5))
                {
                    if (shown > 0) sb.Append("；");
                    
                    string content = group.First().content;
                    if (content.Length > 40)
                        content = content.Substring(0, 40) + "...";
                    
                    sb.Append(group.Count() > 1 ? $"{content}×{group.Count()}" : content);
                    shown++;
                }
            }
            
            if (sb.Length > 0 && memories.Count > 3)
                sb.Append($"（共{memories.Count}条）");
            
            return sb.Length > 0 ? sb.ToString() : $"{type}记忆{memories.Count}条";
        }
        
        /// <summary>
        /// 创建归档摘要（用于手动归档时的占位符）
        /// </summary>
        private string CreateArchiveSummary(List<MemoryEntry> memories, MemoryType type)
        {
            if (memories == null || memories.Count == 0)
                return "";
            
            var sb = new System.Text.StringBuilder();
            sb.Append($"{(type == MemoryType.Conversation ? "对话" : type == MemoryType.Action ? "行动" : type.ToString())}归档（{memories.Count}条）：");
            
            if (type == MemoryType.Conversation)
            {
                var byPerson = memories
                    .Where(m => !string.IsNullOrEmpty(m.relatedPawnName))
                    .GroupBy(m => m.relatedPawnName)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in byPerson.Take(10))
                {
                    if (shown > 0) sb.Append("；");
                    sb.Append($"与{group.Key}对话×{group.Count()}");
                    shown++;
                }
            }
            else if (type == MemoryType.Action)
            {
                var grouped = memories
                    .Select(m => m.content.Length > 20 ? m.content.Substring(0, 20) : m.content)
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(5))
                {
                    if (shown > 0) sb.Append("；");
                    sb.Append(group.Count() > 1 ? $"{group.Key}×{group.Count()}" : group.Key);
                    shown++;
                }
            }
            else
            {
                var grouped = memories
                    .GroupBy(m => m.content.Length > 30 ? m.content.Substring(0, 30) : m.content)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(8))
                {
                    if (shown > 0) sb.Append("；");
                    
                    string content = group.First().content;
                    if (content.Length > 60)
                        content = content.Substring(0, 60) + "...";
                    
                    sb.Append(group.Count() > 1 ? $"{content}×{group.Count()}" : content);
                    shown++;
                }
            }
            
            return sb.ToString();
        }
    }
}
