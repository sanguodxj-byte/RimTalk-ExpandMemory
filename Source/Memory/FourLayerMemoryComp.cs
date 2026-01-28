using RimTalk.Memory.UI;
using RimTalk.MemoryPatch;
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
        // ⭐ v4.0: 三层记忆存储（移除 SCM）
        // ABM: 完整对话记录，无容量限制，总结后转 ELS
        // SCM: 已废弃，保留列表仅为兼容旧存档
        // ELS: 总结后的记忆
        // CLPA: 归档
        private List<MemoryEntry> activeMemories = new List<MemoryEntry>();      // ABM: 无限制
        private List<MemoryEntry> situationalMemories = new List<MemoryEntry>(); // SCM: ⭐ 已废弃，仅兼容旧存档
        private List<MemoryEntry> eventLogMemories = new List<MemoryEntry>();    // ELS: ~50条
        private List<MemoryEntry> archiveMemories = new List<MemoryEntry>();     // CLPA: 无限制

        // 容量限制（从设置中读取）
        // ⭐ v4.0: 移除 MAX_ACTIVE（ABM 无限制）
        // ⭐ v4.0: MAX_SITUATIONAL 仅用于兼容旧存档的显示
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
            AddActiveMemoryInternal(content, type, importance, relatedPawn, null);
        }
        
        /// <summary>
        /// ⭐ v4.0: 添加带有 conversationId 的记忆（用于跨Pawn去重）
        /// </summary>
        public void AddActiveMemoryWithConversationId(string content, MemoryType type, string conversationId, float importance = 1f, string relatedPawn = null)
        {
            AddActiveMemoryInternal(content, type, importance, relatedPawn, conversationId);
        }
        
        /// <summary>
        /// ⭐ v4.0: 内部方法，实际处理记忆添加
        /// ⭐ v4.0: ABM 无容量限制，不再转移到 SCM
        /// </summary>
        private void AddActiveMemoryInternal(string content, MemoryType type, float importance, string relatedPawn, string conversationId)
        {
            // 去重检查（基于 conversationId 优先，否则用内容）
            if (!string.IsNullOrEmpty(conversationId))
            {
                // ⭐ v4.0: 如果有 conversationId，用 conversationId 去重
                if (activeMemories.Any(m => m.conversationId == conversationId))
                {
                    if (Prefs.DevMode)
                    {
                        var pawn = parent as Pawn;
                        string pawnLabel = pawn?.LabelShort ?? "Unknown";
                        Log.Message($"[Memory] Skipped duplicate conversation for {pawnLabel}: {conversationId}");
                    }
                    return;
                }
            }
            else if (IsDuplicateMemory(content, relatedPawn, type))
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
            
            // ⭐ v4.0: 设置 conversationId（如果有）
            if (!string.IsNullOrEmpty(conversationId))
            {
                memory.conversationId = conversationId;
            }
            
            ExtractKeywords(memory);
            activeMemories.Insert(0, memory);

            // ⭐ v4.0: ABM 无容量限制，不再转移到 SCM
            // 总结时会统一处理 ABM，生成 ELS
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

        // ⭐ v4.0: 已删除 PromoteToSituational 方法
        // SCM 层已废弃，ABM 总结后直接转 ELS

        public void DailySummarization()
        {
            // ⭐ 修复：同时检查ABM和SCM是否有内容
            if (activeMemories.Count == 0 && situationalMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            // ⭐ 修复：合并ABM和SCM作为总结池，排除固定的记忆
            var allMemoriesToSummarize = new List<MemoryEntry>();
            allMemoriesToSummarize.AddRange(activeMemories.Where(m => !m.isPinned));
            allMemoriesToSummarize.AddRange(situationalMemories.Where(m => !m.isPinned));

            // 如果没有非固定记忆，不需要总结
            if (allMemoriesToSummarize.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} daily summarization: no non-pinned memories to summarize");
                }
                return;
            }

            var byType = allMemoriesToSummarize.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string simpleSummary = CreateSimpleSummary(memories, typeGroup.Key);

                // ⭐ 修复：使用被总结记忆中最晚（最新）的timestamp作为总结的时间戳
                int latestTimestamp = memories.Max(m => m.timestamp);

                var summaryEntry = new MemoryEntry(
                    content: simpleSummary,
                    type: typeGroup.Key,
                    layer: MemoryLayer.EventLog,
                    importance: memories.Average(m => m.importance) + 0.2f
                );
                
                // ⭐ 修复：覆盖默认的timestamp（MemoryEntry构造函数会自动设置为当前时间）
                summaryEntry.timestamp = latestTimestamp;

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

                // ⭐ 修复：根据时间戳插入到正确位置，而不是总是插入到开头
                InsertMemoryByTimestamp(eventLogMemories, summaryEntry);
            }

            // ⭐ 修复：清空ABM（总结后不再需要保留）
            activeMemories.Clear();
            
            // ⭐ 修复：清空SCM（移除 isUserEdited 检查，只保留固定记忆）
            int beforeCount = situationalMemories.Count;
            situationalMemories.RemoveAll(m => !m.isPinned);
            int removedCount = beforeCount - situationalMemories.Count;
            
            if (Prefs.DevMode && removedCount > 0)
            {
                Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} daily summarization: " +
                           $"cleared ABM, removed {removedCount} SCM, kept {situationalMemories.Count} pinned");
            }
            
            TrimEventLog();
        }

        public void ManualSummarization()
        {
            // ⭐ 修复：同时检查ABM和SCM是否有内容
            if (activeMemories.Count == 0 && situationalMemories.Count == 0) return;

            var pawn = parent as Pawn;
            if (pawn == null) return;

            // ⭐ 修复：合并ABM和SCM作为总结池，排除固定的记忆
            var allMemoriesToSummarize = new List<MemoryEntry>();
            allMemoriesToSummarize.AddRange(activeMemories.Where(m => !m.isPinned));
            allMemoriesToSummarize.AddRange(situationalMemories.Where(m => !m.isPinned));

            // 如果没有非固定记忆，不需要总结
            if (allMemoriesToSummarize.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} manual summarization: no non-pinned memories to summarize");
                }
                return;
            }

            var byType = allMemoriesToSummarize.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                string simpleSummary = CreateSimpleSummary(memories, typeGroup.Key);

                // ⭐ 修复：使用被总结记忆中最晚（最新）的timestamp作为总结的时间戳
                int latestTimestamp = memories.Max(m => m.timestamp);

                var summaryEntry = new MemoryEntry(
                    content: simpleSummary,
                    type: typeGroup.Key,
                    layer: MemoryLayer.EventLog,
                    importance: memories.Average(m => m.importance) + 0.2f
                );
                
                // ⭐ 修复：覆盖默认的timestamp
                summaryEntry.timestamp = latestTimestamp;

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

                // ⭐ 修复：根据时间戳插入到正确位置，而不是总是插入到开头
                InsertMemoryByTimestamp(eventLogMemories, summaryEntry);
            }

            // ⭐ 修复：清空ABM（总结后不再需要保留）
            activeMemories.Clear();
            
            // ⭐ 修复：清空SCM（移除 isUserEdited 检查，只保留固定记忆）
            int beforeCount = situationalMemories.Count;
            situationalMemories.RemoveAll(m => !m.isPinned);
            int removedCount = beforeCount - situationalMemories.Count;
            
            if (Prefs.DevMode && removedCount > 0)
            {
                Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} manual summarization: " +
                           $"cleared ABM, removed {removedCount} SCM, kept {situationalMemories.Count} pinned");
            }
            
            TrimEventLog();
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

            // ⭐ 修复：只计算非固定的记忆数量（移除 isUserEdited 检查）
            int nonPinnedCount = eventLogMemories.Count(m => !m.isPinned);
            
            // 如果非固定记忆没超过上限，则不需要trim
            if (nonPinnedCount <= MAX_EVENTLOG)
                return;

            // ⭐ 修复：按时间戳排序，只移除非固定的最旧记忆（移除 isUserEdited 检查）
            int toRemoveCount = nonPinnedCount - MAX_EVENTLOG;
            var toRemove = eventLogMemories
                .Where(m => !m.isPinned)
                .OrderBy(m => m.timestamp)
                .Take(toRemoveCount)
                .ToList();
            
            foreach (var memory in toRemove)
            {
                eventLogMemories.Remove(memory);
                memory.layer = MemoryLayer.Archive;
                archiveMemories.Insert(0, memory);
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

        /// <summary>
        /// 记忆衰减和自动清理
        /// ⭐ v3.3.14: 添加activity阈值清理 + 容量限制
        /// </summary>
        public void DecayActivity()
        {
            float scmRate = RimTalkMemoryPatchMod.Settings.scmDecayRate;
            float elsRate = RimTalkMemoryPatchMod.Settings.elsDecayRate;
            float clpaRate = RimTalkMemoryPatchMod.Settings.clpaDecayRate;

            // 步骤1：衰减所有记忆的activity
            foreach (var memory in situationalMemories)
                memory.Decay(scmRate);

            foreach (var memory in eventLogMemories)
                memory.Decay(elsRate);

            foreach (var memory in archiveMemories)
                memory.Decay(clpaRate);
            
            // ⭐ 步骤2：清理极低activity的"死亡"记忆（方案1）
            CleanupLowActivityMemories();
            
            // ⭐ 步骤3：强制执行容量限制（方案3）
            EnforceMemoryLimits();
        }
        
        /// <summary>
        /// ⭐ v3.3.14: 清理极低activity的记忆（方案1）
        /// 当activity < 0.01时，认为记忆已"死亡"，可以安全删除
        /// ⭐ 移除 isUserEdited 检查，只保留固定记忆保护
        /// </summary>
        private void CleanupLowActivityMemories()
        {
            const float ACTIVITY_THRESHOLD = 0.01f; // activity < 0.01视为"死亡"
            
            int removedSCM = 0;
            int removedELS = 0;
            int removedCLPA = 0;
            
            // 清理SCM中的低activity记忆（移除 isUserEdited 检查）
            int beforeSCM = situationalMemories.Count;
            situationalMemories.RemoveAll(m => 
                m.activity < ACTIVITY_THRESHOLD && 
                !m.isPinned
            );
            removedSCM = beforeSCM - situationalMemories.Count;
            
            // 清理ELS中的低activity记忆（移除 isUserEdited 检查）
            int beforeELS = eventLogMemories.Count;
            eventLogMemories.RemoveAll(m => 
                m.activity < ACTIVITY_THRESHOLD && 
                !m.isPinned
            );
            removedELS = beforeELS - eventLogMemories.Count;
            
            // ⭐ 清理CLPA中的低activity记忆（移除 isUserEdited 检查）
            int beforeCLPA = archiveMemories.Count;
            archiveMemories.RemoveAll(m => 
                m.activity < ACTIVITY_THRESHOLD && 
                !m.isPinned
            );
            removedCLPA = beforeCLPA - archiveMemories.Count;
            
            // 开发模式日志
            if (Prefs.DevMode && (removedSCM > 0 || removedELS > 0 || removedCLPA > 0))
            {
                var pawn = parent as Pawn;
                Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} cleaned up " +
                           $"{removedSCM} SCM + {removedELS} ELS + {removedCLPA} CLPA memories (activity < {ACTIVITY_THRESHOLD})");
            }
        }
        
        /// <summary>
        /// ⭐ v3.3.14: 强制执行容量限制（方案3）
        /// 当层级超过容量时，删除最低activity的记忆
        /// ⭐ 移除 isUserEdited 检查，只保留固定记忆保护
        /// </summary>
        private void EnforceMemoryLimits()
        {
            int removedSCM = 0;
            int removedELS = 0;
            
            // ⭐ 修复：只计算非固定的记忆数量（移除 isUserEdited 检查）
            int scmNonPinnedCount = situationalMemories.Count(m => !m.isPinned);
            int elsNonPinnedCount = eventLogMemories.Count(m => !m.isPinned);
            
            // ⭐ 处理SCM容量限制（移除 isUserEdited 检查）
            if (scmNonPinnedCount > MAX_SITUATIONAL)
            {
                int toRemoveCount = scmNonPinnedCount - MAX_SITUATIONAL;
                // 按activity升序排序，删除最低的
                var toRemove = situationalMemories
                    .Where(m => !m.isPinned)
                    .OrderBy(m => m.activity)
                    .ThenBy(m => m.timestamp) // 相同activity时，删除更旧的
                    .Take(toRemoveCount)
                    .ToList();
                
                foreach (var memory in toRemove)
                {
                    situationalMemories.Remove(memory);
                    removedSCM++;
                }
            }
            
            // ⭐ 处理ELS容量限制（移除 isUserEdited 检查）
            if (elsNonPinnedCount > MAX_EVENTLOG)
            {
                int toRemoveCount = elsNonPinnedCount - MAX_EVENTLOG;
                // 按activity升序排序，删除最低的
                var toRemove = eventLogMemories
                    .Where(m => !m.isPinned)
                    .OrderBy(m => m.activity)
                    .ThenBy(m => m.timestamp)
                    .Take(toRemoveCount)
                    .ToList();
                
                foreach (var memory in toRemove)
                {
                    eventLogMemories.Remove(memory);
                    removedELS++;
                }
            }
            
            // 开发模式日志
            if (Prefs.DevMode && (removedSCM > 0 || removedELS > 0))
            {
                var pawn = parent as Pawn;
                int scmPinnedCount = situationalMemories.Count(m => m.isPinned);
                int elsPinnedCount = eventLogMemories.Count(m => m.isPinned);
                
                Log.Message($"[Memory] {pawn?.LabelShort ?? "Unknown"} enforced limits: " +
                           $"removed {removedSCM} SCM (non-pinned: {scmNonPinnedCount - removedSCM}, pinned: {scmPinnedCount}, max: {MAX_SITUATIONAL}) + " +
                           $"{removedELS} ELS (non-pinned: {elsNonPinnedCount - removedELS}, pinned: {elsPinnedCount}, max: {MAX_EVENTLOG})");
            }
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
                .OrderByDescending(m => m.timestamp);
            results.AddRange(abmCandidates);
            
            // ⭐ v4.0: SCM 仅兼容旧存档（不再生成新的）
            if (situationalMemories.Count > 0)
            {
                var scmCandidates = situationalMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                    .ThenBy(m => m.id, StringComparer.Ordinal)
                    .Take(5);
                results.AddRange(scmCandidates);
            }
            
            if (query.includeContext && results.Count < query.maxCount)
            {
                // ⭐ v3.3.2.29: ELS 候选 - 确定性排序（分数降序 + ID 升序）
                var elsCandidates = eventLogMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                    .ThenBy(m => m.id, StringComparer.Ordinal)
                    .Take(query.maxCount - results.Count);
                results.AddRange(elsCandidates);
            }
            
            if (query.layer == MemoryLayer.Archive)
            {
            // ⭐ v3.3.2.29: CLPA 候选 - 确定性排序（重要性降序 + ID 升序）
                var clpaCandidates = archiveMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.importance)
                    .ThenBy(m => m.id, StringComparer.Ordinal)
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
                // ⭐ 修复：只在首次编辑时设置 isUserEdited，避免覆盖用户手动删除的标记
                if (!memory.isUserEdited)
                {
                    memory.isUserEdited = true;
                }
                if (!string.IsNullOrEmpty(notes))
                    memory.notes = notes;
            }
        }

        public void PinMemory(string memoryId, bool pinned)
        {
            var memory = FindMemoryById(memoryId);
            if (memory is RoundMemory roundMemory)
            {
                PinRoundMemory(roundMemory, memoryId); // 已并入
                return;
            }
            if (memory != null)
            {
                memory.isPinned = pinned;
            }
        }
        // 轮次记忆入口
        public void PinRoundMemory(RoundMemory roundMemory, string memoryId)
        {
            Log.Message("[轮次记忆] FourLayerMemoryComp.PinMemory: Pinning 轮次记忆");

            // 是 RoundMemory 类型，则创建一个新的 MemoryEntry 对象复制 RoundMemory
            var newMemory = new MemoryEntry(
            content: string.Empty,
            type: MemoryType.Conversation,
            layer: MemoryLayer.Situational,
            importance: 0.5f
            )
            {
                content = roundMemory.content,
                timestamp = roundMemory.timestamp,
                relatedPawnId = roundMemory.relatedPawnId,
                relatedPawnName = roundMemory.relatedPawnName,
                location = roundMemory.location,
                tags = new(roundMemory.tags ?? Enumerable.Empty<string>()),
                keywords = new(roundMemory.keywords ?? Enumerable.Empty<string>()),
                isUserEdited = true,
                isPinned = true,
                notes = roundMemory.notes,
                aiCacheKey = roundMemory.aiCacheKey,
            };
            SituationalMemories?.Add(newMemory);
            DeleteMemory(memoryId);
            Log.Message("[轮次记忆] FourLayerMemoryComp.PinMemory: Pinned 轮次记忆 as MemoryEntry");

            roundMemory.isPinned = false; // 由于UI bug，这里强制回正一下
            // 刷新UI
            var memoryWindow = GetMemoryWindowInstance();
            if (memoryWindow == null) return;
            memoryWindow.LastMemoryCount = -1;
            memoryWindow.FiltersDirty = true;
            Log.Message("[轮次记忆] FourLayerMemoryComp.PinMemory: Refreshed Memory Window UI");

            return;
        }
        // 获取 Memory 窗口实例
        static MainTabWindow_Memory GetMemoryWindowInstance()
        {
            return Find.WindowStack.Windows
                .OfType<MainTabWindow_Memory>()
                .FirstOrDefault();
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
