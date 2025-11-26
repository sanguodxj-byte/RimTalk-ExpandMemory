using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// WorldComponent to manage global memory decay and daily summarization
    /// 支持四层记忆系统 (FMS)
    /// </summary>
    public class MemoryManager : WorldComponent
    {
        private int lastDecayTick = 0;
        private const int DecayInterval = 2500; // Every in-game hour
        
        private int lastSummarizationDay = -1; // 上次ELS总结的日期
        private int lastArchiveDay = -1;        // 上次CLPA归档的日期

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
                
                // ⭐ 每小时扫描PlayLog事件
                if (RimTalkMemoryPatchMod.Settings.enableEventRecordKnowledge)
                {
                    EventRecordKnowledgeGenerator.ScanRecentPlayLog();
                    
                    // ⭐ 新增：检查活跃袭击状态
                    Patches.IncidentPatch.CheckRaidStatus();
                }
                
                // 定期清理
                PawnStatusKnowledgeGenerator.CleanupUpdateRecords();
                EventRecordKnowledgeGenerator.CleanupProcessedRecords();
            }
            
            // ⭐ 处理总结队列（每tick检查）
            ProcessSummarizationQueue();
            
            // ⭐ 处理手动总结队列
            ProcessManualSummarizationQueue();
            
            // 每天 0 点触发总结
            CheckDailySummarization();
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
                        if (pawn.IsColonist)
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
                    if (pawn.IsColonist)
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
                Log.Message($"[RimTalk Memory] ✓ Summarized memories for {pawn.LabelShort} ({summarizationQueue.Count} remaining)");
            }

            // 如果还有更多殖民者，设置下一个总结时间（15秒后）
            if (summarizationQueue.Count > 0)
            {
                nextSummarizationTick = currentTick + SUMMARIZATION_DELAY_TICKS;
                Log.Message($"[RimTalk Memory] ⏰ Next colonist will be summarized in 15 seconds...");
            }
            else
            {
                Log.Message($"[RimTalk Memory] ✅ All colonists summarized!");
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
                Log.Message($"[RimTalk Memory] ✓ Manual summarized for {pawn.LabelShort} ({scmCount} SCM -> ELS, {manualSummarizationQueue.Count} remaining)");
                
                // ⭐ 给用户反馈消息
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
                Log.Message($"[RimTalk Memory] ✅ All manual summarizations complete!");
                // ⭐ 所有总结完成后的消息
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
                    if (pawn.IsColonist)
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
        /// </summary>
        /// <param name="currentDay">当前游戏中的天数</param>
        private void CheckArchiveInterval(int currentDay)
        {
            // 检查设置是否启用CLPA自动归档
            if (!RimTalkMemoryPatchMod.Settings.enableAutoArchive)
                return;
            
            int intervalDays = RimTalkMemoryPatchMod.Settings.archiveIntervalDays;
            
            // 检查是否到达归档间隔
            if (currentDay != lastArchiveDay && currentDay % intervalDays == 0)
            {
                Log.Message($"[RimTalk Memory] 📚 Day {currentDay}: Triggering CLPA archive (every {intervalDays} days)");
                
                int totalArchived = 0;
                
                // 检查每个殖民者的CLPA记忆
                foreach (var map in Find.Maps)
                {
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        if (pawn.IsColonist)
                        {
                            var fourLayerComp = pawn.TryGetComp<FourLayerMemoryComp>();
                            if (fourLayerComp != null)
                            {
                                // 检查CLPA容量，超过上限则清理
                                int maxArchive = RimTalkMemoryPatchMod.Settings.maxArchiveMemories;
                                if (fourLayerComp.ArchiveMemories.Count > maxArchive)
                                {
                                    // 移除最旧的低重要性记忆
                                    var toRemove = fourLayerComp.ArchiveMemories
                                        .OrderBy(m => m.importance)
                                        .ThenBy(m => m.timestamp)
                                        .Take(fourLayerComp.ArchiveMemories.Count - maxArchive)
                                        .ToList();
                                    
                                    foreach (var memory in toRemove)
                                    {
                                        fourLayerComp.ArchiveMemories.Remove(memory);
                                    }
                                    
                                    if (toRemove.Count > 0)
                                    {
                                        totalArchived++;
                                        if (Prefs.DevMode)
                                        {
                                            Log.Message($"[RimTalk Memory] Cleaned {toRemove.Count} old CLPA memories for {pawn.LabelShort}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (totalArchived > 0)
                {
                    Log.Message($"[RimTalk Memory] ✅ CLPA archive cleanup complete for {totalArchived} colonists");
                }
                
                lastArchiveDay = currentDay;
            }
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
            Scribe_Deep.Look(ref promptCache, "promptCache"); // ⭐ 新增
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (commonKnowledge == null)
                    commonKnowledge = new CommonKnowledgeLibrary();
                if (conversationCache == null)
                    conversationCache = new ConversationCache();
                if (promptCache == null) // ⭐ 新增
                    promptCache = new PromptCache();
                
                // ⭐ 重新初始化队列（不保存到存档）
                if (summarizationQueue == null)
                    summarizationQueue = new Queue<Pawn>();
            }
        }
    }
}
