using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimWorld.Planet;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// WorldComponent to manage global memory decay and daily summarization
    /// 支持四层记忆系统 (FMS)
    /// ⭐ v3.3.2.3: 添加向后兼容性支持
    /// </summary>
    public class MemoryManager : WorldComponent
    {
        // ⭐ 静态构造函数确保类型正确注册
        static MemoryManager()
        {
            // RimWorld会自动发现和注册WorldComponent子类
            // 这个静态构造函数确保类型在使用前被初始化
        }
        
        private int lastDecayTick = 0;
        private const int DecayInterval = 2500; // Every in-game hour
        
        private int lastSummarizationDay = -1; // 上次ELS总结的日期
        private int lastArchiveDay = -1;        // 上次CLPA归档的日期
        
        // ⭐ 冷启动缓冲：本次会话开始时间（不保存）
        private int sessionStartTick = -1;
        private const int COLD_START_DELAY = 200; // 启动后延迟200 ticks (约3秒) 再开始运作

        // ⭐ 总结队列（延迟处理）
        private Queue<Pawn> summarizationQueue = new Queue<Pawn>();
        private int nextSummarizationTick = 0;
        private const int SUMMARIZATION_DELAY_TICKS = 900; // 15秒 = 15 * 60 ticks
        
        // ⭐ 手动总结队列（延迟1秒）
        private Queue<Pawn> manualSummarizationQueue = new Queue<Pawn>();
        private int nextManualSummarizationTick = 0;
        private const int MANUAL_SUMMARIZATION_DELAY_TICKS = 60; // 1秒 = 60 ticks

        // 全局常识库
        private CommonKnowledgeLibrary commonKnowledge;
        public CommonKnowledgeLibrary CommonKnowledge
        {
            get
            {
                if (commonKnowledge == null)
                    commonKnowledge = new CommonKnowledgeLibrary();
                return commonKnowledge;
            }
        }
        
        // 对话缓存
        private ConversationCache conversationCache;
        public ConversationCache ConversationCache
        {
            get
            {
                if (conversationCache == null)
                    conversationCache = new ConversationCache();
                return conversationCache;
            }
        }
        
        // ⭐ 提示词缓存（新增）
        private PromptCache promptCache;
        public PromptCache PromptCache
        {
            get
            {
                if (promptCache == null)
                    promptCache = new PromptCache();
                return promptCache;
            }
        }

        /// <summary>
        /// 静态方法获取常识库
        /// </summary>
        public static CommonKnowledgeLibrary GetCommonKnowledge()
        {
            if (Current.Game == null) return new CommonKnowledgeLibrary();
            
            var manager = Find.World.GetComponent<MemoryManager>();
            return manager?.CommonKnowledge ?? new CommonKnowledgeLibrary();
        }
        
        /// <summary>
        /// 静态方法获取对话缓存
        /// </summary>
        public static ConversationCache GetConversationCache()
        {
            if (Current.Game == null) return new ConversationCache();
            
            var manager = Find.World.GetComponent<MemoryManager>();
            return manager?.ConversationCache ?? new ConversationCache();
        }
        
        /// <summary>
        /// ⭐ 静态方法获取提示词缓存（新增）
        /// </summary>
        public static PromptCache GetPromptCache()
        {
            if (Current.Game == null) return new PromptCache();
            
            var manager = Find.World.GetComponent<MemoryManager>();
            return manager?.PromptCache ?? new PromptCache();
        }

        public MemoryManager(World world) : base(world)
        {
            commonKnowledge = new CommonKnowledgeLibrary();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // ⭐ 冷启动缓冲：进入游戏后延迟运作，避免加载时的性能冲击
            if (sessionStartTick == -1) sessionStartTick = Find.TickManager.TicksGame;
            if (Find.TickManager.TicksGame - sessionStartTick < COLD_START_DELAY) return;

            // 每小时衰减记忆活跃度
            if (Find.TickManager.TicksGame - lastDecayTick >= DecayInterval)
            {
                DecayAllMemories();
                lastDecayTick = Find.TickManager.TicksGame;
                
                // 检查工作会话超时
                WorkSessionAggregator.CheckSessionTimeouts();
                
                // ⭐ 每小时更新Pawn状态常识（24小时间隔检查）
                if (RimTalkMemoryPatchMod.Settings.enablePawnStatusKnowledge)
                {
                    PawnStatusKnowledgeGenerator.UpdateAllColonistStatus();
                }
                
                // ⭐ v3.4.0: 移除常识库自动生成事件历史功能
                // 原有的 EventRecordKnowledgeGenerator.ScanRecentPlayLog() 调用已移除
                
                // 定期清理
                PawnStatusKnowledgeGenerator.CleanupUpdateRecords();
            }
            
            // ⭐ 处理总结队列（每tick检查）
            ProcessSummarizationQueue();
            
            // ⭐ 处理手动总结队列
            ProcessManualSummarizationQueue();
            
            // 每天 0 点触发总结
            CheckDailySummarization();
        }
        
        /// <summary>
        /// ⭐ 修复2：更新所有事件常识的时间前缀
        /// </summary>
        private void UpdateEventKnowledgeTimePrefixes()
        {
            if (commonKnowledge == null || commonKnowledge.Entries == null)
                return;
            
            int currentTick = Find.TickManager.TicksGame;
            int updatedCount = 0;
            
            // 只更新带时间戳的事件常识
            foreach (var entry in commonKnowledge.Entries)
            {
                if (entry.creationTick >= 0 && !string.IsNullOrEmpty(entry.originalEventText))
                {
                    // 保存原始内容用于比较
                    string oldContent = entry.content;
                    
                    // 更新时间前缀
                    entry.UpdateEventTimePrefix(currentTick);
                    
                    // 如果内容发生变化，计数
                    if (entry.content != oldContent)
                    {
                        updatedCount++;
                    }
                }
            }
            
            // 开发模式日志（每10次更新才输出一次）
            if (Prefs.DevMode && updatedCount > 0 && UnityEngine.Random.value < 0.1f)
            {
                Log.Message($"[RimTalk Memory] Updated {updatedCount} event knowledge time prefixes");
            }
        }
        
        /// <summary>
        /// 检查并触发每日总结（游戏时间 0 点）
        /// </summary>
        private void CheckDailySummarization()
        {
            if (Current.Game == null || Find.CurrentMap == null) return;
            
            // 检查设置是否启用
            if (!RimTalkMemoryPatchMod.Settings.enableDailySummarization)
                return;
            
            int currentDay = GenDate.DaysPassed;
            int currentHour = GenLocalDate.HourOfDay(Find.CurrentMap);
            int targetHour = RimTalkMemoryPatchMod.Settings.summarizationHour;
            
            // 当天第一次检查，且时间在目标小时（ELS总结：每天一次）
            if (currentDay != lastSummarizationDay && currentHour == targetHour)
            {
                Log.Message($"[RimTalk Memory] 🌙 Day {currentDay}, Hour {currentHour}: Triggering daily ELS summarization");
                
                foreach (var map in Find.Maps)
                {
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        // ⭐ v3.5.2: 扩展到殖民者 + 配置了链接催化剂的殖民地动物/机械体
                        if (pawn.IsColonist || IsColonyAnimalWithVocalLink(pawn))
                        {
                            // 将总结任务加入队列
                            summarizationQueue.Enqueue(pawn);
                        }
                    }
                }
                
                lastSummarizationDay = currentDay;
            }
            
            // CLPA归档：按天数间隔触发
            CheckArchiveInterval(currentDay);
        }

        /// <summary>
        /// 为所有殖民者触发每日总结
        /// </summary>
        private void SummarizeAllMemories()
        {
            if (Current.Game == null) return;

            // ⭐ 收集所有需要总结的殖民者，加入队列
            int queuedCount = 0;
            
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    // ⭐ v3.5.2: 扩展到殖民者 + 配置了链接催化剂的殖民地动物/机械体
                    if (pawn.IsColonist || IsColonyAnimalWithVocalLink(pawn))
                    {
                        // 检查是否有需要总结的记忆
                        var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
                        if (fourLayerComp != null && fourLayerComp.SituationalMemories.Count > 0)
                        {
                            summarizationQueue.Enqueue(pawn);
                            queuedCount++;
                        }
                        else
                        {
                            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
                            if (memoryComp != null && memoryComp.GetSituationalMemoryCount() > 0)
                            {
                                summarizationQueue.Enqueue(pawn);
                                queuedCount++;
                            }
                        }
                    }
                }
            }

            if (queuedCount > 0)
            {
                Log.Message($"[RimTalk Memory] 📋 Queued {queuedCount} colonists for summarization (15s delay between each)");
                // 立即处理第一个
                nextSummarizationTick = Find.TickManager.TicksGame;
            }
            else
            {
                Log.Message($"[RimTalk Memory] ✅ No colonists need summarization");
            }
        }

        /// <summary>
        /// ⭐ 处理总结队列（每个殖民者之间延迟15秒）
        /// </summary>
        private void ProcessSummarizationQueue()
        {
            if (summarizationQueue.Count == 0)
                return;

            int currentTick = Find.TickManager.TicksGame;
            
            // 检查是否到达下一个总结时间
            if (currentTick < nextSummarizationTick)
                return;

            // 从队列中取出一个殖民者
            Pawn pawn = summarizationQueue.Dequeue();
            
            if (pawn == null || pawn.Dead || pawn.Destroyed)
            {
                // 殖民者已死亡或销毁，跳过
                if (summarizationQueue.Count > 0)
                {
                    nextSummarizationTick = currentTick; // 立即处理下一个
                }
                return;
            }

            // 执行总结
            bool summarized = false;
            var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (fourLayerComp != null)
            {
                fourLayerComp.DailySummarization();
                summarized = true;
            }
            else
            {
                var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
                if (memoryComp != null)
                {
                    memoryComp.DailySummarization();
                    summarized = true;
                }
            }

            if (summarized)
            {
                // ⭐ v3.3.2: 降低日志输出 - 仅DevMode且10%概率
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[RimTalk Memory] Summarized memories for {pawn.LabelShort} ({summarizationQueue.Count} remaining)");
                }
            }

            // 如果还有更多殖民者，设置下一个总结时间（15秒后）
            if (summarizationQueue.Count > 0)
            {
                nextSummarizationTick = currentTick + SUMMARIZATION_DELAY_TICKS;
                
                // ⭐ v3.3.2: 移除下一次总结时间的日志
                // Log.Message($"[RimTalk Memory] Next colonist will be summarized in 15 seconds...");
            }
            else
            {
                // ⭐ v3.3.2: 降低日志输出
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[RimTalk Memory] All colonists summarized!");
                }
            }
        }

        /// <summary>
        /// ⭐ 处理手动总结队列（每个殖民者之间延迟1秒）
        /// </summary>
        private void ProcessManualSummarizationQueue()
        {
            if (manualSummarizationQueue.Count == 0)
                return;

            int currentTick = Find.TickManager.TicksGame;
            
            // 检查是否到达下一个总结时间
            if (currentTick < nextManualSummarizationTick)
                return;

            // 从队列中取出一个殖民者
            Pawn pawn = manualSummarizationQueue.Dequeue();
            
            if (pawn == null || pawn.Dead || pawn.Destroyed)
            {
                // 殖民者已死亡或销毁，跳过
                if (manualSummarizationQueue.Count > 0)
                {
                    nextManualSummarizationTick = currentTick; // 立即处理下一个
                }
                return;
            }

            // 执行手动总结
            bool summarized = false;
            int scmCount = 0;
            var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (fourLayerComp != null)
            {
                scmCount = fourLayerComp.SituationalMemories.Count;
                if (scmCount > 0)
                {
                    fourLayerComp.ManualSummarization();
                    summarized = true;
                }
            }

            if (summarized)
            {
                // ⭐ v3.3.2: 降低日志输出
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[RimTalk Memory] Manual summarized for {pawn.LabelShort} ({scmCount} SCM -> ELS, {manualSummarizationQueue.Count} remaining)");
                }
                
                // ⭐ 给用户反馈消息（保留）
                Messages.Message(
                    $"{pawn.LabelShort}: {scmCount}条短期记忆已总结",
                    MessageTypeDefOf.TaskCompletion,
                    false
                );
            }

            // 如果还有更多殖民者，设置下一个总结时间（1秒后）
            if (manualSummarizationQueue.Count > 0)
            {
                nextManualSummarizationTick = currentTick + MANUAL_SUMMARIZATION_DELAY_TICKS;
            }
            else
            {
                // ⭐ v3.3.2: 降低日志输出
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[RimTalk Memory] All manual summarizations complete!");
                }
                // ⭐ 所有总结完成后的消息（保留）
                Messages.Message("所有殖民者手动总结完成", MessageTypeDefOf.PositiveEvent, false);
            }
        }
        
        /// <summary>
        /// ⭐ 手动触发总结（批量）
        /// </summary>
        public void QueueManualSummarization(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0) return;

            int queuedCount = 0;
            foreach (var pawn in pawns)
            {
                if (pawn != null && !pawn.Dead && !pawn.Destroyed)
                {
                    var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
                    if (fourLayerComp != null && fourLayerComp.SituationalMemories.Count > 0)
                    {
                        manualSummarizationQueue.Enqueue(pawn);
                        queuedCount++;
                    }
                }
            }

            if (queuedCount > 0)
            {
                Log.Message($"[RimTalk Memory] 📋 Queued {queuedCount} colonists for manual summarization (1s delay between each)");
                // 立即处理第一个
                nextManualSummarizationTick = Find.TickManager.TicksGame;
            }
            else
            {
                Messages.Message("没有需要手动总结的殖民者", MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>
        /// 为所有殖民者触发记忆衰减
        /// </summary>
        private void DecayAllMemories()
        {
            if (Current.Game == null) return;

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    // ⭐ v3.5.2: 扩展到殖民者 + 配置了链接催化剂的殖民地动物/机械体
                    if (pawn.IsColonist || IsColonyAnimalWithVocalLink(pawn))
                    {
                        // 尝试新的四层记忆组件
                        var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
                        if (fourLayerComp != null)
                        {
                            fourLayerComp.DecayActivity();
                        }
                        else
                        {
                            // 兼容旧的记忆组件
                            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
                            if (memoryComp != null)
                            {
                                memoryComp.DecayMemories();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 检查并触发CLPA归档（按天数间隔）
        /// ⭐ v3.3.2.33: 重构 - 实现真正的 ELS → CLPA 自动归档（前25%）
        /// </summary>
        /// <param name="currentDay">当前游戏中的天数</param>
        private void CheckArchiveInterval(int currentDay)
        {
            // 检查设置是否启用CLPA自动归档
            if (!RimTalkMemoryPatchMod.Settings.enableAutoArchive)
                return;
            
            int intervalDays = RimTalkMemoryPatchMod.Settings.archiveIntervalDays;
            
            // 检查是否到达归档间隔
            if (lastArchiveDay == -1)
            {
                // 首次初始化，记录当前天数
                lastArchiveDay = currentDay;
                return;
            }
            
            int daysSinceLastArchive = currentDay - lastArchiveDay;
            
            // 如果距离上次归档还没到间隔天数，直接返回
            if (daysSinceLastArchive < intervalDays)
                return;
            
            // 到达归档时间
            Log.Message($"[RimTalk Memory] 📚 Day {currentDay}: Triggering CLPA archive (every {intervalDays} days)");
            
            int totalArchivedPawns = 0;
            int totalArchivedEntries = 0;
            int totalRemovedELS = 0;
            
            // 遍历所有殖民者，执行 ELS → CLPA 归档
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    // ⭐ v3.5.2: 扩展到殖民者 + 配置了链接催化剂的殖民地动物/机械体
                    if (!pawn.IsColonist && !IsColonyAnimalWithVocalLink(pawn))
                        continue;
                    
                    var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
                    if (fourLayerComp == null)
                        continue;
                    
                    // 检查是否有 ELS 记忆需要归档
                    if (fourLayerComp.EventLogMemories.Count == 0)
                        continue;
                    
                    // ⭐ 步骤1：选择最旧的前 25% ELS 记忆进行归档（移除 isUserEdited 检查）
                    var nonPinnedELS = fourLayerComp.EventLogMemories
                        .Where(m => !m.isPinned)
                        .ToList();
                    
                    if (nonPinnedELS.Count == 0)
                        continue;
                    
                    // 计算归档数量（前25%，至少1条）
                    int archiveCount = Math.Max(1, (int)(nonPinnedELS.Count * 0.25f));
                    
                    // 选择最旧的记忆
                    var toArchive = nonPinnedELS
                        .OrderBy(m => m.timestamp)
                        .Take(archiveCount)
                        .ToList();
                    
                    if (toArchive.Count == 0)
                        continue;
                    
                    // ⭐ 步骤2：将选中的记忆按类型分组并总结归档
                    var byType = toArchive.GroupBy(m => m.type);
                    
                    int archivedCount = 0;
                    foreach (var typeGroup in byType)
                    {
                        var memories = typeGroup.ToList();
                        
                        // 创建归档摘要（简单版本）
                        string archiveSummary = CreateArchiveSummary(memories, typeGroup.Key);
                        
                        // ⭐ 修复：使用被归档记忆中最晚（最新）的timestamp作为归档entry的时间戳
                        int latestTimestamp = memories.Max(m => m.timestamp);
                        
                        var archiveEntry = new MemoryEntry(
                            content: archiveSummary,
                            type: typeGroup.Key,
                            layer: MemoryLayer.Archive,
                            importance: memories.Average(m => m.importance) + 0.3f // CLPA 记忆重要性更高
                        );
                        
                        // ⭐ 修复：覆盖默认的timestamp
                        archiveEntry.timestamp = latestTimestamp;
                        
                        // 合并关键词和标签
                        archiveEntry.keywords.AddRange(memories.SelectMany(m => m.keywords).Distinct());
                        archiveEntry.tags.AddRange(memories.SelectMany(m => m.tags).Distinct());
                        archiveEntry.AddTag("自动归档");
                        archiveEntry.AddTag($"源自{memories.Count}条ELS");
                        
                        // ⭐ 如果启用 AI 总结，异步更新归档内容
                        if (RimTalkMemoryPatchMod.Settings.useAISummarization && AI.IndependentAISummarizer.IsAvailable())
                        {
                            string cacheKey = AI.IndependentAISummarizer.ComputeCacheKey(pawn, memories);
                            
                            AI.IndependentAISummarizer.RegisterCallback(cacheKey, (aiSummary) =>
                            {
                                if (!string.IsNullOrEmpty(aiSummary))
                                {
                                    archiveEntry.content = aiSummary;
                                    archiveEntry.RemoveTag("简单归档");
                                    archiveEntry.AddTag("AI归档");
                                    archiveEntry.notes = "AI 深度归档已完成。";
                                }
                            });
                            
                            AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "deep_archive");
                            
                            archiveEntry.AddTag("简单归档");
                            archiveEntry.AddTag("待AI更新");
                            archiveEntry.notes = "AI 深度归档正在后台处理中...";
                        }
                        
                        // 添加到 CLPA
                        fourLayerComp.ArchiveMemories.Insert(0, archiveEntry);
                        archivedCount++;
                    }
                    
                    // ⭐ 步骤3：只删除已归档的记忆
                    int removedCount = 0;
                    foreach (var memory in toArchive)
                    {
                        if (fourLayerComp.EventLogMemories.Remove(memory))
                        {
                            removedCount++;
                        }
                    }
                    
                    if (archivedCount > 0)
                    {
                        totalArchivedPawns++;
                        totalArchivedEntries += archivedCount;
                        totalRemovedELS += removedCount;
                        
                        if (Prefs.DevMode)
                        {
                            int remainingELS = fourLayerComp.EventLogMemories.Count;
                            Log.Message($"[RimTalk Memory] Archived {archivedCount} CLPA entries for {pawn.LabelShort}, " +
                                       $"removed {removedCount} ELS (前25%), kept {remainingELS} ELS (including {remainingELS - nonPinnedELS.Count + removedCount} pinned/edited)");
                        }
                    }
                    
                    // ⭐ 步骤4：清理超过上限的旧 CLPA 记忆（移除 isUserEdited 检查）
                    int maxArchive = RimTalkMemoryPatchMod.Settings.maxArchiveMemories;
                    if (fourLayerComp.ArchiveMemories.Count > maxArchive)
                    {
                        // 移除最旧的低重要性记忆（只保护固定记忆）
                        var toRemove = fourLayerComp.ArchiveMemories
                            .Where(m => !m.isPinned)
                            .OrderBy(m => m.importance)
                            .ThenBy(m => m.timestamp)
                            .Take(fourLayerComp.ArchiveMemories.Count - maxArchive)
                            .ToList();
                        
                        foreach (var memory in toRemove)
                        {
                            fourLayerComp.ArchiveMemories.Remove(memory);
                        }
                        
                        if (Prefs.DevMode && toRemove.Count > 0)
                        {
                            Log.Message($"[RimTalk Memory] Cleaned {toRemove.Count} old CLPA memories for {pawn.LabelShort}");
                        }
                    }
                }
            }
            
            // 更新最后归档日期
            lastArchiveDay = currentDay;
            
            // 输出总结日志
            if (totalArchivedPawns > 0)
            {
                Log.Message($"[RimTalk Memory] ✅ CLPA auto-archive complete: {totalArchivedPawns} colonists, {totalArchivedEntries} CLPA entries created, {totalRemovedELS} ELS removed (前25%)");
                
                // 可选：给用户一个通知
                Messages.Message(
                    $"CLPA自动归档完成：{totalArchivedPawns}名殖民者，{totalArchivedEntries}条归档记忆，{totalRemovedELS}条ELS已归档",
                    MessageTypeDefOf.NeutralEvent,
                    false
                );
            }
            else
            {
                Log.Message($"[RimTalk Memory] ✅ CLPA auto-archive check complete: no memories to archive");
            }
        }
        
        /// <summary>
        /// 创建归档摘要（简单版本）
        /// </summary>
        private string CreateArchiveSummary(List<MemoryEntry> memories, MemoryType type)
        {
            if (memories == null || memories.Count == 0)
                return null;
            
            var summary = new StringBuilder();
            
            // 归档摘要格式：更详细，因为是长期保存
            if (type == MemoryType.Conversation)
            {
                // 对话归档：按对话对象分组
                var byPerson = memories
                    .Where(m => !string.IsNullOrEmpty(m.relatedPawnName))
                    .GroupBy(m => m.relatedPawnName)
                    .OrderByDescending(g => g.Count());
                
                summary.Append($"对话归档（{memories.Count}条）：");
                int shown = 0;
                foreach (var group in byPerson.Take(10))
                {
                    if (shown > 0) summary.Append("；");
                    summary.Append($"与{group.Key}对话×{group.Count()}");
                    shown++;
                }
            }
            else if (type == MemoryType.Action)
            {
                // 行动归档：按行动类型分组
                summary.Append($"行动归档（{memories.Count}条）：");
                
                var grouped = memories
                    .Select(m => m.content.Length > 20 ? m.content.Substring(0, 20) : m.content)
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(5))
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
                // 其他类型归档：保留更多细节
                summary.Append($"{type}归档（{memories.Count}条）：");
                
                var grouped = memories
                    .GroupBy(m => m.content.Length > 30 ? m.content.Substring(0, 30) : m.content)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(8))
                {
                    if (shown > 0) summary.Append("；");
                    
                    string content = group.First().content;
                    if (content.Length > 60)
                        content = content.Substring(0, 60) + "...";
                    
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
            
            return summary.ToString();
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref lastDecayTick, "lastDecayTick", 0);
            Scribe_Values.Look(ref lastSummarizationDay, "lastSummarizationDay", -1);
            Scribe_Values.Look(ref lastArchiveDay, "lastArchiveDay", -1);
            Scribe_Values.Look(ref nextSummarizationTick, "nextSummarizationTick", 0);
            
            Scribe_Deep.Look(ref commonKnowledge, "commonKnowledge");
            Scribe_Deep.Look(ref conversationCache, "conversationCache");
            Scribe_Deep.Look(ref promptCache, "promptCache");
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // ⭐ 确保所有组件都已初始化
                if (commonKnowledge == null)
                {
                    commonKnowledge = new CommonKnowledgeLibrary();
                    Log.Warning("[RimTalk Memory] commonKnowledge was null, initialized new instance");
                }
                if (conversationCache == null)
                {
                    conversationCache = new ConversationCache();
                    Log.Warning("[RimTalk Memory] conversationCache was null, initialized new instance");
                }
                if (promptCache == null)
                {
                    promptCache = new PromptCache();
                    Log.Warning("[RimTalk Memory] promptCache was null, initialized new instance");
                }
                
                // ⭐ 重新初始化队列（不保存到存档）
                if (summarizationQueue == null)
                    summarizationQueue = new Queue<Pawn>();
                if (manualSummarizationQueue == null)
                    manualSummarizationQueue = new Queue<Pawn>();
                
                // ⭐ 兼容性处理：旧存档初始化
                // 如果是旧存档（没有记录过日期），将日期初始化为当前日期，防止立即触发归档/总结
                int currentDay = GenDate.DaysPassed;
                
                if (lastArchiveDay == -1)
                {
                    lastArchiveDay = currentDay;
                    Log.Warning($"[RimTalk Memory] ⚠️ Old save detected! Initialized lastArchiveDay to {currentDay} to prevent immediate archive.");
                }
                
                if (lastSummarizationDay == -1)
                {
                    lastSummarizationDay = currentDay;
                    Log.Warning($"[RimTalk Memory] ⚠️ Old save detected! Initialized lastSummarizationDay to {currentDay} to prevent immediate summarization.");
                }
                
                Log.Message($"[RimTalk Memory] MemoryManager loaded successfully.");
            }
        }
        
        #region 辅助方法
        /// <summary>
        /// ⭐ v3.5.2: 检测是否为配置了链接催化剂的殖民地动物或机械体
        /// </summary>
        private static bool IsColonyAnimalWithVocalLink(Pawn pawn)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer) return false;
            if (pawn.RaceProps?.Humanlike == true) return false; // 人类已经被 IsColonist 覆盖
            
            try
            {
                var vocalLinkDef = DefDatabase<HediffDef>.GetNamed("VocalLinkImplant", false);
                return vocalLinkDef != null && pawn.health?.hediffSet?.HasHediff(vocalLinkDef) == true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
