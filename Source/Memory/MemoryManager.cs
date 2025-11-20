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
            }
            
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
                SummarizeAllMemories();
                lastSummarizationDay = currentDay;
            }
            
            // CLPA归档：按天数间隔触发
            CheckArchiveInterval(currentDay);
            
            // Debug：每天只输出一次当前状态
            if (Prefs.DevMode && currentDay != lastSummarizationDay)
            {
                int archiveInterval = RimTalkMemoryPatchMod.Settings.archiveIntervalDays;
                Log.Message($"[RimTalk Memory Debug] Day {currentDay}: Waiting for summarization (target hour: {targetHour}, archive interval: {archiveInterval} days)");
            }
        }

        /// <summary>
        /// 为所有殖民者触发每日总结
        /// </summary>
        private void SummarizeAllMemories()
        {
            if (Current.Game == null) return;

            int totalSummarized = 0;

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
                            fourLayerComp.DailySummarization();
                            totalSummarized++;
                        }
                        else
                        {
                            // 兼容旧的记忆组件
                            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
                            if (memoryComp != null)
                            {
                                memoryComp.DailySummarization();
                                totalSummarized++;
                            }
                        }
                    }
                }
            }

            Log.Message($"[RimTalk Memory] ✅ Daily summarization complete for {totalSummarized} colonists");
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
            Scribe_Deep.Look(ref commonKnowledge, "commonKnowledge");
            Scribe_Deep.Look(ref conversationCache, "conversationCache");
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (commonKnowledge == null)
                    commonKnowledge = new CommonKnowledgeLibrary();
                if (conversationCache == null)
                    conversationCache = new ConversationCache();
            }
        }
    }
}
