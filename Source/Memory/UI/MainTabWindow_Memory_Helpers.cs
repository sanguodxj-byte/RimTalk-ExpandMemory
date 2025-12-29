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
        /// ⭐ 通用记忆聚合方法 - 支持AI总结
        /// ⭐ v3.3.2.35: 修复时间戳继承和插入位置问题
        /// </summary>
        private void AggregateMemories(
            List<MemoryEntry> memories, 
            MemoryLayer targetLayer,
            List<MemoryEntry> sourceList,
            List<MemoryEntry> targetList,
            string promptTemplate)
        {
            // ⭐ 修复：过滤掉固定记忆（不应该被总结）
            var memoriesToSummarize = memories.Where(m => !m.isPinned).ToList();
            
            if (memoriesToSummarize.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("[Memory] AggregateMemories: All selected memories are pinned, skipping summarization");
                }
                return;
            }
            
            var byType = memoriesToSummarize.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var items = typeGroup.ToList();
                
                // ⭐ 修复：使用被总结记忆中最晚（最新）的timestamp
                int latestTimestamp = items.Max(m => m.timestamp);
                
                // 创建聚合条目
                var aggregated = new MemoryEntry(
                    content: targetLayer == MemoryLayer.Archive 
                        ? CreateArchiveSummary(items, typeGroup.Key)
                        : CreateSimpleSummary(items, typeGroup.Key),
                    type: typeGroup.Key,
                    layer: targetLayer,
                    importance: items.Average(m => m.importance) + (targetLayer == MemoryLayer.Archive ? 0.3f : 0.2f)
                );
                
                // ⭐ 修复：覆盖默认的timestamp（MemoryEntry构造函数会自动设置为当前时间）
                aggregated.timestamp = latestTimestamp;
                
                // 合并元数据
                aggregated.keywords.AddRange(items.SelectMany(m => m.keywords).Distinct());
                aggregated.tags.AddRange(items.SelectMany(m => m.tags).Distinct());
                aggregated.AddTag(targetLayer == MemoryLayer.Archive ? "手动归档" : "选中总结");
                if (targetLayer == MemoryLayer.Archive)
                {
                    aggregated.AddTag($"源自{items.Count}条ELS");
                }
                
                // ⭐ AI总结（如果可用）
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
                
                // ⭐ 修复：根据时间戳插入到正确位置，而不是总是插入到开头
                InsertMemoryByTimestamp(targetList, aggregated);
            }
            
            // ⭐ 修复：只从源列表中移除非固定记忆
            foreach (var memory in memoriesToSummarize)
            {
                sourceList.Remove(memory);
            }
        }
        
        /// <summary>
        /// ⭐ 新方法：根据时间戳将记忆插入到正确的位置（保持列表按时间降序排序）
        /// </summary>
        private void InsertMemoryByTimestamp(List<MemoryEntry> list, MemoryEntry entry)
        {
            // 如果列表为空，直接添加
            if (list.Count == 0)
            {
                list.Add(entry);
                return;
            }

            // 使用二分查找找到插入位置（降序排列，新的在前）
            int insertIndex = list.FindIndex(m => m.timestamp < entry.timestamp);
            
            // 如果没找到（所有记忆都比新记忆新），添加到末尾
            if (insertIndex == -1)
            {
                list.Add(entry);
            }
            else
            {
                list.Insert(insertIndex, entry);
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