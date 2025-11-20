using System.Collections.Generic;
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
        
        private int lastSummarizationDay = -1; // 上次总结的日期

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

        /// <summary>
        /// 静态方法获取常识库
        /// </summary>
        public static CommonKnowledgeLibrary GetCommonKnowledge()
        {
            if (Current.Game == null) return new CommonKnowledgeLibrary();
            
            var manager = Find.World.GetComponent<MemoryManager>();
            return manager?.CommonKnowledge ?? new CommonKnowledgeLibrary();
        }

        public MemoryManager(World world) : base(world)
        {
            commonKnowledge = new CommonKnowledgeLibrary();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // 每小时衰减记忆活跃度
            if (Find.TickManager.TicksGame - lastDecayTick > DecayInterval)
            {
                DecayAllMemories();
                lastDecayTick = Find.TickManager.TicksGame;
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
            
            // 当天第一次检查，且时间在目标小时
            if (currentDay != lastSummarizationDay && currentHour == targetHour)
            {
                Log.Message($"[RimTalk Memory] 🌙 Day {currentDay}, Hour {currentHour}: Triggering daily summarization for all colonists");
                SummarizeAllMemories();
                lastSummarizationDay = currentDay;
            }
            
            // Debug：每天只输出一次当前状态
            if (Prefs.DevMode && currentDay != lastSummarizationDay && currentHour == 0)
            {
                Log.Message($"[RimTalk Memory Debug] Day {currentDay}: Waiting for hour {targetHour} to summarize (current: {currentHour})");
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastDecayTick, "lastDecayTick", 0);
            Scribe_Values.Look(ref lastSummarizationDay, "lastSummarizationDay", -1);
            Scribe_Deep.Look(ref commonKnowledge, "commonKnowledge");
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (commonKnowledge == null)
                    commonKnowledge = new CommonKnowledgeLibrary();
            }
        }
    }
}
