using UnityEngine;
using Verse;
using RimTalk.Memory;

namespace RimTalk.MemoryPatch
{
    public class RimTalkMemoryPatchSettings : ModSettings
    {
        // 四层记忆容量设置
        public int maxActiveMemories = 3;        // ABM 容量
        public int maxSituationalMemories = 20;  // SCM 容量
        public int maxEventLogMemories = 50;     // ELS 容量
        // CLPA 无限制
        
        // 衰减速率设置
        public float scmDecayRate = 0.01f;   // SCM 衰减率（1% 每小时）
        public float elsDecayRate = 0.005f;  // ELS 衰减率（0.5% 每小时）
        public float clpaDecayRate = 0.001f; // CLPA 衰减率（0.1% 每小时）
        
        // 总结设置
        public bool enableDailySummarization = true;  // 启用每日总结
        public int summarizationHour = 0;             // 总结触发时间（游戏小时）
        public bool useAISummarization = true;        // 使用 AI 总结
        public int maxSummaryLength = 80;             // 最大总结长度
        
        // UI 设置
        public bool enableMemoryUI = true;
        
        // 记忆类型开关
        // - 对话记忆（Conversation）：记录RimTalk生成的完整对话内容
        // - 行动记忆（Action）：记录工作、战斗等行为
        public bool enableActionMemory = true;        // 行动记忆（工作、战斗）
        public bool enableConversationMemory = true;  // 对话记忆（RimTalk对话内容）
        
        // 兼容旧设置（用于数据迁移）
        [System.Obsolete("使用四层架构设置")]
        public int maxShortTermMemories = 20;
        [System.Obsolete("使用四层架构设置")]
        public int maxLongTermMemories = 50;
        [System.Obsolete("使用四层架构设置")]
        public float memoryDecayRate = 0.01f;

        public override void ExposeData()
        {
            base.ExposeData();
            
            // 四层记忆容量
            Scribe_Values.Look(ref maxActiveMemories, "fourLayer_maxActiveMemories", 3);
            Scribe_Values.Look(ref maxSituationalMemories, "fourLayer_maxSituationalMemories", 20);
            Scribe_Values.Look(ref maxEventLogMemories, "fourLayer_maxEventLogMemories", 50);
            
            // 衰减速率
            Scribe_Values.Look(ref scmDecayRate, "fourLayer_scmDecayRate", 0.01f);
            Scribe_Values.Look(ref elsDecayRate, "fourLayer_elsDecayRate", 0.005f);
            Scribe_Values.Look(ref clpaDecayRate, "fourLayer_clpaDecayRate", 0.001f);
            
            // 总结设置
            Scribe_Values.Look(ref enableDailySummarization, "fourLayer_enableDailySummarization", true);
            Scribe_Values.Look(ref summarizationHour, "fourLayer_summarizationHour", 0);
            Scribe_Values.Look(ref useAISummarization, "fourLayer_useAISummarization", true);
            Scribe_Values.Look(ref maxSummaryLength, "fourLayer_maxSummaryLength", 80);
            
            // UI 设置
            Scribe_Values.Look(ref enableMemoryUI, "memoryPatch_enableMemoryUI", true);
            
            // 记忆类型开关
            Scribe_Values.Look(ref enableActionMemory, "memoryPatch_enableActionMemory", true);
            Scribe_Values.Look(ref enableConversationMemory, "memoryPatch_enableConversationMemory", true);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            
            // 使用滚动视图以容纳所有内容
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 1100f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listingStandard.Begin(viewRect);

            // === 容量设置 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("四层记忆容量");
            Text.Font = GameFont.Small;
            
            listingStandard.Label($"ABM（超短期）: {maxActiveMemories} 条");
            maxActiveMemories = (int)listingStandard.Slider(maxActiveMemories, 2, 5);
            
            listingStandard.Label($"SCM（短期）: {maxSituationalMemories} 条");
            maxSituationalMemories = (int)listingStandard.Slider(maxSituationalMemories, 10, 50);
            
            listingStandard.Label($"ELS（中期）: {maxEventLogMemories} 条");
            maxEventLogMemories = (int)listingStandard.Slider(maxEventLogMemories, 20, 100);
            
            GUI.color = Color.gray;
            listingStandard.Label("CLPA（长期）: 无限制");
            GUI.color = Color.white;
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // === 衰减设置 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("记忆衰减速率（每小时）");
            Text.Font = GameFont.Small;
            
            listingStandard.Label($"SCM: {scmDecayRate:P1}");
            scmDecayRate = listingStandard.Slider(scmDecayRate, 0.001f, 0.05f);
            
            listingStandard.Label($"ELS: {elsDecayRate:P1}");
            elsDecayRate = listingStandard.Slider(elsDecayRate, 0.0005f, 0.02f);
            
            listingStandard.Label($"CLPA: {clpaDecayRate:P1}");
            clpaDecayRate = listingStandard.Slider(clpaDecayRate, 0.0001f, 0.01f);
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // === 总结设置 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("AI 自动总结");
            Text.Font = GameFont.Small;
            
            listingStandard.CheckboxLabeled("启用每日总结（SCM → ELS）", ref enableDailySummarization);
            
            if (enableDailySummarization)
            {
                GUI.color = new Color(0.8f, 0.8f, 1f);
                listingStandard.Label($"  触发条件：每天 {summarizationHour}:00（游戏时间）");
                GUI.color = Color.white;
                summarizationHour = (int)listingStandard.Slider(summarizationHour, 0, 23);
                
                GUI.color = Color.gray;
                listingStandard.Label("  说明：每天到达设定时间时，自动将 SCM 记忆总结为 ELS");
                GUI.color = Color.white;
            }
            
            listingStandard.Gap();
            listingStandard.CheckboxLabeled("使用 AI 总结（需要 RimTalk）", ref useAISummarization);
            
            if (useAISummarization)
            {
                listingStandard.Label($"  最大总结长度: {maxSummaryLength} 字");
                maxSummaryLength = (int)listingStandard.Slider(maxSummaryLength, 50, 200);
            }
            else
            {
                GUI.color = Color.yellow;
                listingStandard.Label("  使用简单规则总结（不消耗 token）");
                GUI.color = Color.white;
            }
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // === 记忆类型开关 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("记忆类型");
            Text.Font = GameFont.Small;
            
            listingStandard.CheckboxLabeled("行动记忆（工作、战斗）", ref enableActionMemory);
            listingStandard.CheckboxLabeled("对话记忆（RimTalk 对话）", ref enableConversationMemory);
            
            GUI.color = Color.gray;
            listingStandard.Label("注：观察记忆已移除，因实现成本过高且与现有类型重叠");
            GUI.color = Color.white;
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // === 其他设置 ===
            listingStandard.CheckboxLabeled("启用记忆界面", ref enableMemoryUI);
            
            listingStandard.Gap();
            GUI.color = Color.gray;
            listingStandard.Label("对话功能请在 RimTalk 原版设置中配置");
            listingStandard.Label("记忆不会自动生成对话，仅作为上下文使用");
            GUI.color = Color.white;

            listingStandard.End();
            Widgets.EndScrollView();
        }
        
        private static Vector2 scrollPosition = Vector2.zero;
    }
}
