using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.Memory;
using RimTalk.Memory.UI;
using System.Collections.Generic; // ? 添加
using System.Linq;

namespace RimTalk.MemoryPatch
{
    public class RimTalkMemoryPatchSettings : ModSettings
    {
        // ? 新增：提示词规范化规则
        /// <summary>
        /// 替换规则定义
        /// </summary>
        public class ReplacementRule : IExposable
        {
            public string pattern = "";
            public string replacement = "";
            public bool isEnabled = true;
            
            public ReplacementRule() { }
            
            public ReplacementRule(string pattern, string replacement, bool isEnabled = true)
            {
                this.pattern = pattern;
                this.replacement = replacement;
                this.isEnabled = isEnabled;
            }
            
            public void ExposeData()
            {
                Scribe_Values.Look(ref pattern, "pattern", "");
                Scribe_Values.Look(ref replacement, "replacement", "");
                Scribe_Values.Look(ref isEnabled, "isEnabled", true);
            }
        }
        
        // ? 提示词规范化规则列表
        public List<ReplacementRule> normalizationRules = new List<ReplacementRule>
        {
            new ReplacementRule(@"\(Player\)", "Master", true),
            new ReplacementRule(@"\(玩家\)", "主人", true)
        };
        
        // 四层记忆容量配置
        public int maxActiveMemories = 6;
        public int maxSituationalMemories = 20;
        public int maxEventLogMemories = 50;
        
        // 衰减速率设置
        public float scmDecayRate = 0.01f;
        public float elsDecayRate = 0.005f;
        public float clpaDecayRate = 0.001f;
        
        // 总结设置
        public bool enableDailySummarization = true;
        public int summarizationHour = 0;
        public bool useAISummarization = true;
        public int maxSummaryLength = 80;
        
        // CLPA 归档设置
        public bool enableAutoArchive = true;
        public int archiveIntervalDays = 7;
        public int maxArchiveMemories = 30;

        // AI 配置
        public bool useRimTalkAIConfig = true;
        public string independentApiKey = "";
        public string independentApiUrl = "";
        public string independentModel = "gpt-3.5-turbo";
        public string independentProvider = "OpenAI";
        public bool enablePromptCaching = true;

        // UI 设置
        public bool enableMemoryUI = true;
        
        // 记忆类型开关
        public bool enableActionMemory = true;
        public bool enableConversationMemory = true;
        
        // Pawn状态常识自动生成
        public bool enablePawnStatusKnowledge = false;
        
        // 事件记录常识自动生成
        public bool enableEventRecordKnowledge = false;

        // 对话缓存设置
        public bool enableConversationCache = true;
        public int conversationCacheSize = 200;
        public int conversationCacheExpireDays = 14;
        
        // 提示词缓存设置
        public bool enablePromptCache = true;
        public int promptCacheSize = 100;
        public int promptCacheExpireMinutes = 60;

        // 动态注入设置
        public bool useDynamicInjection = true;
        public int maxInjectedMemories = 10;
        public int maxInjectedKnowledge = 5;
        
        // 动态注入权重配置
        public float weightTimeDecay = 0.3f;
        public float weightImportance = 0.3f;
        public float weightKeywordMatch = 0.4f;
        
        // 注入阈值设置
        public float memoryScoreThreshold = 0.15f;
        public float knowledgeScoreThreshold = 0.1f;
        
        // 自适应阈值设置
        public bool enableAdaptiveThreshold = false;
        public bool autoApplyAdaptiveThreshold = false;
        
        // 主动记忆召回
        public bool enableProactiveRecall = false;
        public float recallTriggerChance = 0.15f;
        
        // 常识库权重配置
        public float knowledgeBaseScore = 0.05f;
        public float knowledgeJaccardWeight = 0.7f;
        public float knowledgeTagWeight = 0.3f;
        public float knowledgeMatchBonus = 0.08f;

        // Vector Enhancement Settings
        public bool enableVectorEnhancement = false;
        public float vectorSimilarityThreshold = 0.75f;
        public int maxVectorResults = 5;
        
        // Cloud Embedding Settings
        public string embeddingApiKey = "";
        public string embeddingApiUrl = "https://api.openai.com/v1/embeddings";
        public string embeddingModel = "text-embedding-3-small";

        // Knowledge Matching Settings
        public bool enableKnowledgeChaining = true;
        public int maxChainingRounds = 2;
        

        // UI折叠状态
        private static bool expandDynamicInjection = true;
        private static bool expandMemoryCapacity = false;
        private static bool expandDecayRates = false;
        private static bool expandSummarization = false;
        private static bool expandAIConfig = true;
        private static bool expandMemoryTypes = false;
        private static bool expandExperimentalFeatures = true;
        private static bool expandVectorEnhancement = true;
        
        private static Vector2 scrollPosition = Vector2.zero;

        public override void ExposeData()
        {
            base.ExposeData();
            
            // ? 序列化提示词规范化规则
            Scribe_Collections.Look(ref normalizationRules, "normalizationRules", LookMode.Deep);
            
            // ? 兼容性：如果加载后为 null，初始化默认规则
            if (Scribe.mode == LoadSaveMode.PostLoadInit && normalizationRules == null)
            {
                normalizationRules = new List<ReplacementRule>
                {
                    new ReplacementRule(@"\(Player\)", "Master", true),
                    new ReplacementRule(@"\(玩家\)", "主人", true)
                };
            }
            
            Scribe_Values.Look(ref maxActiveMemories, "fourLayer_maxActiveMemories", 6);
            Scribe_Values.Look(ref maxSituationalMemories, "fourLayer_maxSituationalMemories", 20);
            Scribe_Values.Look(ref maxEventLogMemories, "fourLayer_maxEventLogMemories", 50);
            
            Scribe_Values.Look(ref scmDecayRate, "fourLayer_scmDecayRate", 0.01f);
            Scribe_Values.Look(ref elsDecayRate, "fourLayer_elsDecayRate", 0.005f);
            Scribe_Values.Look(ref clpaDecayRate, "fourLayer_clpaDecayRate", 0.001f);
            
            Scribe_Values.Look(ref enableDailySummarization, "fourLayer_enableDailySummarization", true);
            Scribe_Values.Look(ref summarizationHour, "fourLayer_summarizationHour", 0);
            Scribe_Values.Look(ref useAISummarization, "fourLayer_useAISummarization", true);
            Scribe_Values.Look(ref maxSummaryLength, "fourLayer_maxSummaryLength", 80);
            
            Scribe_Values.Look(ref enableAutoArchive, "fourLayer_enableAutoArchive", true);
            Scribe_Values.Look(ref archiveIntervalDays, "fourLayer_archiveIntervalDays", 7);
            Scribe_Values.Look(ref maxArchiveMemories, "fourLayer_maxArchiveMemories", 30);

            Scribe_Values.Look(ref useRimTalkAIConfig, "ai_useRimTalkConfig", true);
            Scribe_Values.Look(ref independentApiKey, "ai_independentApiKey", "");
            Scribe_Values.Look(ref independentApiUrl, "ai_independentApiUrl", "");
            Scribe_Values.Look(ref independentModel, "ai_independentModel", "gpt-3.5-turbo");
            Scribe_Values.Look(ref independentProvider, "ai_independentProvider", "OpenAI");
            Scribe_Values.Look(ref enablePromptCaching, "ai_enablePromptCaching", true);

            Scribe_Values.Look(ref enableMemoryUI, "memoryPatch_enableMemoryUI", true);
            Scribe_Values.Look(ref enableActionMemory, "memoryPatch_enableActionMemory", true);
            Scribe_Values.Look(ref enableConversationMemory, "memoryPatch_enableConversationMemory", true);
            Scribe_Values.Look(ref enablePawnStatusKnowledge, "pawnStatus_enablePawnStatusKnowledge", false);
            Scribe_Values.Look(ref enableEventRecordKnowledge, "eventRecord_enableEventRecordKnowledge", false);

            Scribe_Values.Look(ref enableConversationCache, "cache_enableConversationCache", true);
            Scribe_Values.Look(ref conversationCacheSize, "cache_conversationCacheSize", 200);
            Scribe_Values.Look(ref conversationCacheExpireDays, "cache_conversationCacheExpireDays", 14);
            Scribe_Values.Look(ref enablePromptCache, "cache_enablePromptCache", true);
            Scribe_Values.Look(ref promptCacheSize, "cache_promptCacheSize", 100);
            Scribe_Values.Look(ref promptCacheExpireMinutes, "cache_promptCacheExpireMinutes", 60);
            
            Scribe_Values.Look(ref useDynamicInjection, "dynamic_useDynamicInjection", true);
            Scribe_Values.Look(ref maxInjectedMemories, "dynamic_maxInjectedMemories", 10);
            Scribe_Values.Look(ref maxInjectedKnowledge, "dynamic_maxInjectedKnowledge", 5);
            Scribe_Values.Look(ref weightTimeDecay, "dynamic_weightTimeDecay", 0.3f);
            Scribe_Values.Look(ref weightImportance, "dynamic_weightImportance", 0.3f);
            Scribe_Values.Look(ref weightKeywordMatch, "dynamic_weightKeywordMatch", 0.4f);
            Scribe_Values.Look(ref memoryScoreThreshold, "dynamic_memoryScoreThreshold", 0.15f);
            Scribe_Values.Look(ref knowledgeScoreThreshold, "dynamic_knowledgeScoreThreshold", 0.1f);
            
            Scribe_Values.Look(ref enableAdaptiveThreshold, "adaptive_enableAdaptiveThreshold", false);
            Scribe_Values.Look(ref autoApplyAdaptiveThreshold, "adaptive_autoApplyAdaptiveThreshold", false);
            Scribe_Values.Look(ref enableProactiveRecall, "recall_enableProactiveRecall", false);
            Scribe_Values.Look(ref recallTriggerChance, "recall_triggerChance", 0.15f);

            Scribe_Values.Look(ref knowledgeBaseScore, "knowledge_baseScore", 0.05f);
            Scribe_Values.Look(ref knowledgeJaccardWeight, "knowledge_jaccardWeight", 0.7f);
            Scribe_Values.Look(ref knowledgeTagWeight, "knowledge_tagWeight", 0.3f);
            Scribe_Values.Look(ref knowledgeMatchBonus, "knowledge_matchBonus", 0.08f);

            // Vector Enhancement
            Scribe_Values.Look(ref enableVectorEnhancement, "vector_enableVectorEnhancement", false);
            Scribe_Values.Look(ref vectorSimilarityThreshold, "vector_vectorSimilarityThreshold", 0.75f);
            Scribe_Values.Look(ref maxVectorResults, "vector_maxVectorResults", 5);
            
            Scribe_Values.Look(ref embeddingApiKey, "vector_embeddingApiKey", "");
            Scribe_Values.Look(ref embeddingApiUrl, "vector_embeddingApiUrl", "https://api.openai.com/v1/embeddings");
            Scribe_Values.Look(ref embeddingModel, "vector_embeddingModel", "text-embedding-3-small");

            // Knowledge Matching
            Scribe_Values.Look(ref enableKnowledgeChaining, "knowledge_enableKnowledgeChaining", true);
            Scribe_Values.Look(ref maxChainingRounds, "knowledge_maxChainingRounds", 2);
            
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 2400f); // ? 增加高度以容纳新内容
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listingStandard.Begin(viewRect);

            // ? 新增：提示词规范化设置
            Text.Font = GameFont.Medium;
            listingStandard.Label("提示词规范化");
            Text.Font = GameFont.Small;
            
            GUI.color = Color.gray;
            listingStandard.Label("自定义替换规则，在发送给 AI 前自动处理提示词");
            GUI.color = Color.white;
            
            listingStandard.Gap(6f);
            
            DrawPromptNormalizationSettings(listingStandard);
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // 常识库管理
            Text.Font = GameFont.Medium;
            listingStandard.Label("常识库管理");
            Text.Font = GameFont.Small;
            
            GUI.color = Color.gray;
            listingStandard.Label("管理全局常识库，为AI对话提供背景知识");
            GUI.color = Color.white;
            
            listingStandard.Gap(6f);
            
            Rect knowledgeButtonRect = listingStandard.GetRect(35f);
            if (Widgets.ButtonText(knowledgeButtonRect, "打开常识库"))
            {
                OpenCommonKnowledgeDialog();
            }
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // 动态注入设置
            DrawCollapsibleSection(listingStandard, "动态注入设置", ref expandDynamicInjection, () => DrawDynamicInjectionSettings(listingStandard));
            DrawCollapsibleSection(listingStandard, "?? 向量增强设置", ref expandVectorEnhancement, () => DrawVectorEnhancementSettings(listingStandard));
            DrawCollapsibleSection(listingStandard, "记忆容量配置", ref expandMemoryCapacity, () => DrawMemoryCapacitySettings(listingStandard));
            DrawCollapsibleSection(listingStandard, "记忆衰减配置", ref expandDecayRates, () => DrawDecaySettings(listingStandard));
            DrawCollapsibleSection(listingStandard, "记忆总结设置", ref expandSummarization, () => DrawSummarizationSettings(listingStandard));

            if (useAISummarization)
            {
                DrawCollapsibleSection(listingStandard, "AI 配置", ref expandAIConfig, () => DrawAIConfigSettings(listingStandard));
            }

            DrawCollapsibleSection(listingStandard, "记忆类型开关", ref expandMemoryTypes, () => DrawMemoryTypesSettings(listingStandard));
            DrawCollapsibleSection(listingStandard, "?? 实验性功能", ref expandExperimentalFeatures, () => DrawExperimentalFeaturesSettings(listingStandard));

            // 调试工具
            listingStandard.Gap();
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            listingStandard.Label("调试工具");
            GUI.color = Color.white;
            
            Rect previewButtonRect = listingStandard.GetRect(35f);
            if (Widgets.ButtonText(previewButtonRect, "注入预览器"))
            {
                Find.WindowStack.Add(new Memory.Debug.Dialog_InjectionPreview());
            }
            
            GUI.color = Color.gray;
            listingStandard.Label("实时预览记忆/常识注入效果");
            GUI.color = Color.white;

            listingStandard.End();
            Widgets.EndScrollView();
            
            // ? v3.3.2.37: 在设置窗口每帧更新时更新规则（用户修改后立即生效）
            PromptNormalizer.UpdateRules(normalizationRules);
        }

        private void DrawCollapsibleSection(Listing_Standard listing, string title, ref bool expanded, System.Action drawContent)
        {
            Rect headerRect = listing.GetRect(30f);
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            Text.Font = GameFont.Medium;
            Rect labelRect = new Rect(headerRect.x + 30f, headerRect.y + 3f, headerRect.width - 30f, headerRect.height);
            Widgets.Label(labelRect, title);
            Text.Font = GameFont.Small;
            
            Rect iconRect = new Rect(headerRect.x + 5f, headerRect.y + 7f, 20f, 20f);
            if (Widgets.ButtonImage(iconRect, expanded ? TexButton.Collapse : TexButton.Reveal))
            {
                expanded = !expanded;
            }
            
            listing.Gap(3f);
            
            if (expanded)
            {
                listing.Gap(3f);
                drawContent?.Invoke();
                listing.Gap(6f);
            }
            
            listing.GapLine();
        }

        private void DrawDynamicInjectionSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("启用动态注入", ref useDynamicInjection);
            
            if (useDynamicInjection)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  智能选择最相关的记忆和常识注入到AI对话中");
                GUI.color = Color.white;
                
                listing.Gap();
                
                listing.Label($"最大注入记忆数: {maxInjectedMemories}");
                maxInjectedMemories = (int)listing.Slider(maxInjectedMemories, 1, 20);
                
                listing.Label($"最大注入常识数: {maxInjectedKnowledge}");
                maxInjectedKnowledge = (int)listing.Slider(maxInjectedKnowledge, 1, 10);
                
                listing.Gap();
                
                listing.Label($"记忆评分阈值: {memoryScoreThreshold:P0}");
                memoryScoreThreshold = listing.Slider(memoryScoreThreshold, 0f, 1f);
                
                listing.Label($"常识评分阈值: {knowledgeScoreThreshold:P0}");
                knowledgeScoreThreshold = listing.Slider(knowledgeScoreThreshold, 0f, 1f);
            }
        }

        private void DrawMemoryCapacitySettings(Listing_Standard listing)
        {
            listing.Label($"SCM (短期记忆): {maxSituationalMemories} 条");
            maxSituationalMemories = (int)listing.Slider(maxSituationalMemories, 10, 50);
            
            listing.Label($"ELS (中期记忆): {maxEventLogMemories} 条");
            maxEventLogMemories = (int)listing.Slider(maxEventLogMemories, 20, 100);
        }

        private void DrawDecaySettings(Listing_Standard listing)
        {
            listing.Label($"SCM 衰减率: {scmDecayRate:P1}");
            scmDecayRate = listing.Slider(scmDecayRate, 0.001f, 0.05f);
            
            listing.Label($"ELS 衰减率: {elsDecayRate:P1}");
            elsDecayRate = listing.Slider(elsDecayRate, 0.0005f, 0.02f);
        }

        private void DrawSummarizationSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("启用每日总结", ref enableDailySummarization);
            
            if (enableDailySummarization)
            {
                listing.Label($"触发时间: {summarizationHour}时");
                summarizationHour = (int)listing.Slider(summarizationHour, 0, 23);
            }
            
            listing.CheckboxLabeled("启用自动归档", ref enableAutoArchive);
        }

        private void DrawAIConfigSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("优先使用 RimTalk 配置", ref useRimTalkAIConfig);
            
            if (useRimTalkAIConfig)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  将自动跟随 RimTalk Mod 的 AI 配置");
                GUI.color = Color.white;
                listing.Gap();
            }
            
            listing.Gap();
            
            // ? v3.3.8: AI 提供商选择
            listing.Label("AI 提供商:");
            GUI.color = Color.gray;
            listing.Label($"  当前: {independentProvider}");
            GUI.color = Color.white;
            
            // 提供商选择按钮
            Rect providerHeaderRect = listing.GetRect(25f);
            Widgets.DrawBoxSolid(providerHeaderRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.Label(providerHeaderRect.ContractedBy(5f), "选择提供商");
            
            Rect providerButtonRect1 = listing.GetRect(30f);
            float buttonWidth = (providerButtonRect1.width - 20f) / 3f;
            
            // 第一行：OpenAI, DeepSeek, Player2
            Rect openaiRect = new Rect(providerButtonRect1.x, providerButtonRect1.y, buttonWidth, 30f);
            Rect deepseekRect = new Rect(providerButtonRect1.x + buttonWidth + 10f, providerButtonRect1.y, buttonWidth, 30f);
            Rect player2Rect = new Rect(providerButtonRect1.x + 2 * (buttonWidth + 10f), providerButtonRect1.y, buttonWidth, 30f);
            
            bool isOpenAI = independentProvider == "OpenAI";
            bool isDeepSeek = independentProvider == "DeepSeek";
            bool isPlayer2 = independentProvider == "Player2";
            
            GUI.color = isOpenAI ? new Color(0.5f, 1f, 0.5f) : Color.white;
            if (Widgets.ButtonText(openaiRect, "OpenAI"))
            {
                independentProvider = "OpenAI";
                independentModel = "gpt-3.5-turbo";
                independentApiUrl = "https://api.openai.com/v1/chat/completions";
            }
            
            GUI.color = isDeepSeek ? new Color(0.5f, 0.7f, 1f) : Color.white;
            if (Widgets.ButtonText(deepseekRect, "DeepSeek"))
            {
                independentProvider = "DeepSeek";
                independentModel = "deepseek-chat";
                independentApiUrl = "https://api.deepseek.com/v1/chat/completions";
            }
            
            GUI.color = isPlayer2 ? new Color(1f, 0.8f, 0.5f) : Color.white;
            if (Widgets.ButtonText(player2Rect, "Player2"))
            {
                independentProvider = "Player2";
                independentModel = "gpt-4o";
                independentApiUrl = "https://api.player2.game/v1/chat/completions";
            }
            GUI.color = Color.white;
            
            // 第二行：Google, Custom
            Rect providerButtonRect2 = listing.GetRect(30f);
            Rect googleRect = new Rect(providerButtonRect2.x, providerButtonRect2.y, buttonWidth, 30f);
            Rect customRect = new Rect(providerButtonRect2.x + buttonWidth + 10f, providerButtonRect2.y, buttonWidth, 30f);
            
            bool isGoogle = independentProvider == "Google";
            bool isCustom = independentProvider == "Custom";
            
            GUI.color = isGoogle ? new Color(1f, 0.5f, 0.5f) : Color.white;
            if (Widgets.ButtonText(googleRect, "Google"))
            {
                independentProvider = "Google";
                independentModel = "gemini-2.0-flash-exp";
                independentApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            }
            
            GUI.color = isCustom ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
            if (Widgets.ButtonText(customRect, "Custom"))
            {
                independentProvider = "Custom";
                independentModel = "custom-model";
                independentApiUrl = "https://your-api-url.com/v1/chat/completions";
            }
            GUI.color = Color.white;
            
            listing.Gap();
            
            // 提供商说明
            GUI.color = new Color(0.7f, 0.9f, 1f);
            if (independentProvider == "OpenAI")
            {
                listing.Label("?? OpenAI GPT 系列模型，稳定可靠");
                listing.Label("   推荐模型: gpt-3.5-turbo, gpt-4");
            }
            else if (independentProvider == "DeepSeek")
            {
                listing.Label("?? DeepSeek 中文优化模型，性价比高");
                listing.Label("   推荐模型: deepseek-chat, deepseek-coder");
            }
            else if (independentProvider == "Player2")
            {
                listing.Label("?? Player2 游戏优化 AI，支持本地客户端");
                listing.Label("   推荐模型: gpt-4o, gpt-4-turbo");
            }
            else if (independentProvider == "Google")
            {
                listing.Label("?? Google Gemini 系列，多模态能力强");
                listing.Label("   推荐模型: gemini-2.0-flash-exp");
            }
            else if (independentProvider == "Custom")
            {
                listing.Label("?? 自定义 API 端点，支持第三方代理");
                listing.Label("   请手动配置 API URL 和 Model");
            }
            GUI.color = Color.white;
            
            listing.Gap();
            
            // API 配置
            listing.Label("API Key:");
            independentApiKey = listing.TextEntry(independentApiKey);
            
            listing.Label("API URL:");
            independentApiUrl = listing.TextEntry(independentApiUrl);
            
            listing.Label("Model:");
            independentModel = listing.TextEntry(independentModel);
            
            listing.Gap();
            
            // Prompt Caching 选项
            listing.CheckboxLabeled("启用 Prompt Caching", ref enablePromptCaching);
            
            if (enablePromptCaching)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                if (independentProvider == "OpenAI")
                {
                    listing.Label("  ? OpenAI 支持 Prompt Caching (Beta)");
                    listing.Label("  适用模型: gpt-4o, gpt-4-turbo");
                }
                else if (independentProvider == "DeepSeek")
                {
                    listing.Label("  ? DeepSeek 支持 Prompt Caching");
                    listing.Label("  可节省约 50% 费用");
                }
                else if (independentProvider == "Player2")
                {
                    listing.Label("  ? Player2 支持 Prompt Caching");
                    listing.Label("  本地客户端自动缓存");
                }
                else if (independentProvider == "Google")
                {
                    listing.Label("  ? Google Gemini 暂不支持 Prompt Caching");
                }
                else if (independentProvider == "Custom")
                {
                    listing.Label("  ?? 取决于您的自定义 API 实现");
                }
                GUI.color = Color.white;
            }
            
            listing.Gap();
            
            // 配置验证按钮
            Rect validateButtonRect = listing.GetRect(35f);
            if (Widgets.ButtonText(validateButtonRect, "?? 验证配置"))
            {
                ValidateAIConfig();
            }
            
            // 提示信息
            GUI.color = Color.gray;
            listing.Label("提示: 验证将测试 API 连接和配置");
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 验证 AI 配置
        /// </summary>
        private void ValidateAIConfig()
        {
            if (useRimTalkAIConfig)
            {
                Messages.Message("当前使用 RimTalk 配置，无需验证独立配置", MessageTypeDefOf.NeutralEvent);
                return;
            }
            
            if (string.IsNullOrEmpty(independentApiKey))
            {
                Messages.Message("请先输入 API Key", MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (string.IsNullOrEmpty(independentApiUrl))
            {
                Messages.Message("请先输入 API URL", MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (string.IsNullOrEmpty(independentModel))
            {
                Messages.Message("请先输入 Model", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Messages.Message("配置验证中...", MessageTypeDefOf.NeutralEvent);
            
            // 强制重新初始化 AI Summarizer
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Memory.AI.IndependentAISummarizer.ForceReinitialize();
                    
                    if (Memory.AI.IndependentAISummarizer.IsAvailable())
                    {
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            Messages.Message($"? 配置验证成功！提供商: {independentProvider}", MessageTypeDefOf.PositiveEvent);
                        });
                    }
                    else
                    {
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            Messages.Message("? 配置验证失败，请检查 API Key 和 URL", MessageTypeDefOf.RejectInput);
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"AI Config validation failed: {ex.Message}");
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        Messages.Message($"? 验证失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                    });
                }
            });
        }

        private void DrawMemoryTypesSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("行动记忆", ref enableActionMemory);
            listing.CheckboxLabeled("对话记忆", ref enableConversationMemory);
        }

        private void DrawExperimentalFeaturesSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("启用主动记忆召回", ref enableProactiveRecall);
            
            if (enableProactiveRecall)
            {
                listing.Label($"触发概率: {recallTriggerChance:P0}");
                recallTriggerChance = listing.Slider(recallTriggerChance, 0.05f, 0.60f);
            }
        }

        private void DrawVectorEnhancementSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("启用向量增强", ref enableVectorEnhancement);
            if (enableVectorEnhancement)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  使用语义向量检索来增强常识匹配。");
                GUI.color = Color.white;
                listing.Gap();
                
                listing.Label($"相似度阈值: {vectorSimilarityThreshold:F2}");
                vectorSimilarityThreshold = listing.Slider(vectorSimilarityThreshold, 0.5f, 1.0f);
                
                listing.Label($"最大补充结果: {maxVectorResults}");
                maxVectorResults = (int)listing.Slider(maxVectorResults, 1, 15);
                
                listing.Gap();
                
                GUI.color = new Color(1f, 0.9f, 0.7f);
                listing.Label("云端 Embedding 配置");
                GUI.color = Color.white;
                
                listing.Label("API Key:");
                embeddingApiKey = listing.TextEntry(embeddingApiKey);
                
                listing.Label("API URL:");
                embeddingApiUrl = listing.TextEntry(embeddingApiUrl);
                
                listing.Label("Model:");
                embeddingModel = listing.TextEntry(embeddingModel);
                
                GUI.color = Color.gray;
                listing.Label("提示: 留空 API Key 将使用上方 AI 配置中的 API Key");
                GUI.color = Color.white;
            }
            
            listing.Gap();
            listing.GapLine();
            
            // 常识匹配设置
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            listing.Label("常识匹配设置");
            GUI.color = Color.white;
            listing.Gap();
            
            listing.CheckboxLabeled("启用常识链", ref enableKnowledgeChaining);
            if (enableKnowledgeChaining)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  允许常识触发常识，进行多轮匹配");
                GUI.color = Color.white;
                listing.Gap();
                
                listing.Label($"最大轮数: {maxChainingRounds}");
                maxChainingRounds = (int)listing.Slider(maxChainingRounds, 1, 5);
            }
            
            listing.Gap();
            
        }

        private void OpenCommonKnowledgeDialog()
        {
            if (Current.Game == null)
            {
                Messages.Message("请先进入游戏", MessageTypeDefOf.RejectInput);
                return;
            }

            var memoryManager = Find.World.GetComponent<MemoryManager>();
            if (memoryManager == null)
            {
                Messages.Message("无法找到内存管理器", MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(new Dialog_CommonKnowledge(memoryManager.CommonKnowledge));
        }
        
        /// <summary>
        /// ? 绘制提示词规范化设置 UI
        /// </summary>
        private void DrawPromptNormalizationSettings(Listing_Standard listing)
        {
            // 背景框
            Rect sectionRect = listing.GetRect(300f); // 预估高度
            Widgets.DrawBoxSolid(sectionRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            
            Listing_Standard inner = new Listing_Standard();
            inner.Begin(sectionRect.ContractedBy(10f));
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            inner.Label("替换规则列表");
            GUI.color = Color.white;
            
            inner.Gap(5f);
            
            // 规则列表（最多显示 10 条）
            if (normalizationRules == null)
            {
                normalizationRules = new List<ReplacementRule>();
            }
            
            // 绘制每条规则
            for (int i = 0; i < normalizationRules.Count; i++)
            {
                var rule = normalizationRules[i];
                
                Rect ruleRect = inner.GetRect(30f);
                
                // 启用复选框
                Rect checkboxRect = new Rect(ruleRect.x, ruleRect.y, 24f, 24f);
                Widgets.Checkbox(checkboxRect.position, ref rule.isEnabled);
                
                // 模式输入框
                Rect patternRect = new Rect(ruleRect.x + 30f, ruleRect.y, 200f, 25f);
                rule.pattern = Widgets.TextField(patternRect, rule.pattern ?? "");
                
                // 箭头
                Rect arrowRect = new Rect(ruleRect.x + 235f, ruleRect.y, 30f, 25f);
                Widgets.Label(arrowRect, " → ");
                
                // 替换输入框
                Rect replacementRect = new Rect(ruleRect.x + 270f, ruleRect.y, 150f, 25f);
                rule.replacement = Widgets.TextField(replacementRect, rule.replacement ?? "");
                
                // 删除按钮
                Rect deleteRect = new Rect(ruleRect.x + 430f, ruleRect.y, 30f, 25f);
                GUI.color = new Color(1f, 0.3f, 0.3f);
                if (Widgets.ButtonText(deleteRect, "?"))
                {
                    normalizationRules.RemoveAt(i);
                    i--; // 调整索引
                }
                GUI.color = Color.white;
                
                inner.Gap(3f);
            }
            
            // 添加新规则按钮
            Rect addButtonRect = inner.GetRect(30f);
            if (Widgets.ButtonText(addButtonRect, "+ 添加新规则"))
            {
                normalizationRules.Add(new ReplacementRule("", "", true));
            }
            
            inner.Gap(5f);
            
            // 统计信息
            int enabledCount = normalizationRules.Count(r => r.isEnabled);
            GUI.color = Color.gray;
            inner.Label($"已启用: {enabledCount} / {normalizationRules.Count} 条规则");
            GUI.color = Color.white;
            
            // 示例提示
            inner.Gap(3f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            inner.Label("?? 示例：模式 \\(Player\\) → 替换 Master");
            inner.Label("   支持正则表达式（忽略大小写）");
            GUI.color = Color.white;
            
            inner.End();
        }
    }
}
