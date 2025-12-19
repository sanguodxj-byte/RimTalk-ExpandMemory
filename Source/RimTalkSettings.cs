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
        
        // ⭐ v3.3.20: SiliconFlow向量嵌入服务配置
        public string siliconFlowApiKey = "";
        public string siliconFlowModel = "BAAI/bge-large-zh-v1.5";
        public bool enableMemoryVectorSearch = false;      // 记忆向量检索
        public bool enableKnowledgeVectorSearch = false;   // 常识向量检索

        // Knowledge Matching Settings
        public bool enableKnowledgeChaining = true;
        public int maxChainingRounds = 2;
        
        // ⭐ v3.3.20: 新增高级匹配设置
        public float confidenceMargin = 0.05f; // 防误触领跑分 (0.0 - 0.2)
        public float hybridWeightBalance = 0.5f; // 混合检索权重 (0.0 - 1.0, 0=Keywords, 1=Vector)
        public string globalExcludeKeywords = ""; // 全局排除词 (逗号分隔)
        
        private string[] cachedGlobalExcludeKeywords; // 缓存的全局排除词数组

        public string[] GetGlobalExcludeKeywords()
        {
            if (cachedGlobalExcludeKeywords == null)
            {
                if (string.IsNullOrEmpty(globalExcludeKeywords))
                {
                    cachedGlobalExcludeKeywords = new string[0];
                }
                else
                {
                    cachedGlobalExcludeKeywords = globalExcludeKeywords.Split(new[] { ',', '，', '、', ';', '；' }, System.StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToArray();
                }
            }
            return cachedGlobalExcludeKeywords;
        }
        
        public void ClearGlobalExcludeCache()
        {
            cachedGlobalExcludeKeywords = null;
        }

        // UI折叠状态
        private static bool expandDynamicInjection = true;
        private static bool expandMemoryCapacity = false;
        private static bool expandDecayRates = false;
        private static bool expandSummarization = false;
        private static bool expandAIConfig = true;
        private static bool expandMemoryTypes = false;
        private static bool expandExperimentalFeatures = true;
        private static bool expandVectorEnhancement = true;
        private static bool expandPromptNormalization = true;
        
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
            
            // ⭐ v3.3.20: SiliconFlow向量嵌入服务
            Scribe_Values.Look(ref siliconFlowApiKey, "siliconFlow_apiKey", "");
            Scribe_Values.Look(ref siliconFlowModel, "siliconFlow_model", "BAAI/bge-large-zh-v1.5");
            Scribe_Values.Look(ref enableMemoryVectorSearch, "siliconFlow_enableMemoryVector", false);
            Scribe_Values.Look(ref enableKnowledgeVectorSearch, "siliconFlow_enableKnowledgeVector", false);

            // Knowledge Matching
            Scribe_Values.Look(ref enableKnowledgeChaining, "knowledge_enableKnowledgeChaining", true);
            Scribe_Values.Look(ref maxChainingRounds, "knowledge_maxChainingRounds", 2);
            
            // ⭐ v3.3.20: 序列化高级匹配设置
            Scribe_Values.Look(ref confidenceMargin, "knowledge_confidenceMargin", 0.05f);
            Scribe_Values.Look(ref hybridWeightBalance, "knowledge_hybridWeightBalance", 0.5f);
            Scribe_Values.Look(ref globalExcludeKeywords, "knowledge_globalExcludeKeywords", "");
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ClearGlobalExcludeCache();
            }
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 1400f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listingStandard.Begin(viewRect);

            DrawPresetConfiguration(listingStandard);
            listingStandard.Gap();
            DrawQuickActionButtons(listingStandard);
            listingStandard.GapLine();

            Text.Font = GameFont.Medium;
            listingStandard.Label("API 配置");
            Text.Font = GameFont.Small;
            DrawAIConfigSettings(listingStandard);

            listingStandard.GapLine();
            Rect advancedButtonRect = listingStandard.GetRect(40f);
            if (Widgets.ButtonText(advancedButtonRect, "高级设置..."))
            {
                Find.WindowStack.Add(new AdvancedSettingsWindow(this));
            }

            listingStandard.End();
            Widgets.EndScrollView();
        }

        private void DrawPresetConfiguration(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("预设配置");
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            listing.Label("从记忆/常识注入数量少到多快速切换，并预估 token 消耗");
            GUI.color = Color.white;
            listing.Gap(6f);

            Rect rowRect = listing.GetRect(95f);
            float spacing = 10f;
            float cardWidth = (rowRect.width - spacing * 2f) / 3f;
            float cardHeight = rowRect.height;

            DrawPresetCard(new Rect(rowRect.x, rowRect.y, cardWidth, cardHeight), "轻量", 3, 2, 250);
            DrawPresetCard(new Rect(rowRect.x + cardWidth + spacing, rowRect.y, cardWidth, cardHeight), "平衡", 6, 4, 520);
            DrawPresetCard(new Rect(rowRect.x + 2f * (cardWidth + spacing), rowRect.y, cardWidth, cardHeight), "强化", 10, 6, 850);

            listing.GapLine();
        }

        private void DrawPresetCard(Rect rect, string title, int memoryCount, int knowledgeCount, int tokenEstimate)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.18f, 0.6f));
            Widgets.DrawHighlightIfMouseover(rect);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(rect, $"{title}\n记忆 {memoryCount} | 常识 {knowledgeCount}\n~{tokenEstimate} tokens");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect, $"记忆 {memoryCount} 条 + 常识 {knowledgeCount} 条\n预计消耗 ~{tokenEstimate} tokens");

            if (Widgets.ButtonInvisible(rect))
            {
                useDynamicInjection = true;
                maxInjectedMemories = memoryCount;
                maxInjectedKnowledge = knowledgeCount;
                Messages.Message($"已应用预设: {title} (记忆 {memoryCount}, 常识 {knowledgeCount})", MessageTypeDefOf.PositiveEvent);
            }
        }

        private void DrawQuickActionButtons(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("功能入口");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            Rect rowRect = listing.GetRect(60f);
            float spacing = 10f;
            float buttonWidth = (rowRect.width - spacing * 2f) / 3f;
            float buttonHeight = rowRect.height;

            DrawActionButton(new Rect(rowRect.x, rowRect.y, buttonWidth, buttonHeight), "常识库", "打开并管理全局常识库", delegate
            {
                OpenCommonKnowledgeDialog();
            });

            DrawActionButton(new Rect(rowRect.x + buttonWidth + spacing, rowRect.y, buttonWidth, buttonHeight), "提示词替换", "编辑提示词替换/规范化规则", delegate
            {
                Find.WindowStack.Add(new PromptNormalizationWindow(this));
            });

            DrawActionButton(new Rect(rowRect.x + 2f * (buttonWidth + spacing), rowRect.y, buttonWidth, buttonHeight), "注入预览器", "实时查看记忆/常识注入效果", delegate
            {
                Find.WindowStack.Add(new Memory.Debug.Dialog_InjectionPreview());
            });
        }

        private void DrawActionButton(Rect rect, string label, string tip, System.Action onClick)
        {
            if (Widgets.ButtonText(rect, label))
            {
                onClick?.Invoke();
            }
            TooltipHandler.TipRegion(rect, tip);
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
            
            // ⭐ v3.3.20: 使用辅助类绘制提供商选择
            SettingsUIDrawers.DrawAIProviderSelection(listing, this);
            
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
                    listing.Label("  ✓ OpenAI 支持 Prompt Caching (Beta)");
                    listing.Label("  适用模型: gpt-4o, gpt-4-turbo");
                }
                else if (independentProvider == "DeepSeek")
                {
                    listing.Label("  ✓ DeepSeek 支持 Prompt Caching");
                    listing.Label("  可节省约 50% 费用");
                }
                else if (independentProvider == "Player2")
                {
                    listing.Label("  ✓ Player2 支持 Prompt Caching");
                    listing.Label("  本地客户端自动缓存");
                }
                else if (independentProvider == "Google")
                {
                    listing.Label("  ✗ Google Gemini 暂不支持 Prompt Caching");
                }
                else if (independentProvider == "Custom")
                {
                    listing.Label("  ✦ 取决于您的自定义 API 实现");
                }
                GUI.color = Color.white;
            }
            
            listing.Gap();
            
            // 配置验证按钮
            Rect validateButtonRect = listing.GetRect(35f);
            if (Widgets.ButtonText(validateButtonRect, "✓ 验证配置"))
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
            // ⭐ v3.3.20: 使用辅助类绘制SiliconFlow设置
            SettingsUIDrawers.DrawSiliconFlowSettings(listing, this);
            
            listing.GapLine();
            
            // 旧的向量增强设置（保留兼容性）
            listing.CheckboxLabeled("启用向量增强 (需要重启)", ref enableVectorEnhancement);
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
            }
            
            listing.Gap();
            listing.GapLine();
            
            // ⭐ v3.3.20: 使用辅助类绘制高级匹配设置
            SettingsUIDrawers.DrawAdvancedMatchingSettings(listing, this);
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
        /// ✦ 绘制提示词规范化设置 UI
        /// </summary>
        private void DrawPromptNormalizationSettings(Listing_Standard listing)
        {
            // ⭐ v3.3.20: 使用辅助类绘制
            SettingsUIDrawers.DrawPromptNormalizationSettings(listing, this);
        }

        private class AdvancedSettingsWindow : Window
        {
            private readonly RimTalkMemoryPatchSettings settings;
            private Vector2 scrollPos;

            public override Vector2 InitialSize => new Vector2(900f, 760f);

            public AdvancedSettingsWindow(RimTalkMemoryPatchSettings settings)
            {
                this.settings = settings;
                doCloseX = true;
                doCloseButton = true;
                absorbInputAroundWindow = true;
                closeOnClickedOutside = false;
            }

            public override void DoWindowContents(Rect inRect)
            {
                Listing_Standard listing = new Listing_Standard();
                Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 2400f);
                Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
                listing.Begin(viewRect);

                Text.Font = GameFont.Medium;
                listing.Label("高级设置");
                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                listing.Label("包含注入、记忆容量、衰减等详细配置");
                GUI.color = Color.white;
                listing.GapLine();

                settings.DrawCollapsibleSection(listing, "提示词规范化", ref expandPromptNormalization, delegate { settings.DrawPromptNormalizationSettings(listing); });
                settings.DrawCollapsibleSection(listing, "动态注入设置", ref expandDynamicInjection, delegate { settings.DrawDynamicInjectionSettings(listing); });
                settings.DrawCollapsibleSection(listing, "?? 向量增强设置", ref expandVectorEnhancement, delegate { settings.DrawVectorEnhancementSettings(listing); });
                settings.DrawCollapsibleSection(listing, "记忆容量配置", ref expandMemoryCapacity, delegate { settings.DrawMemoryCapacitySettings(listing); });
                settings.DrawCollapsibleSection(listing, "记忆衰减配置", ref expandDecayRates, delegate { settings.DrawDecaySettings(listing); });
                settings.DrawCollapsibleSection(listing, "记忆总结设置", ref expandSummarization, delegate { settings.DrawSummarizationSettings(listing); });

                if (settings.useAISummarization)
                {
                    settings.DrawCollapsibleSection(listing, "AI 配置", ref expandAIConfig, delegate { settings.DrawAIConfigSettings(listing); });
                }

                settings.DrawCollapsibleSection(listing, "记忆类型开关", ref expandMemoryTypes, delegate { settings.DrawMemoryTypesSettings(listing); });
                settings.DrawCollapsibleSection(listing, "?? 实验性功能", ref expandExperimentalFeatures, delegate { settings.DrawExperimentalFeaturesSettings(listing); });

                listing.End();
                Widgets.EndScrollView();

                PromptNormalizer.UpdateRules(settings.normalizationRules);
            }
        }

        private class PromptNormalizationWindow : Window
        {
            private readonly RimTalkMemoryPatchSettings settings;
            private Vector2 scrollPos;

            public override Vector2 InitialSize => new Vector2(750f, 520f);

            public PromptNormalizationWindow(RimTalkMemoryPatchSettings settings)
            {
                this.settings = settings;
                doCloseX = true;
                doCloseButton = true;
                absorbInputAroundWindow = true;
                closeOnClickedOutside = false;
            }

            public override void DoWindowContents(Rect inRect)
            {
                Listing_Standard listing = new Listing_Standard();
                Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 420f);
                Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
                listing.Begin(viewRect);

                Text.Font = GameFont.Medium;
                listing.Label("提示词替换");
                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                listing.Label("在发送给 AI 前自动规范化提示词");
                GUI.color = Color.white;
                listing.Gap(6f);

                settings.DrawPromptNormalizationSettings(listing);

                listing.End();
                Widgets.EndScrollView();

                PromptNormalizer.UpdateRules(settings.normalizationRules);
            }
        }
    }
}
