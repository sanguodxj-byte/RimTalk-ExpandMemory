using RimTalk.Memory.Capture;
using RimTalk.Memory.Maintenance;
using RimTalk.MemoryPatch;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 四层记忆系统核心组件
    /// ABM -> SCM -> ELS -> CLPA
    /// </summary>
    public class FourLayerMemoryComp : ThingComp
    {
        // 核心记忆存储
        private List<MemoryEntry> activeMemories = new();      // ABM: 完整对话记录，无容量限制，总结后转 ELS
        private List<MemoryEntry> situationalMemories = new(); // SCM: 固定后的轮次记忆ABM，或是非轮次记忆的过渡层
        private List<MemoryEntry> eventLogMemories = new();    // ELS: 总结后的记忆，~50条
        private List<MemoryEntry> archiveMemories = new();     // CLPA: 归档后的记忆，无容量限制

        // 直接持有工作记忆捕获模块
        private readonly JobMemoryCapturer _jobCapturer;

        // 机械维护器：执行衰减、清理、容量治理与直接迁移
        private readonly MemoryMaintainer _maintainer;

        // 属性访问
        /// <summary>
        /// ABM: 完整对话记录，无容量限制，总结后转 ELS
        /// </summary>
        public List<MemoryEntry> ActiveMemories => activeMemories;

        /// <summary>
        /// SCM: 固定后的轮次记忆ABM，或是非轮次记忆的过渡层
        /// </summary>
        public List<MemoryEntry> SituationalMemories => situationalMemories;

        /// <summary>
        /// ELS: 总结后的记忆
        /// </summary>
        public List<MemoryEntry> EventLogMemories => eventLogMemories;

        /// <summary>
        /// CLPA: 归档后的记忆
        /// </summary>
        public List<MemoryEntry> ArchiveMemories => archiveMemories;

        /// <summary>
        /// 工作记忆捕获模块，负责捕获工作相关的记忆并存入 ABM
        /// </summary>
        public JobMemoryCapturer JobCapturer => _jobCapturer;

        /// <summary>
        /// 机械维护器，负责衰减、清理、容量治理与直接层级迁移
        /// </summary>
        public MemoryMaintainer Maintainer => _maintainer;

        // 配置项（从设置中读取）
        public static bool IsRoundMemoryEnabled => RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? false;

        // 构造函数，初始化捕获模块和维护器
        public FourLayerMemoryComp()
        {
            _jobCapturer = new JobMemoryCapturer(this);
            _maintainer = new MemoryMaintainer(this);
        }


        // 存档读写
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref activeMemories, "activeMemories", LookMode.Deep); // label建议使用大写开头。但此处屎山已成
            Scribe_Collections.Look(ref situationalMemories, "situationalMemories", LookMode.Deep);
            Scribe_Collections.Look(ref eventLogMemories, "eventLogMemories", LookMode.Deep);
            Scribe_Collections.Look(ref archiveMemories, "archiveMemories", LookMode.Deep);

            // 集合空保护
            activeMemories ??= new();
            situationalMemories ??= new();
            eventLogMemories ??= new();
            archiveMemories ??= new();
        }

        /// <summary>
        /// 低频 Tick：每小时衰减 + 清理低活跃，每天执行容量治理。
        /// 通过 <see cref="Pawn.IsHashIntervalTick"/> 哈希分散，避免全局集中执行。
        /// </summary>
        public override void CompTickRare()
        {
            base.CompTickRare();

            // 每小时衰减 + 迁移/清理 ABM + 清理低活跃记忆
            if (parent.IsHashIntervalTick(GenDate.TicksPerHour))
            {
                _maintainer.RunDecay();
                _maintainer.ConvertActiveMemories();
                _maintainer.CleanupLowActivityMemories();
            }

            // 每天执行容量治理
            if (parent.IsHashIntervalTick(GenDate.TicksPerDay))
            {
                _maintainer.EnforceMemoryLimits();
            }
        }

        public void DailySummarization()
        {
            // ⭐ 修复：同时检查ABM和SCM是否有内容
            if (activeMemories.Count == 0 && situationalMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            // ⭐ 修复：合并ABM和SCM作为总结池，排除总结过的记忆（即旧的固定记忆）
            var allMemoriesToSummarize = new List<MemoryEntry>();
            allMemoriesToSummarize.AddRange(activeMemories.Where(m => m.CanBeSummarized));
            allMemoriesToSummarize.AddRange(situationalMemories.Where(m => m.CanBeSummarized));

            // 如果没有未总结过的记忆，不需要总结
            if (allMemoriesToSummarize.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} daily summarization: no non-pinned memories to summarize");
                }
                return;
            }

            // MemoryType.Conversation即总结得到的ELS的记忆类型，可以根据需要调整为其他类型，建议改为总结独有类型
            var byType = allMemoriesToSummarize.GroupBy(m => MemoryType.Conversation);

            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string simpleSummary = CreateSimpleSummary(memories, typeGroup.Key);

                // ⭐ 修复：使用被总结记忆中最晚（最新）的timestamp作为总结的时间戳
                int latestTimestamp = memories.Max(m => m.GameTick);

                var summaryEntry = new MemoryEntry(
                    content: simpleSummary,
                    type: typeGroup.Key,
                    layer: MemoryLayer.EventLog,
                    importance: memories.Average(m => m.Importance) + 0.2f
                );

                // ⭐ 修复：覆盖默认的timestamp（MemoryEntry构造函数会自动设置为当前时间）
                summaryEntry.GameTick = latestTimestamp;

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
                            summaryEntry.Content = aiSummary;
                            summaryEntry.RemoveTag("简单总结");
                            summaryEntry.AddTag("AI总结");
                            summaryEntry.Notes = "AI 总结已于后台完成并自动更新。";
                        }
                    });

                    AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "daily_summary");

                    summaryEntry.AddTag("待AI更新");
                    summaryEntry.Notes = "AI 总结正在后台处理中...";
                }

                // 按时间排序的义务已转交至 UI 端
                EventLogMemories.Add(summaryEntry);
            }

            foreach (var memory in allMemoriesToSummarize)
            {
                if (memory != null) memory.IsSummarized = true; // 标记为已总结
            }

            // ⭐ 修复：清空ABM（总结后不再需要保留）
            activeMemories.Clear();

            // ⭐ 修复：清空SCM（移除 isUserEdited 检查，只保留固定记忆）
            int beforeCount = situationalMemories.Count;
            situationalMemories.RemoveAll(m => !m.IsPinned);
            int removedCount = beforeCount - situationalMemories.Count;

            if (Prefs.DevMode && removedCount > 0)
            {
                Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} daily summarization: " +
                           $"cleared ABM, removed {removedCount} SCM, kept {situationalMemories.Count} pinned");
            }
        }

        // 经过艰辛的排查，终于确定此方法用于【一键总结所有殖民者】
        public void ManualSummarization()
        {
            // ⭐ 修复：同时检查ABM和SCM是否有内容
            if (activeMemories.Count == 0 && situationalMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            // ⭐ 修复：合并ABM和SCM作为总结池，排除总结过的记忆（即旧的固定记忆）
            var allMemoriesToSummarize = new List<MemoryEntry>();
            allMemoriesToSummarize.AddRange(activeMemories.Where(m => m.CanBeSummarized));
            allMemoriesToSummarize.AddRange(situationalMemories.Where(m => m.CanBeSummarized));

            // 如果没有非固定记忆，不需要总结
            if (allMemoriesToSummarize.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} manual summarization: no non-pinned memories to summarize");
                }
                return;
            }

            // MemoryType.Conversation即总结得到的ELS的记忆类型，可以根据需要调整为其他类型，建议改为总结独有类型
            var byType = allMemoriesToSummarize.GroupBy(m => MemoryType.Conversation);

            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string simpleSummary = CreateSimpleSummary(memories, typeGroup.Key);

                // ⭐ 修复：使用被总结记忆中最晚（最新）的timestamp作为总结的时间戳
                int latestTimestamp = memories.Max(m => m.GameTick);

                var summaryEntry = new MemoryEntry(
                    content: simpleSummary,
                    type: typeGroup.Key,
                    layer: MemoryLayer.EventLog,
                    importance: memories.Average(m => m.Importance) + 0.2f
                );

                // ⭐ 修复：覆盖默认的timestamp
                summaryEntry.GameTick = latestTimestamp;

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
                            summaryEntry.Content = aiSummary;
                            summaryEntry.RemoveTag("简单总结");
                            summaryEntry.AddTag("AI总结");
                            summaryEntry.Notes = "AI 总结已于后台完成并自动更新。";
                        }
                    });

                    AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "daily_summary");

                    summaryEntry.AddTag("待AI更新");
                    summaryEntry.Notes = "AI 总结正在后台处理中...";
                }

                // ⭐ 修复：根据时间戳插入到正确位置，而不是总是插入到开头
                // 按时间排序的义务已转交至 UI 端
                EventLogMemories.Add(summaryEntry);
            }

            foreach (var memory in allMemoriesToSummarize)
            {
                if (memory != null) memory.IsSummarized = true; // 标记为已总结
            }

            // ⭐ 修复：清空ABM（总结后不再需要保留）
            activeMemories.Clear();

            // ⭐ 修复：清空SCM（移除 isUserEdited 检查，只保留固定记忆）
            int beforeCount = situationalMemories.Count;
            situationalMemories.RemoveAll(m => !m.IsPinned);
            int removedCount = beforeCount - situationalMemories.Count;

            if (Prefs.DevMode && removedCount > 0)
            {
                Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} manual summarization: " +
                           $"cleared ABM, removed {removedCount} SCM, kept {situationalMemories.Count} pinned");
            }
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
                    string action = m.Content.Length > 15 ? m.Content.Substring(0, 15) : m.Content;
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
                    .GroupBy(m => m.Content.Length > 20 ? m.Content.Substring(0, 20) : m.Content)
                    .OrderByDescending(g => g.Count());

                int shown = 0;
                foreach (var group in grouped.Take(5))
                {
                    if (shown > 0) summary.Append("；");

                    string content = group.First().Content;
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

        /// <summary>
        /// ⭐ v4.0: 更新检索逻辑
        /// - ABM: 按 conversationId 去重后返回所有
        /// - SCM: 仅兼容旧存档，返回已有的
        /// - ELS/CLPA: 保持原有逻辑
        /// </summary>
        public List<MemoryEntry> RetrieveMemories(MemoryQuery query)
        {
            var results = new List<MemoryEntry>();

            // ⭐ v4.0: ABM 无容量限制，返回所有匹配的
            var abmCandidates = activeMemories
                .Where(m => MatchesQuery(m, query))
                .OrderByDescending(m => m.GameTick);
            results.AddRange(abmCandidates);

            // ⭐ v4.0: SCM 仅兼容旧存档（不再生成新的）
            if (situationalMemories.Count > 0)
            {
                var scmCandidates = situationalMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                    .ThenBy(m => m.Id, StringComparer.Ordinal)
                    .Take(5);
                results.AddRange(scmCandidates);
            }

            if (query.includeContext && results.Count < query.maxCount)
            {
                // ⭐ v3.3.2.29: ELS 候选 - 确定性排序（分数降序 + ID 升序）
                var elsCandidates = eventLogMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                    .ThenBy(m => m.Id, StringComparer.Ordinal)
                    .Take(query.maxCount - results.Count);
                results.AddRange(elsCandidates);
            }

            if (query.layer == MemoryLayer.Archive)
            {
                // ⭐ v3.3.2.29: CLPA 候选 - 确定性排序（重要性降序 + ID 升序）
                var clpaCandidates = archiveMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.Importance)
                    .ThenBy(m => m.Id, StringComparer.Ordinal)
                    .Take(3);
                results.AddRange(clpaCandidates);
            }

            return results.Take(query.maxCount).ToList();
        }

        private bool MatchesQuery(MemoryEntry memory, MemoryQuery query)
        {
            if (query.type.HasValue && memory.Type != query.type.Value)
                return false;

            if (query.layer.HasValue && memory.Layer != query.layer.Value)
                return false;

            if (!string.IsNullOrEmpty(query.relatedPawn) && memory.relatedPawnName != query.relatedPawn)
                return false;

            if (query.tags.Any() && !query.tags.Any(t => memory.tags.Contains(t)))
                return false;

            return true;
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

        // 此方法未正确处理固定的记忆
        public void ManualArchive()
        {
            if (eventLogMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            var byType = eventLogMemories.GroupBy(m => m.Type);

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
                        importance: memories.Average(m => m.Importance) + 0.3f
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


        // 注入层相关，待后续分离解耦
        // 兼容旧API：GetMemoryContext
        public string GetMemoryContext(int count = 5)
        {
            var query = new MemoryQuery
            {
                maxCount = count,
                includeContext = true
            };

            var memories = RetrieveMemories(query);
            var context = new StringBuilder();

            foreach (var memory in memories)
            {
                context.AppendLine($"- [{memory.TypeName}] {memory.Content} ({memory.AgeString})");
            }

            return context.ToString();
        }

        // 兼容旧API：GetRelevantMemories
        public List<MemoryEntry> GetRelevantMemories(int count = 5)
        {
            var query = new MemoryQuery
            {
                maxCount = count,
                includeContext = true
            };

            return RetrieveMemories(query);
        }
    }

}
