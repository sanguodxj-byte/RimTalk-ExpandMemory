using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.Memory;
using RimTalk.Memory.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RimTalk.MemoryPatch
{
    public class RimTalkMemoryPatchSettings : ModSettings
    {
        // ⭐ 提示词规范化规则
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
        
        // ⭐ 提示词规范化规则列表（功能保留，默认为空）
        public List<ReplacementRule> normalizationRules = new List<ReplacementRule>();
        
        // ⭐ v4.0: 三层记忆容量配置（移除 SCM）
        // maxActiveMemories 已废弃，ABM 无容量限制
        // maxSituationalMemories 仅用于兼容旧存档显示
        public int maxSituationalMemories = 20;
        public int maxEventLogMemories = 50;
        
        // ⭐ v4.0: ABM 注入轮数配置
        public int maxABMInjectionRounds = 3;  // 默认注入最近3轮对话
        
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
        public int maxArchiveMemories = 50;

        // AI 配置
        public bool useRimTalkAIConfig = true;
        public string independentApiKey = "";
        public string independentApiUrl = "";
        public string independentModel = "gpt-3.5-turbo";
        public string independentProvider = "OpenAI";
        public bool enablePromptCaching = true;
        
        // AI 总结提示词配置
        public string dailySummaryPrompt = "";  // 空字符串表示使用默认
        public string deepArchivePrompt = "";   // 空字符串表示使用默认
        public int summaryMaxTokens = 8000;  // ⭐ v3.4.0: 调整默认值为 8000

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
        
        // Vector Enhancement Settings
        public bool enableVectorEnhancement = false;
        public float vectorSimilarityThreshold = 0.75f;
        public int maxVectorResults = 5;
        
        // Cloud Embedding Settings
        public string embeddingApiKey = "";
        public string embeddingApiUrl = "https://api.siliconflow.cn/v1/embeddings";
        public string embeddingModel = "BAAI/bge-m3";
        
        // Knowledge Matching Settings
        public bool enableKnowledgeChaining = false; // ⭐ 默认改为false
        public int maxChainingRounds = 2;
        
        // v4.1: 知识匹配源选择（用于选择哪些 Mustache 变量用于匹配）
        // 默认勾选 Pawn 的核心属性：fullname、role、age、gender、backstory、traits、skills、relations
        public List<string> knowledgeMatchingSources = new List<string> { "prompt", "fullname", "role", "age", "gender", "backstory", "traits", "skills", "relations" };

        // UI折叠状态
        private static bool expandDynamicInjection = true;
        private static bool expandMemoryCapacity = false;
        private static bool expandDecayRates = false;
        private static bool expandSummarization = false;
        private static bool expandAIConfig = true;
        private static bool expandMemoryTypes = false;
        private static bool expandVectorEnhancement = true; // ⭐ 恢复向量增强折叠状态
        private static bool expandExperimentalFeatures = true;
        
        private static Vector2 scrollPosition = Vector2.zero;

        public override void ExposeData()
        {
            base.ExposeData();
            
            // ⭐ 序列化提示词规范化规则
            Scribe_Collections.Look(ref normalizationRules, "normalizationRules", LookMode.Deep);
            
            // ⭐ 兼容性：如果加载后为 null，初始化为空列表
            if (Scribe.mode == LoadSaveMode.PostLoadInit && normalizationRules == null)
            {
                normalizationRules = new List<ReplacementRule>();
            }
            
            // ⭐ v4.0: maxActiveMemories 已废弃，保留序列化key以兼容旧存档（读取后忽略）
            int _legacyMaxActive = 6;
            Scribe_Values.Look(ref _legacyMaxActive, "fourLayer_maxActiveMemories", 6);
            
            Scribe_Values.Look(ref maxSituationalMemories, "fourLayer_maxSituationalMemories", 20);
            Scribe_Values.Look(ref maxEventLogMemories, "fourLayer_maxEventLogMemories", 50);
            
            // ⭐ v4.0: ABM 注入轮数
            Scribe_Values.Look(ref maxABMInjectionRounds, "fourLayer_maxABMInjectionRounds", 3);
            
            Scribe_Values.Look(ref scmDecayRate, "fourLayer_scmDecayRate", 0.01f);
            Scribe_Values.Look(ref elsDecayRate, "fourLayer_elsDecayRate", 0.005f);
            Scribe_Values.Look(ref clpaDecayRate, "fourLayer_clpaDecayRate", 0.001f);
            
            Scribe_Values.Look(ref enableDailySummarization, "fourLayer_enableDailySummarization", true);
            Scribe_Values.Look(ref summarizationHour, "fourLayer_summarizationHour", 0);
            Scribe_Values.Look(ref useAISummarization, "fourLayer_useAISummarization", true);
            Scribe_Values.Look(ref maxSummaryLength, "fourLayer_maxSummaryLength", 80);
            
            Scribe_Values.Look(ref enableAutoArchive, "fourLayer_enableAutoArchive", true);
            Scribe_Values.Look(ref archiveIntervalDays, "fourLayer_archiveIntervalDays", 7);
            Scribe_Values.Look(ref maxArchiveMemories, "fourLayer_maxArchiveMemories", 50);

            Scribe_Values.Look(ref useRimTalkAIConfig, "ai_useRimTalkConfig", true);
            Scribe_Values.Look(ref independentApiKey, "ai_independentApiKey", "");
            Scribe_Values.Look(ref independentApiUrl, "ai_independentApiUrl", "");
            Scribe_Values.Look(ref independentModel, "ai_independentModel", "gpt-3.5-turbo");
            Scribe_Values.Look(ref independentProvider, "ai_independentProvider", "OpenAI");
            Scribe_Values.Look(ref enablePromptCaching, "ai_enablePromptCaching", true);
            
            Scribe_Values.Look(ref dailySummaryPrompt, "ai_dailySummaryPrompt", "");
            Scribe_Values.Look(ref deepArchivePrompt, "ai_deepArchivePrompt", "");
            Scribe_Values.Look(ref summaryMaxTokens, "ai_summaryMaxTokens", 8000);  // ⭐ v3.4.0: 与字段默认值同步

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

            // Vector Enhancement
            Scribe_Values.Look(ref enableVectorEnhancement, "vector_enableVectorEnhancement", false);
            Scribe_Values.Look(ref vectorSimilarityThreshold, "vector_vectorSimilarityThreshold", 0.75f);
            Scribe_Values.Look(ref maxVectorResults, "vector_maxVectorResults", 5);
            
            Scribe_Values.Look(ref embeddingApiKey, "vector_embeddingApiKey", "");
            Scribe_Values.Look(ref embeddingApiUrl, "vector_embeddingApiUrl", "https://api.siliconflow.cn/v1/embeddings");
            Scribe_Values.Look(ref embeddingModel, "vector_embeddingModel", "BAAI/bge-m3");

            // Knowledge Matching
            Scribe_Values.Look(ref enableKnowledgeChaining, "knowledge_enableKnowledgeChaining", false); // ⭐ 默认改为false
            Scribe_Values.Look(ref maxChainingRounds, "knowledge_maxChainingRounds", 2);
            
            // ⭐ v4.0: 知识匹配源
            Scribe_Collections.Look(ref knowledgeMatchingSources, "knowledgeMatchingSources", LookMode.Value);
            
            // 兼容性：如果加载后为 null 或空，初始化为默认值
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (knowledgeMatchingSources == null || knowledgeMatchingSources.Count == 0))
            {
                knowledgeMatchingSources = new List<string> { "prompt", "fullname", "role", "age", "gender", "backstory", "traits", "skills", "relations" };
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
            listingStandard.Label("RimTalk_Settings_APIConfigTitle".Translate());
            Text.Font = GameFont.Small;
            DrawAIConfigSettings(listingStandard);

            listingStandard.GapLine();
            Rect advancedButtonRect = listingStandard.GetRect(40f);
            if (Widgets.ButtonText(advancedButtonRect, "RimTalk_Settings_AdvancedSettings".Translate()))
            {
                Find.WindowStack.Add(new AdvancedSettingsWindow(this));
            }

            listingStandard.End();
            Widgets.EndScrollView();
        }

        private void DrawPresetConfiguration(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk_Settings_PresetConfig".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            listing.Label("RimTalk_Settings_PresetConfigDesc".Translate());
            GUI.color = Color.white;
            listing.Gap(6f);

            Rect rowRect = listing.GetRect(95f);
            float spacing = 10f;
            float cardWidth = (rowRect.width - spacing * 2f) / 3f;
            float cardHeight = rowRect.height;

            DrawPresetCard(new Rect(rowRect.x, rowRect.y, cardWidth, cardHeight), "RimTalk_Settings_PresetLight".Translate(), 3, 2, 250);
            DrawPresetCard(new Rect(rowRect.x + cardWidth + spacing, rowRect.y, cardWidth, cardHeight), "RimTalk_Settings_PresetBalanced".Translate(), 6, 4, 520);
            DrawPresetCard(new Rect(rowRect.x + 2f * (cardWidth + spacing), rowRect.y, cardWidth, cardHeight), "RimTalk_Settings_PresetEnhanced".Translate(), 10, 6, 850);

            listing.GapLine();
        }

        private void DrawPresetCard(Rect rect, string title, int memoryCount, int knowledgeCount, int tokenEstimate)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.18f, 0.6f));
            Widgets.DrawHighlightIfMouseover(rect);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(rect, "RimTalk_Settings_PresetCardContent".Translate(title, memoryCount, knowledgeCount, tokenEstimate));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect, "RimTalk_Settings_PresetCardTooltip".Translate(memoryCount, knowledgeCount, tokenEstimate));

            if (Widgets.ButtonInvisible(rect))
            {
                useDynamicInjection = true;
                maxInjectedMemories = memoryCount;
                maxInjectedKnowledge = knowledgeCount;
                Messages.Message("RimTalk_Settings_PresetApplied".Translate(title, memoryCount, knowledgeCount), MessageTypeDefOf.PositiveEvent);
            }
        }

        private void DrawQuickActionButtons(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("RimTalk_Settings_FeatureEntries".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            Rect rowRect = listing.GetRect(60f);
            float spacing = 10f;
            float buttonWidth = (rowRect.width - spacing * 2f) / 3f; // ⭐ 改回3个按钮
            float buttonHeight = rowRect.height;

            DrawActionButton(new Rect(rowRect.x, rowRect.y, buttonWidth, buttonHeight), "RimTalk_Settings_KnowledgeLibrary".Translate(), "RimTalk_Settings_KnowledgeLibraryTip".Translate(), delegate
            {
                OpenCommonKnowledgeDialog();
            });

            // ⭐ 恢复"提示词替换"按钮
            DrawActionButton(new Rect(rowRect.x + buttonWidth + spacing, rowRect.y, buttonWidth, buttonHeight), "RimTalk_Settings_PromptReplacement".Translate(), "RimTalk_Settings_PromptReplacementTip".Translate(), delegate
            {
                Find.WindowStack.Add(new PromptNormalizationWindow(this));
            });

            DrawActionButton(new Rect(rowRect.x + 2f * (buttonWidth + spacing), rowRect.y, buttonWidth, buttonHeight), "RimTalk_Settings_InjectionPreviewer".Translate(), "RimTalk_Settings_InjectionPreviewerTip".Translate(), delegate
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
            listing.CheckboxLabeled("RimTalk_Settings_EnableDynamicInjection".Translate(), ref useDynamicInjection);
            
            if (useDynamicInjection)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  " + "RimTalk_Settings_DynamicInjectionDesc".Translate());
                GUI.color = Color.white;
                
                listing.Gap();
                
                listing.Label("RimTalk_Settings_MaxInjectedMemoriesLabel".Translate(maxInjectedMemories));
                maxInjectedMemories = (int)listing.Slider(maxInjectedMemories, 1, 20);
                
                listing.Label("RimTalk_Settings_MaxInjectedKnowledgeLabel".Translate(maxInjectedKnowledge));
                maxInjectedKnowledge = (int)listing.Slider(maxInjectedKnowledge, 1, 10);
                
                listing.Gap();
                
                listing.Label("RimTalk_Settings_MemoryScoreThresholdLabel".Translate(memoryScoreThreshold.ToString("P0")));
                memoryScoreThreshold = listing.Slider(memoryScoreThreshold, 0f, 1f);
                
                listing.Label("RimTalk_Settings_KnowledgeScoreThresholdLabel".Translate(knowledgeScoreThreshold.ToString("P0")));
                knowledgeScoreThreshold = listing.Slider(knowledgeScoreThreshold, 0f, 1f);
            }
        }

        private void DrawMemoryCapacitySettings(Listing_Standard listing)
        {
            listing.Label("RimTalk_Settings_SCMCapacityLabel".Translate(maxSituationalMemories));
            maxSituationalMemories = (int)listing.Slider(maxSituationalMemories, 10, 50);
            
            listing.Label("RimTalk_Settings_ELSCapacityLabel".Translate(maxEventLogMemories));
            maxEventLogMemories = (int)listing.Slider(maxEventLogMemories, 20, 100);
        }

        private void DrawDecaySettings(Listing_Standard listing)
        {
            listing.Label("RimTalk_Settings_SCMDecayLabel".Translate(scmDecayRate.ToString("P1")));
            scmDecayRate = listing.Slider(scmDecayRate, 0.001f, 0.05f);
            
            listing.Label("RimTalk_Settings_ELSDecayLabel".Translate(elsDecayRate.ToString("P1")));
            elsDecayRate = listing.Slider(elsDecayRate, 0.0005f, 0.02f);
            
            listing.Label("RimTalk_Settings_CLPADecayLabel".Translate(clpaDecayRate.ToString("P1")));
            clpaDecayRate = listing.Slider(clpaDecayRate, 0.0001f, 0.01f);
        }

        private void DrawSummarizationSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimTalk_Settings_EnableDailySummarization".Translate(), ref enableDailySummarization);
            
            if (enableDailySummarization)
            {
                listing.Label("RimTalk_Settings_TriggerTimeLabel".Translate(summarizationHour));
                summarizationHour = (int)listing.Slider(summarizationHour, 0, 23);
            }
            
            listing.CheckboxLabeled("RimTalk_Settings_EnableAutoArchive".Translate(), ref enableAutoArchive);
        }

        private void DrawAIConfigSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimTalk_Settings_PreferRimTalkAI".Translate(), ref useRimTalkAIConfig);
            
            if (useRimTalkAIConfig)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  " + "RimTalk_Settings_WillFollowRimTalkConfig".Translate());
                GUI.color = Color.white;
                listing.Gap();
            }
            
            listing.Gap();
            
            // ⭐ v3.3.20: 使用辅助类绘制提供商选择
            SettingsUIDrawers.DrawAIProviderSelection(listing, this);
            
            listing.Gap();
            
            // API 配置
            listing.Label("RimTalk_Settings_APIKey".Translate() + ":");
            independentApiKey = listing.TextEntry(independentApiKey);
            
            listing.Label("RimTalk_Settings_APIURL".Translate() + ":");
            independentApiUrl = listing.TextEntry(independentApiUrl);
            
            listing.Label("RimTalk_Settings_ModelName".Translate() + ":");
            independentModel = listing.TextEntry(independentModel);
            
            listing.Gap();
            
            // ⭐ 修改：Prompt Caching 选项 - 仅DeepSeek和OpenAI可切换
            bool canToggleCaching = (independentProvider == "OpenAI" || independentProvider == "DeepSeek");
            
            if (canToggleCaching)
            {
                listing.CheckboxLabeled("RimTalk_Settings_EnablePromptCaching".Translate(), ref enablePromptCaching);
            }
            else
            {
                // 其他提供商强制关闭缓存
                enablePromptCaching = false;
                GUI.color = Color.gray;
                bool disabledCache = false;
                listing.CheckboxLabeled("RimTalk_Settings_EnablePromptCachingUnavailable".Translate(), ref disabledCache);
                GUI.color = Color.white;
            }
            
            if (enablePromptCaching || !canToggleCaching)
            {
                if (independentProvider == "OpenAI")
                {
                    GUI.color = new Color(0.8f, 1f, 0.8f);
                    listing.Label("  " + "RimTalk_Settings_OpenAICachingSupport".Translate());
                    listing.Label("  " + "RimTalk_Settings_OpenAICachingModels".Translate());
                    GUI.color = Color.white;
                }
                else if (independentProvider == "DeepSeek")
                {
                    GUI.color = new Color(0.8f, 1f, 0.8f);
                    listing.Label("  " + "RimTalk_Settings_DeepSeekCachingSupport".Translate());
                    listing.Label("  " + "RimTalk_Settings_DeepSeekCachingSavings".Translate());
                    GUI.color = Color.white;
                }
                else if (independentProvider == "Player2")
                {
                    GUI.color = Color.gray;
                    listing.Label("  " + "RimTalk_Settings_Player2NoCaching".Translate());
                    listing.Label("  " + "RimTalk_Settings_Player2LocalNoCache".Translate());
                    GUI.color = Color.white;
                }
                else if (independentProvider == "Google")
                {
                    GUI.color = Color.gray;
                    listing.Label("  " + "RimTalk_Settings_GoogleNoCaching".Translate());
                    GUI.color = Color.white;
                }
                else if (independentProvider == "Custom")
                {
                    GUI.color = Color.gray;
                    listing.Label("  " + "RimTalk_Settings_CustomNoCaching".Translate());
                    listing.Label("  " + "RimTalk_Settings_CustomCachingDepends".Translate());
                    GUI.color = Color.white;
                }
            }
            
            listing.Gap();
            
            // 配置验证按钮
            Rect validateButtonRect = listing.GetRect(35f);
            if (Widgets.ButtonText(validateButtonRect, "RimTalk_Settings_ValidateConfig".Translate()))
            {
                ValidateAIConfig();
            }
            
            // 提示信息
            GUI.color = Color.gray;
            listing.Label("RimTalk_Settings_ValidateConfigTip".Translate());
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 验证 AI 配置
        /// </summary>
        private void ValidateAIConfig()
        {
            if (useRimTalkAIConfig)
            {
                Messages.Message("RimTalk_Settings_UsingRimTalkConfigNoValidation".Translate(), MessageTypeDefOf.NeutralEvent);
                return;
            }
            
            if (string.IsNullOrEmpty(independentApiKey))
            {
                Messages.Message("RimTalk_Settings_PleaseEnterAPIKey".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (string.IsNullOrEmpty(independentApiUrl))
            {
                Messages.Message("RimTalk_Settings_PleaseEnterAPIURL".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (string.IsNullOrEmpty(independentModel))
            {
                Messages.Message("RimTalk_Settings_PleaseEnterModel".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            Messages.Message("RimTalk_Settings_Validating".Translate(), MessageTypeDefOf.NeutralEvent);
            
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
                            Messages.Message("RimTalk_Settings_ValidationSuccess".Translate(independentProvider), MessageTypeDefOf.PositiveEvent);
                        });
                    }
                    else
                    {
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            Messages.Message("RimTalk_Settings_ValidationFailed".Translate(), MessageTypeDefOf.RejectInput);
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"AI Config validation failed: {ex.Message}");
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        Messages.Message("RimTalk_Settings_ValidationError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
                    });
                }
            });
        }

        private void DrawMemoryTypesSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimTalk_Settings_ActionMemory".Translate(), ref enableActionMemory);
            listing.CheckboxLabeled("RimTalk_Settings_ConversationMemory".Translate(), ref enableConversationMemory);
        }

        private void DrawExperimentalFeaturesSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimTalk_Settings_EnableProactiveRecall".Translate(), ref enableProactiveRecall);
            
            if (enableProactiveRecall)
            {
                listing.Label("RimTalk_Settings_TriggerChanceLabel".Translate(recallTriggerChance.ToString("P0")));
                recallTriggerChance = listing.Slider(recallTriggerChance, 0.05f, 0.60f);
            }
            
            listing.Gap();
            listing.GapLine();
            
            // ⭐ v4.0: 知识匹配源选择（动态从 RimTalk 获取 Mustache 变量）
            SettingsUIDrawers.DrawKnowledgeMatchingSourcesSettings(listing, this);
            
            // ⭐ 常识链设置
            SettingsUIDrawers.DrawKnowledgeChainingSettings(listing, this);
        }
        
        private void DrawVectorEnhancementSettings(Listing_Standard listing)
        {
            // ⭐ SiliconFlow向量服务设置
            SettingsUIDrawers.DrawSiliconFlowSettings(listing, this);
        }

        private void OpenCommonKnowledgeDialog()
        {
            if (Current.Game == null)
            {
                Messages.Message("RimTalk_Settings_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var memoryManager = Find.World.GetComponent<MemoryManager>();
            if (memoryManager == null)
            {
                Messages.Message("RimTalk_Settings_CannotFindManager".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(new Dialog_CommonKnowledge(memoryManager.CommonKnowledge));
        }
        
        /// <summary>
        /// ✦ 绘制提示词规范化设置 UI
        /// </summary>
        private void DrawPromptNormalizationSettings(Listing_Standard listing)
        {
            // ⭐ 使用辅助类绘制
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
                Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 2200f); // ⭐ 增加高度
                Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
                listing.Begin(viewRect);

                Text.Font = GameFont.Medium;
                listing.Label("RimTalk_Settings_AdvancedSettingsTitle".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                listing.Label("RimTalk_Settings_AdvancedSettingsDesc".Translate());
                GUI.color = Color.white;
                listing.GapLine();

                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_DynamicInjectionSection".Translate(), ref expandDynamicInjection, delegate { settings.DrawDynamicInjectionSettings(listing); });
                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_MemoryCapacitySection".Translate(), ref expandMemoryCapacity, delegate { settings.DrawMemoryCapacitySettings(listing); });
                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_MemoryDecaySection".Translate(), ref expandDecayRates, delegate { settings.DrawDecaySettings(listing); });
                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_SummarizationSection".Translate(), ref expandSummarization, delegate { settings.DrawSummarizationSettings(listing); });

                if (settings.useAISummarization)
                {
                    settings.DrawCollapsibleSection(listing, "RimTalk_Settings_AIConfigSection".Translate(), ref expandAIConfig, delegate { settings.DrawAIConfigSettings(listing); });
                }

                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_MemoryTypesSection".Translate(), ref expandMemoryTypes, delegate { settings.DrawMemoryTypesSettings(listing); });
                
                // ⭐ 添加向量增强设置
                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_VectorEnhancementSection".Translate(), ref expandVectorEnhancement, delegate { settings.DrawVectorEnhancementSettings(listing); });
                
                settings.DrawCollapsibleSection(listing, "RimTalk_Settings_ExperimentalSection".Translate(), ref expandExperimentalFeatures, delegate { settings.DrawExperimentalFeaturesSettings(listing); });

                listing.End();
                Widgets.EndScrollView();
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
                listing.Label("RimTalk_Settings_PromptReplacementTitle".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                listing.Label("RimTalk_Settings_PromptReplacementDesc".Translate());
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
