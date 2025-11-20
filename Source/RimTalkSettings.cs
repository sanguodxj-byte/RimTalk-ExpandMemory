using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.Memory;
using RimTalk.Memory.UI;

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
        
        // === 独立 AI 配置 ===
        public bool useRimTalkAIConfig = true;        // 优先使用 RimTalk 的 AI 配置（默认开启）
        public string independentApiKey = "";         // 独立 API Key
        public string independentApiUrl = "";         // 独立 API URL
        public string independentModel = "gpt-3.5-turbo";  // 独立模型
        public string independentProvider = "OpenAI"; // 独立提供商（OpenAI/Google）
        
        // UI 设置
        public bool enableMemoryUI = true;
        
        // 记忆类型开关
        public bool enableActionMemory = true;        // 行动记忆（工作、战斗）
        public bool enableConversationMemory = true;  // 对话记忆（RimTalk对话内容）

        // === 动态注入设置 ===
        public bool useDynamicInjection = true;       // 使用动态注入（默认开启）
        public int maxInjectedMemories = 10;          // 最大注入记忆数量
        public int maxInjectedKnowledge = 5;          // 最大注入常识数量
        
        // 动态注入权重配置
        public float weightTimeDecay = 0.3f;          // 时间衰减权重
        public float weightImportance = 0.3f;         // 重要性权重
        public float weightKeywordMatch = 0.4f;       // 关键词匹配权重

        // UI折叠状态（不保存到存档）
        private static bool expandDynamicInjection = true;
        private static bool expandMemoryCapacity = false;
        private static bool expandDecayRates = false;
        private static bool expandSummarization = false;
        private static bool expandAIConfig = false;
        private static bool expandMemoryTypes = false;

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
            
            // 独立 AI 配置
            Scribe_Values.Look(ref useRimTalkAIConfig, "ai_useRimTalkConfig", true);
            Scribe_Values.Look(ref independentApiKey, "ai_independentApiKey", "");
            Scribe_Values.Look(ref independentApiUrl, "ai_independentApiUrl", "");
            Scribe_Values.Look(ref independentModel, "ai_independentModel", "gpt-3.5-turbo");
            Scribe_Values.Look(ref independentProvider, "ai_independentProvider", "OpenAI");
            
            // UI 设置
            Scribe_Values.Look(ref enableMemoryUI, "memoryPatch_enableMemoryUI", true);
            
            // 记忆类型开关
            Scribe_Values.Look(ref enableActionMemory, "memoryPatch_enableActionMemory", true);
            Scribe_Values.Look(ref enableConversationMemory, "memoryPatch_enableConversationMemory", true);
            
            // 动态注入设置
            Scribe_Values.Look(ref useDynamicInjection, "dynamic_useDynamicInjection", true);
            Scribe_Values.Look(ref maxInjectedMemories, "dynamic_maxInjectedMemories", 10);
            Scribe_Values.Look(ref maxInjectedKnowledge, "dynamic_maxInjectedKnowledge", 5);
            Scribe_Values.Look(ref weightTimeDecay, "dynamic_weightTimeDecay", 0.3f);
            Scribe_Values.Look(ref weightImportance, "dynamic_weightImportance", 0.3f);
            Scribe_Values.Look(ref weightKeywordMatch, "dynamic_weightKeywordMatch", 0.4f);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            
            // 使用滚动视图以容纳所有内容
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 1600f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listingStandard.Begin(viewRect);

            // === 常识库管理 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("常识库管理");
            Text.Font = GameFont.Small;
            
            GUI.color = Color.gray;
            listingStandard.Label("常识库可用于向AI注入世界观、背景知识等通用信息");
            GUI.color = Color.white;
            
            listingStandard.Gap(6f);
            
            Rect knowledgeButtonRect = listingStandard.GetRect(35f);
            if (Widgets.ButtonText(knowledgeButtonRect, "打开常识库管理"))
            {
                OpenCommonKnowledgeDialog();
            }
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // === 动态注入设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "动态注入系统",
                ref expandDynamicInjection,
                () => DrawDynamicInjectionSettings(listingStandard)
            );

            // === 容量设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "四层记忆容量",
                ref expandMemoryCapacity,
                () => DrawMemoryCapacitySettings(listingStandard)
            );

            // === 衰减设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "记忆衰减速率",
                ref expandDecayRates,
                () => DrawDecaySettings(listingStandard)
            );

            // === 总结设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "AI 自动总结",
                ref expandSummarization,
                () => DrawSummarizationSettings(listingStandard)
            );

            // === AI 配置 ===
            if (useAISummarization)
            {
                DrawCollapsibleSection(
                    listingStandard,
                    "AI API 配置",
                    ref expandAIConfig,
                    () => DrawAIConfigSettings(listingStandard)
                );
            }

            // === 记忆类型开关 ===
            DrawCollapsibleSection(
                listingStandard,
                "记忆类型",
                ref expandMemoryTypes,
                () => DrawMemoryTypesSettings(listingStandard)
            );

            // === 其他设置 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("其他设置");
            Text.Font = GameFont.Small;
            
            listingStandard.CheckboxLabeled("启用记忆界面", ref enableMemoryUI);
            
            listingStandard.Gap();
            
            // 注入内容预览器
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            listingStandard.Label("调试工具：");
            GUI.color = Color.white;
            
            Rect previewButtonRect = listingStandard.GetRect(35f);
            if (Widgets.ButtonText(previewButtonRect, "打开注入内容预览器"))
            {
                Find.WindowStack.Add(new RimTalk.Memory.Debug.Dialog_InjectionPreview());
            }
            
            GUI.color = Color.gray;
            listingStandard.Label("实时查看将要注入给AI的记忆和常识（需进入游戏）");
            GUI.color = Color.white;
            
            listingStandard.Gap();
            GUI.color = Color.gray;
            listingStandard.Label("提示：本 Mod 完全独立，不依赖 RimTalk");
            GUI.color = Color.white;

            listingStandard.End();
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制可折叠的设置区块
        /// </summary>
        private void DrawCollapsibleSection(Listing_Standard listing, string title, ref bool expanded, System.Action drawContent)
        {
            Rect headerRect = listing.GetRect(30f);
            
            // 绘制背景
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            // 绘制标题
            Text.Font = GameFont.Medium;
            Rect labelRect = new Rect(headerRect.x + 30f, headerRect.y + 3f, headerRect.width - 30f, headerRect.height);
            Widgets.Label(labelRect, title);
            Text.Font = GameFont.Small;
            
            // 绘制展开/折叠图标
            Rect iconRect = new Rect(headerRect.x + 5f, headerRect.y + 7f, 20f, 20f);
            if (Widgets.ButtonImage(iconRect, expanded ? TexButton.Collapse : TexButton.Reveal))
            {
                expanded = !expanded;
            }
            
            listing.Gap(3f);
            
            // 如果展开，绘制内容
            if (expanded)
            {
                listing.Gap(3f);
                drawContent?.Invoke();
                listing.Gap(6f);
            }
            
            listing.GapLine();
        }

        /// <summary>
        /// 绘制动态注入设置
        /// </summary>
        private void DrawDynamicInjectionSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("启用动态记忆注入（推荐）", ref useDynamicInjection);
            
            if (useDynamicInjection)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  根据时间、重要性和关键词匹配动态选择最相关的记忆");
                GUI.color = Color.white;
                
                listing.Label($"  最大注入记忆数: {maxInjectedMemories}");
                maxInjectedMemories = (int)listing.Slider(maxInjectedMemories, 1, 20);
                
                listing.Label($"  最大注入常识数: {maxInjectedKnowledge}");
                maxInjectedKnowledge = (int)listing.Slider(maxInjectedKnowledge, 1, 10);
                
                listing.Gap();
                
                Text.Font = GameFont.Tiny;
                listing.Label("评分权重配置:");
                Text.Font = GameFont.Small;
                
                listing.Label($"  时间衰减: {weightTimeDecay:P0}");
                weightTimeDecay = listing.Slider(weightTimeDecay, 0f, 1f);
                
                listing.Label($"  重要性: {weightImportance:P0}");
                weightImportance = listing.Slider(weightImportance, 0f, 1f);
                
                listing.Label($"  关键词匹配: {weightKeywordMatch:P0}");
                weightKeywordMatch = listing.Slider(weightKeywordMatch, 0f, 1f);
                
                // 应用权重到动态注入系统
                DynamicMemoryInjection.Weights.TimeDecay = weightTimeDecay;
                DynamicMemoryInjection.Weights.Importance = weightImportance;
                DynamicMemoryInjection.Weights.KeywordMatch = weightKeywordMatch;
            }
            else
            {
                GUI.color = Color.yellow;
                listing.Label("  将使用静态注入（按层级顺序）");
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// 绘制记忆容量设置
        /// </summary>
        private void DrawMemoryCapacitySettings(Listing_Standard listing)
        {
            listing.Label($"ABM（超短期）: {maxActiveMemories} 条");
            maxActiveMemories = (int)listing.Slider(maxActiveMemories, 2, 5);
            
            listing.Label($"SCM（短期）: {maxSituationalMemories} 条");
            maxSituationalMemories = (int)listing.Slider(maxSituationalMemories, 10, 50);
            
            listing.Label($"ELS（中期）: {maxEventLogMemories} 条");
            maxEventLogMemories = (int)listing.Slider(maxEventLogMemories, 20, 100);
            
            GUI.color = Color.gray;
            listing.Label("CLPA（长期）: 无限制");
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制衰减速率设置
        /// </summary>
        private void DrawDecaySettings(Listing_Standard listing)
        {
            listing.Label($"SCM（每小时）: {scmDecayRate:P1}");
            scmDecayRate = listing.Slider(scmDecayRate, 0.001f, 0.05f);
            
            listing.Label($"ELS（每小时）: {elsDecayRate:P1}");
            elsDecayRate = listing.Slider(elsDecayRate, 0.0005f, 0.02f);
            
            listing.Label($"CLPA（每小时）: {clpaDecayRate:P1}");
            clpaDecayRate = listing.Slider(clpaDecayRate, 0.0001f, 0.01f);
        }

        /// <summary>
        /// 绘制总结设置
        /// </summary>
        private void DrawSummarizationSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("启用每日总结（SCM → ELS）", ref enableDailySummarization);
            
            if (enableDailySummarization)
            {
                GUI.color = new Color(0.8f, 0.8f, 1f);
                listing.Label($"  触发条件：每天 {summarizationHour}:00（游戏时间）");
                GUI.color = Color.white;
                summarizationHour = (int)listing.Slider(summarizationHour, 0, 23);
                
                GUI.color = Color.gray;
                listing.Label("  说明：每天到达设定时间时，自动将 SCM 记忆总结为 ELS");
                GUI.color = Color.white;
            }
            
            listing.Gap();
            listing.CheckboxLabeled("使用 AI 总结", ref useAISummarization);
            
            if (useAISummarization)
            {
                listing.Label($"  最大总结长度: {maxSummaryLength} 字");
                maxSummaryLength = (int)listing.Slider(maxSummaryLength, 50, 200);
            }
            else
            {
                GUI.color = Color.yellow;
                listing.Label("  使用简单规则总结（不消耗 token）");
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// 绘制AI配置设置
        /// </summary>
        private void DrawAIConfigSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("优先使用 RimTalk 的 AI 配置", ref useRimTalkAIConfig);
            
            GUI.color = Color.gray;
            if (useRimTalkAIConfig)
            {
                listing.Label("  将尝试读取 RimTalk Mod 的 API 配置");
                listing.Label("  如果 RimTalk 未安装或未配置，将使用下方的独立配置");
            }
            else
            {
                listing.Label("  将使用下方的独立配置，不依赖 RimTalk");
            }
            GUI.color = Color.white;
            
            listing.Gap();
            
            // 独立配置区域
            GUI.color = new Color(1f, 1f, 0.8f);
            listing.Label("=== 独立 AI 配置 ===");
            GUI.color = Color.white;
            
            listing.Label("提供商：");
            Rect providerRect = listing.GetRect(30f);
            float buttonWidth = providerRect.width / 3f;
            
            // OpenAI
            if (Widgets.ButtonText(new Rect(providerRect.x, providerRect.y, buttonWidth - 3f, providerRect.height), 
                independentProvider == "OpenAI" ? "OpenAI ✓" : "OpenAI"))
            {
                independentProvider = "OpenAI";
                independentApiUrl = "https://api.openai.com/v1/chat/completions";
                independentModel = "gpt-3.5-turbo";
            }
            
            // DeepSeek
            if (Widgets.ButtonText(new Rect(providerRect.x + buttonWidth + 2f, providerRect.y, buttonWidth - 3f, providerRect.height), 
                independentProvider == "DeepSeek" ? "DeepSeek ✓" : "DeepSeek"))
            {
                independentProvider = "DeepSeek";
                independentApiUrl = "https://api.deepseek.com/v1/chat/completions";
                independentModel = "deepseek-chat";
            }
            
            // Google
            if (Widgets.ButtonText(new Rect(providerRect.x + buttonWidth * 2 + 4f, providerRect.y, buttonWidth - 3f, providerRect.height), 
                independentProvider == "Google" ? "Google ✓" : "Google"))
            {
                independentProvider = "Google";
                independentApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
                independentModel = "gemini-pro";
            }
            
            listing.Gap();
            
            listing.Label("API Key:");
            independentApiKey = listing.TextEntry(independentApiKey);
            
            listing.Gap();
            
            listing.Label("API URL:");
            independentApiUrl = listing.TextEntry(independentApiUrl);
            
            listing.Gap();
            
            listing.Label("模型名称:");
            independentModel = listing.TextEntry(independentModel);
            
            GUI.color = Color.gray;
            listing.Label($"  OpenAI: gpt-3.5-turbo, gpt-4, gpt-4-turbo");
            listing.Label($"  DeepSeek: deepseek-chat, deepseek-coder");
            listing.Label($"  Google: gemini-pro, gemini-1.5-flash");
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制记忆类型设置
        /// </summary>
        private void DrawMemoryTypesSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("行动记忆（工作、战斗）", ref enableActionMemory);
            listing.CheckboxLabeled("对话记忆（RimTalk 对话）", ref enableConversationMemory);
        }
        
        private void OpenCommonKnowledgeDialog()
        {
            if (Current.Game == null)
            {
                Messages.Message("需要进入游戏后才能管理常识库", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var memoryManager = Find.World.GetComponent<MemoryManager>();
            if (memoryManager == null)
            {
                Messages.Message("无法找到记忆管理器", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Dialog_CommonKnowledge(memoryManager.CommonKnowledge));
        }
        
        private static Vector2 scrollPosition = Vector2.zero;
    }
}
