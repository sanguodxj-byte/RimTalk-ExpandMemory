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

        // ⭐ 冷启动缓冲：本次会话开始时间（不保存）
        private int sessionStartTick = -1;
        private const int COLD_START_DELAY = 200; // 启动后延迟200 ticks (约3秒) 再开始运作

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
                lastDecayTick = Find.TickManager.TicksGame;

                // 检查工作会话超时
                // 已迁移至 JobMemoryCapturer 的 CompTick 中自动处理

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
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref lastDecayTick, "lastDecayTick", 0);

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
                // _maintenanceQueue 是 readonly，构造时已初始化

                // ⭐ 兼容性处理：旧存档初始化
                // 如果是旧存档（没有记录过日期），将日期初始化为当前日期，防止立即触发归档/总结
                int currentDay = GenDate.DaysPassed;

                Log.Message($"[RimTalk Memory] MemoryManager loaded successfully.");
            }
        }

        // --- 待解耦 ---
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
    }
}
