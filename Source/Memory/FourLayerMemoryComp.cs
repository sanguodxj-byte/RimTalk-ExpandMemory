using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 四层记忆系统核心组件
    /// ABM -> SCM -> ELS -> CLPA
    /// </summary>
    public class FourLayerMemoryComp : ThingComp
    {
        // 四层记忆存储
        private List<MemoryEntry> activeMemories = new List<MemoryEntry>();      // ABM: 2-3条
        private List<MemoryEntry> situationalMemories = new List<MemoryEntry>(); // SCM: ~20条
        private List<MemoryEntry> eventLogMemories = new List<MemoryEntry>();    // ELS: ~50条
        private List<MemoryEntry> archiveMemories = new List<MemoryEntry>();     // CLPA: 无限制

        // 容量限制（从设置中读取）
        private int MAX_ACTIVE => RimTalkMemoryPatchMod.Settings.maxActiveMemories;
        private int MAX_SITUATIONAL => RimTalkMemoryPatchMod.Settings.maxSituationalMemories;
        private int MAX_EVENTLOG => RimTalkMemoryPatchMod.Settings.maxEventLogMemories;
        // CLPA 无限制

        public List<MemoryEntry> ActiveMemories => activeMemories;
        public List<MemoryEntry> SituationalMemories => situationalMemories;
        public List<MemoryEntry> EventLogMemories => eventLogMemories;
        public List<MemoryEntry> ArchiveMemories => archiveMemories;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref activeMemories, "activeMemories", LookMode.Deep);
            Scribe_Collections.Look(ref situationalMemories, "situationalMemories", LookMode.Deep);
            Scribe_Collections.Look(ref eventLogMemories, "eventLogMemories", LookMode.Deep);
            Scribe_Collections.Look(ref archiveMemories, "archiveMemories", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activeMemories == null) activeMemories = new List<MemoryEntry>();
                if (situationalMemories == null) situationalMemories = new List<MemoryEntry>();
                if (eventLogMemories == null) eventLogMemories = new List<MemoryEntry>();
                if (archiveMemories == null) archiveMemories = new List<MemoryEntry>();
            }
        }
        
        /// <summary>
        /// ⭐ 修复3: 定期检查并清理工作会话（Pawn死亡/离开时）
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            
            // 每5秒检查一次
            if (Find.TickManager.TicksGame % 300 == 0)
            {
                var pawn = parent as Pawn;
                if (pawn != null)
                {
                    // 如果Pawn死亡或不在地图上，强制结束工作会话
                    if (pawn.Dead || !pawn.Spawned || pawn.Map == null)
                    {
                        WorkSessionAggregator.ForceEndSession(pawn);
                    }
                }
            }
        }

        /// <summary>
        /// 添加记忆到超短期记忆（ABM）
        /// ⭐ v3.3.2.25: 移除向量化同步
        /// </summary>
        public void AddActiveMemory(string content, MemoryType type, float importance = 1f, string relatedPawn = null)
        {
            // 去重检查
            if (IsDuplicateMemory(content, relatedPawn, type))
            {
                if (Prefs.DevMode)
                {
                    var pawn = parent as Pawn;
                    string pawnLabel = pawn?.LabelShort ?? "Unknown";
                    Log.Message($"[Memory] Skipped duplicate memory for {pawnLabel}: {content.Substring(0, Math.Min(50, content.Length))}...");
                }
                return;
            }
            
            var memory = new MemoryEntry(content, type, MemoryLayer.Active, importance, relatedPawn);
            
            ExtractKeywords(memory);
            activeMemories.Insert(0, memory);

            // ⭐ v3.3.2.25: VectorDB已移除，不再需要异步同步
            // 记忆现在完全通过关键词检索，无需向量化

            // 超短期记忆满了，转移到短期
            if (activeMemories.Count > MAX_ACTIVE)
            {
                var oldest = activeMemories[activeMemories.Count - 1];
                activeMemories.RemoveAt(activeMemories.Count - 1);
                PromoteToSituational(oldest);
            }
        }
        
        private bool IsDuplicateMemory(string content, string relatedPawn, MemoryType type)
        {
            if (string.IsNullOrEmpty(content))
                return false;
            
            foreach (var memory in activeMemories)
            {
                if (memory.type == type && memory.content == content && memory.relatedPawnName == relatedPawn)
                    return true;
            }
            
            int checkCount = Math.Min(5, situationalMemories.Count);
            for (int i = 0; i < checkCount; i++)
            {
                var memory = situationalMemories[i];
                if (memory.type == type && memory.content == content && memory.relatedPawnName == relatedPawn)
                    return true;
            }
            
            return false;
        }

        private void PromoteToSituational(MemoryEntry memory)
        {
            memory.layer = MemoryLayer.Situational;
            situationalMemories.Insert(0, memory);

            if (situationalMemories.Count > MAX_SITUATIONAL * 1.5f)
            {
                Log.Warning($"[Memory] {parent.LabelShort} SCM overflow ({situationalMemories.Count}), needs summarization");
            }
        }

        public void DailySummarization()
        {
            if (situationalMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            var byType = situationalMemories.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string simpleSummary = CreateSimpleSummary(memories, typeGroup.Key);

                var summaryEntry = new MemoryEntry(
                    content: simpleSummary,
                    type: typeGroup.Key,
                    layer: MemoryLayer.EventLog,
                    importance: memories.Average(m => m.importance) + 0.2f
                );

                summaryEntry.keywords.AddRange(memories.SelectMany(m => m.keywords).Distinct());
                summaryEntry.tags.AddRange(memories.SelectMany(m => m.tags).Distinct());
                summaryEntry.AddTag("简单总结");

                if (RimTalkMemoryPatchMod.Settings.useAISummarization && AI.IndependentAISummarizer.IsAvailable())
                {
                    string cacheKey = AI.IndependentAISummarizer.ComputeCacheKey(pawn, memories);
                    
                    AI.IndependentAISummarizer.RegisterCallback(cacheKey, (aiSummary) =>
                    {
                        if (!string.IsNullOrEmpty(aiSummary))
                        {
                            summaryEntry.content = aiSummary;
                            summaryEntry.RemoveTag("简单总结");
                            summaryEntry.AddTag("AI总结");
                            summaryEntry.notes = "AI 总结已于后台完成并自动更新。";
                        }
                    });

                    AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "daily_summary");
                    
                    summaryEntry.AddTag("待AI更新");
                    summaryEntry.notes = "AI 总结正在后台处理中...";
                }

                eventLogMemories.Insert(0, summaryEntry);
            }

            situationalMemories.Clear();
            TrimEventLog();
        }

        public void ManualSummarization()
        {
            // 手动总结：与DailySummarization相同，也使用AI（如果启用）
            if (situationalMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            var byType = situationalMemories.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string simpleSummary = CreateSimpleSummary(memories, typeGroup.Key);

                var summaryEntry = new MemoryEntry(
                    content: simpleSummary,
                    type: typeGroup.Key,
                    layer: MemoryLayer.EventLog,
                    importance: memories.Average(m => m.importance) + 0.2f
                );

                summaryEntry.keywords.AddRange(memories.SelectMany(m => m.keywords).Distinct());
                summaryEntry.tags.AddRange(memories.SelectMany(m => m.tags).Distinct());
                summaryEntry.AddTag("手动总结");

                // ⭐ 修改：手动总结也使用AI（如果启用）
                if (RimTalkMemoryPatchMod.Settings.useAISummarization && AI.IndependentAISummarizer.IsAvailable())
                {
                    string cacheKey = AI.IndependentAISummarizer.ComputeCacheKey(pawn, memories);
                    
                    AI.IndependentAISummarizer.RegisterCallback(cacheKey, (aiSummary) =>
                    {
                        if (!string.IsNullOrEmpty(aiSummary))
                        {
                            summaryEntry.content = aiSummary;
                            summaryEntry.RemoveTag("简单总结");
                            summaryEntry.AddTag("AI总结");
                            summaryEntry.notes = "AI 总结已于后台完成并自动更新。";
                        }
                    });

                    AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "daily_summary");
                    
                    summaryEntry.AddTag("待AI更新");
                    summaryEntry.notes = "AI 总结正在后台处理中...";
                }

                eventLogMemories.Insert(0, summaryEntry);
            }

            situationalMemories.Clear();
            TrimEventLog();
        }

        private string CreateSimpleSummary(List<MemoryEntry> memories, MemoryType type)
        {
            if (memories == null || memories.Count == 0)
                return null;

            var summary = new StringBuilder();
            
            if (type == MemoryType.Conversation)
            {
                var byPerson = memories
                    .Where(m => !string.IsNullOrEmpty(m.relatedPawnName))
                    .GroupBy(m => m.relatedPawnName)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in byPerson.Take(5))
                {
                    if (shown > 0) summary.Append("；");
                    summary.Append($"与{group.Key}对话×{group.Count()}");
                    shown++;
                }
                
                if (shown == 0)
                {
                    summary.Append($"对话{memories.Count}次");
                }
            }
            else if (type == MemoryType.Action)
            {
                var actions = new List<string>();
                foreach (var m in memories)
                {
                    string action = m.content.Length > 15 ? m.content.Substring(0, 15) : m.content;
                    actions.Add(action);
                }
                
                var grouped = actions
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(3))
                {
                    if (shown > 0) summary.Append("；");
                    if (group.Count() > 1)
                    {
                        summary.Append($"{group.Key}×{group.Count()}");
                    }
                    else
                    {
                        summary.Append(group.Key);
                    }
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
                    if (shown > 0) summary.Append("；");
                    
                    string content = group.First().content;
                    if (content.Length > 40)
                        content = content.Substring(0, 40) + "...";
                    
                    if (group.Count() > 1)
                    {
                        summary.Append($"{content}×{group.Count()}");
                    }
                    else
                    {
                        summary.Append(content);
                    }
                    shown++;
                }
            }

            if (summary.Length > 0 && memories.Count > 3)
            {
                summary.Append($"（共{memories.Count}条）");
            }

            return summary.Length > 0 ? summary.ToString() : $"{type}记忆{memories.Count}条";
        }

        private void TrimEventLog()
        {
            if (eventLogMemories.Count <= MAX_EVENTLOG)
                return;

            while (eventLogMemories.Count > MAX_EVENTLOG)
            {
                var oldest = eventLogMemories[eventLogMemories.Count - 1];
                eventLogMemories.RemoveAt(eventLogMemories.Count - 1);
                
                oldest.layer = MemoryLayer.Archive;
                archiveMemories.Insert(0, oldest);
            }
        }

        private void ExtractKeywords(MemoryEntry memory)
        {
            if (string.IsNullOrEmpty(memory.content))
                return;

            var words = memory.content
                .Split(new[] { ' ', '，', '。', '、', '；', '：', '-', '×' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1)
                .Distinct()
                .Take(10);

            foreach (var word in words)
            {
                memory.AddKeyword(word);
            }
        }

        public void DecayActivity()
        {
            float scmRate = RimTalkMemoryPatchMod.Settings.scmDecayRate;
            float elsRate = RimTalkMemoryPatchMod.Settings.elsDecayRate;
            float clpaRate = RimTalkMemoryPatchMod.Settings.clpaDecayRate;

            foreach (var memory in situationalMemories)
                memory.Decay(scmRate);

            foreach (var memory in eventLogMemories)
                memory.Decay(elsRate);

            foreach (var memory in archiveMemories)
                memory.Decay(clpaRate);
        }

        public List<MemoryEntry> RetrieveMemories(MemoryQuery query)
        {
            var results = new List<MemoryEntry>();
            
            results.AddRange(activeMemories.Take(MAX_ACTIVE));
            
            var scmCandidates = situationalMemories
                .Where(m => MatchesQuery(m, query))
                .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                .Take(5);
            results.AddRange(scmCandidates);
            
            if (query.includeContext && results.Count < query.maxCount)
            {
                var elsCandidates = eventLogMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                    .Take(query.maxCount - results.Count);
                results.AddRange(elsCandidates);
            }
            
            if (query.layer == MemoryLayer.Archive)
            {
                var clpaCandidates = archiveMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.importance)
                    .Take(3);
                results.AddRange(clpaCandidates);
            }

            return results.Take(query.maxCount).ToList();
        }
        
        private bool MatchesQuery(MemoryEntry memory, MemoryQuery query)
        {
            if (query.type.HasValue && memory.type != query.type.Value)
                return false;

            if (query.layer.HasValue && memory.layer != query.layer.Value)
                return false;

            if (!string.IsNullOrEmpty(query.relatedPawn) && memory.relatedPawnName != query.relatedPawn)
                return false;

            if (query.tags.Any() && !query.tags.Any(t => memory.tags.Contains(t)))
                return false;

            return true;
        }

        public void EditMemory(string memoryId, string newContent, string notes = null)
        {
            var memory = FindMemoryById(memoryId);
            if (memory != null)
            {
                memory.content = newContent;
                memory.isUserEdited = true;
                if (!string.IsNullOrEmpty(notes))
                    memory.notes = notes;
            }
        }

        public void PinMemory(string memoryId, bool pinned)
        {
            var memory = FindMemoryById(memoryId);
            if (memory != null)
            {
                memory.isPinned = pinned;
            }
        }

        public void DeleteMemory(string memoryId)
        {
            activeMemories.RemoveAll(m => m.id == memoryId);
            situationalMemories.RemoveAll(m => m.id == memoryId);
            eventLogMemories.RemoveAll(m => m.id == memoryId);
            archiveMemories.RemoveAll(m => m.id == memoryId);
        }

        private MemoryEntry FindMemoryById(string id)
        {
            return activeMemories.FirstOrDefault(m => m.id == id)
                ?? situationalMemories.FirstOrDefault(m => m.id == id)
                ?? eventLogMemories.FirstOrDefault(m => m.id == id)
                ?? archiveMemories.FirstOrDefault(m => m.id == id);
        }

        public List<MemoryEntry> GetAllMemories()
        {
            var all = new List<MemoryEntry>();
            all.AddRange(activeMemories);
            all.AddRange(situationalMemories);
            all.AddRange(eventLogMemories);
            all.AddRange(archiveMemories);
            return all;
        }

        public void ManualArchive()
        {
            if (eventLogMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            var byType = eventLogMemories.GroupBy(m => m.type);
            
            int archivedCount = 0;
            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string archiveSummary = AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "deep_archive");

                if (!string.IsNullOrEmpty(archiveSummary))
                {
                    var archiveEntry = new MemoryEntry(
                        content: archiveSummary,
                        type: typeGroup.Key,
                        layer: MemoryLayer.Archive,
                        importance: memories.Average(m => m.importance) + 0.3f
                    );

                    archiveEntry.AddTag("手动归档");
                    archiveEntry.AddTag($"源自{memories.Count}条ELS");
                    archiveMemories.Insert(0, archiveEntry);
                    archivedCount++;
                }
            }
            
            if (archivedCount > 0)
            {
                eventLogMemories.Clear();
                Log.Message($"[Memory] {parent.LabelShort} manual archive: {archivedCount} entries");
            }
        }
    }
}
