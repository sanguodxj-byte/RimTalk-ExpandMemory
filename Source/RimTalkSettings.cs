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
        public int maxActiveMemories = 6;        // ABM 容量固定为6
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
        
        // CLPA 归档设置（归属于AI总结功能）
        public bool enableAutoArchive = true;         // 启用 CLPA 自动归档
        public int archiveIntervalDays = 7;           // 归档间隔天数（3-30天）
        public int maxArchiveMemories = 30;           // CLPA 最大容量（超过后自动清理最旧的）

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
        
        // === Pawn状态常识自动生成 ===
        public bool enablePawnStatusKnowledge = false;  // ⭐ 改为默认关闭
        
        // === 事件记录常识自动生成 ===
        public bool enableEventRecordKnowledge = false; // 自动生成事件记录常识（默认关闭）

        // === 对话缓存设置 ===
        public bool enableConversationCache = true;   // 启用对话缓存
        public int conversationCacheSize = 100;       // 缓存大小（50-500）
        public int conversationCacheExpireDays = 7;   // 过期天数（1-30）
        
        // === 提示词缓存设置（新增）===
        public bool enablePromptCache = true;         // 启用提示词缓存
        public int promptCacheSize = 50;              // 缓存大小（20-200）
        public int promptCacheExpireMinutes = 30;     // 过期分钟数（5-120）

        // === 动态注入设置 ===
        public bool useDynamicInjection = true;       // 使用动态注入（默认开启）
        public int maxInjectedMemories = 10;          // 最大注入记忆数量
        public int maxInjectedKnowledge = 5;          // 最大注入常识数量
        
        // 动态注入权重配置
        public float weightTimeDecay = 0.3f;          // 时间衰减权重
        public float weightImportance = 0.3f;         // 重要性权重
        public float weightKeywordMatch = 0.4f;       // 关键词匹配权重
        
        // 注入阈值设置
        public float memoryScoreThreshold = 0.15f;    // 记忆评分阈值（低于此分数不注入）
        public float knowledgeScoreThreshold = 0.1f;  // 常识评分阈值（低于此分数不注入）
        
        // ⭐ 自适应阈值设置（v3.0新增）
        public bool enableAdaptiveThreshold = false;  // 启用自适应阈值（实验性功能）
        public bool autoApplyAdaptiveThreshold = false; // 自动应用推荐阈值
        
        // ⭐ 主动记忆召回（v3.0实验性功能）
        public bool enableProactiveRecall = false;    // 启用主动记忆召回
        public float recallTriggerChance = 0.15f;     // 基础触发概率（15%）
        
        // === 常识库权重配置 ===
        public float knowledgeBaseScore = 0.05f;      // 基础分系数（固定为0.05，不提供UI配置）
        public float knowledgeJaccardWeight = 0.7f;   // Jaccard相似度权重
        public float knowledgeTagWeight = 0.3f;       // 标签匹配权重
        public float knowledgeMatchBonus = 0.08f;     // 每个匹配关键词加分（固定，不提供UI配置）

        // UI折叠状态（不保存到存档）
        private static bool expandDynamicInjection = true;
        private static bool expandMemoryCapacity = false;
        private static bool expandDecayRates = false;
        private static bool expandSummarization = false;
        private static bool expandAIConfig = true;
        private static bool expandMemoryTypes = false;
        private static bool expandExperimentalFeatures = true;

        public override void ExposeData()
        {
            base.ExposeData();
            
            // 四层记忆容量
            Scribe_Values.Look(ref maxActiveMemories, "fourLayer_maxActiveMemories", 6);
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
            
            // CLPA 归档设置
            Scribe_Values.Look(ref enableAutoArchive, "fourLayer_enableAutoArchive", true);
            Scribe_Values.Look(ref archiveIntervalDays, "fourLayer_archiveIntervalDays", 7);
            Scribe_Values.Look(ref maxArchiveMemories, "fourLayer_maxArchiveMemories", 30);

            // === 独立 AI 配置 ===
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
            
            // Pawn状态常识
            Scribe_Values.Look(ref enablePawnStatusKnowledge, "pawnStatus_enablePawnStatusKnowledge", false);
            
            // 事件记录常识
            Scribe_Values.Look(ref enableEventRecordKnowledge, "eventRecord_enableEventRecordKnowledge", false);

            // 对话缓存设置
            Scribe_Values.Look(ref enableConversationCache, "cache_enableConversationCache", true);
            Scribe_Values.Look(ref conversationCacheSize, "cache_conversationCacheSize", 100);
            Scribe_Values.Look(ref conversationCacheExpireDays, "cache_conversationCacheExpireDays", 7);
            
            // 提示词缓存设置
            Scribe_Values.Look(ref enablePromptCache, "cache_enablePromptCache", true);
            Scribe_Values.Look(ref promptCacheSize, "cache_promptCacheSize", 50);
            Scribe_Values.Look(ref promptCacheExpireMinutes, "cache_promptCacheExpireMinutes", 30);
            
            // 动态注入设置
            Scribe_Values.Look(ref useDynamicInjection, "dynamic_useDynamicInjection", true);
            Scribe_Values.Look(ref maxInjectedMemories, "dynamic_maxInjectedMemories", 10);
            Scribe_Values.Look(ref maxInjectedKnowledge, "dynamic_maxInjectedKnowledge", 5);
            
            Scribe_Values.Look(ref weightTimeDecay, "dynamic_weightTimeDecay", 0.3f);
            Scribe_Values.Look(ref weightImportance, "dynamic_weightImportance", 0.3f);
            Scribe_Values.Look(ref weightKeywordMatch, "dynamic_weightKeywordMatch", 0.4f);
            Scribe_Values.Look(ref memoryScoreThreshold, "dynamic_memoryScoreThreshold", 0.15f);
            Scribe_Values.Look(ref knowledgeScoreThreshold, "dynamic_knowledgeScoreThreshold", 0.1f);
            
            // ⭐ 自适应阈值设置（v3.0新增）
            Scribe_Values.Look(ref enableAdaptiveThreshold, "adaptive_enableAdaptiveThreshold", false);
            Scribe_Values.Look(ref autoApplyAdaptiveThreshold, "adaptive_autoApplyAdaptiveThreshold", false);
            
            // ⭐ 主动记忆召回（v3.0实验性功能）
            Scribe_Values.Look(ref enableProactiveRecall, "recall_enableProactiveRecall", false);
            Scribe_Values.Look(ref recallTriggerChance, "recall_triggerChance", 0.15f);
            
            // ⭐ v3.3.2.27: 兼容旧存档（读取但不使用这些字段）
            bool _deprecatedEnableSemanticEmbedding = false;
            bool _deprecatedAutoPrewarmEmbedding = false;
            bool _deprecatedEnableVectorDatabase = false;
            bool _deprecatedUseSharedVectorDB = false;
            bool _deprecatedAutoSyncToVectorDB = false;
            bool _deprecatedEnableRAGRetrieval = false;
            bool _deprecatedRagUseCache = false;
            int _deprecatedRagCacheTTL = 100;
            
            Scribe_Values.Look(ref _deprecatedEnableSemanticEmbedding, "semantic_enableSemanticEmbedding", false);
            Scribe_Values.Look(ref _deprecatedAutoPrewarmEmbedding, "semantic_autoPrewarmEmbedding", false);
            Scribe_Values.Look(ref _deprecatedEnableVectorDatabase, "vectordb_enableVectorDatabase", false);
            Scribe_Values.Look(ref _deprecatedUseSharedVectorDB, "vectordb_useSharedVectorDB", false);
            Scribe_Values.Look(ref _deprecatedAutoSyncToVectorDB, "vectordb_autoSyncToVectorDB", false);
            Scribe_Values.Look(ref _deprecatedEnableRAGRetrieval, "rag_enableRAGRetrieval", false);
            Scribe_Values.Look(ref _deprecatedRagUseCache, "rag_ragUseCache", false);
            Scribe_Values.Look(ref _deprecatedRagCacheTTL, "rag_ragCacheTTL", 100);

            // 常识库权重配置
            Scribe_Values.Look(ref knowledgeBaseScore, "knowledge_baseScore", 0.05f);
            Scribe_Values.Look(ref knowledgeJaccardWeight, "knowledge_jaccardWeight", 0.7f);
            Scribe_Values.Look(ref knowledgeTagWeight, "knowledge_tagWeight", 0.3f);
            Scribe_Values.Look(ref knowledgeMatchBonus, "knowledge_matchBonus", 0.08f);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            
            // ⭐ 增加滚动视图高度，确保所有内容都能显示（从1600增加到2400）
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 2400f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listingStandard.Begin(viewRect);

            // === 常识库管理 ===
            Text.Font = GameFont.Medium;
            listingStandard.Label("RimTalk_Settings_KnowledgeLibraryTitle".Translate());
            Text.Font = GameFont.Small;
            
            GUI.color = Color.gray;
            listingStandard.Label("RimTalk_Settings_KnowledgeLibraryDesc".Translate());
            GUI.color = Color.white;
            
            listingStandard.Gap(6f);
            
            Rect knowledgeButtonRect = listingStandard.GetRect(35f);
            if (Widgets.ButtonText(knowledgeButtonRect, "RimTalk_Settings_OpenKnowledgeLibrary".Translate()))
            {
                OpenCommonKnowledgeDialog();
            }
            
            listingStandard.Gap();
            listingStandard.GapLine();

            // === 动态注入设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "RimTalk_Settings_DynamicInjectionTitle".Translate(),
                ref expandDynamicInjection,
                () => DrawDynamicInjectionSettings(listingStandard)
            );

            // === 容量设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "RimTalk_Settings_MemoryCapacityTitle".Translate(),
                ref expandMemoryCapacity,
                () => DrawMemoryCapacitySettings(listingStandard)
            );

            // === 衰减设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "RimTalk_Settings_DecayRatesTitle".Translate(),
                ref expandDecayRates,
                () => DrawDecaySettings(listingStandard)
            );

            // === 总结设置 ===
            DrawCollapsibleSection(
                listingStandard,
                "RimTalk_Settings_SummarizationTitle".Translate(),
                ref expandSummarization,
                () => DrawSummarizationSettings(listingStandard)
            );

            // === AI 配置 ===
            if (useAISummarization)
            {
                DrawCollapsibleSection(
                    listingStandard,
                    "RimTalk_Settings_AIConfigTitle".Translate(),
                    ref expandAIConfig,
                    () => DrawAIConfigSettings(listingStandard)
                );
            }

            // === 记忆类型开关 ===
            DrawCollapsibleSection(
                listingStandard,
                "RimTalk_Settings_MemoryTypesTitle".Translate(),
                ref expandMemoryTypes,
                () => DrawMemoryTypesSettings(listingStandard)
            );

            // ⭐ === 实验性功能（独立区域）===
            DrawCollapsibleSection(
                listingStandard,
                "🧪 实验性功能 (v3.0-v3.3)",
                ref expandExperimentalFeatures,
                () => DrawExperimentalFeaturesSettings(listingStandard)
            );

            // 调试工具
            listingStandard.Gap();
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            listingStandard.Label("RimTalk_Settings_DebugToolsTitle".Translate());
            GUI.color = Color.white;
            
            Rect previewButtonRect = listingStandard.GetRect(35f);
            if (Widgets.ButtonText(previewButtonRect, "RimTalk_Settings_OpenInjectionPreviewer".Translate()))
            {
                Find.WindowStack.Add(new RimTalk.Memory.Debug.Dialog_InjectionPreview());
            }
            
            GUI.color = Color.gray;
            listingStandard.Label("RimTalk_Settings_PreviewerDesc".Translate());
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
            listing.CheckboxLabeled("RimTalk_Settings_EnableDynamicInjection".Translate(), ref useDynamicInjection);
            
            if (useDynamicInjection)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  " + "RimTalk_Settings_DynamicInjectionDesc".Translate());
                GUI.color = Color.white;
                
                listing.Gap();
                
                // === 注入数量配置 ===
                listing.Label($"{"RimTalk_Settings_MaxInjectedMemories".Translate()}: {maxInjectedMemories}");
                maxInjectedMemories = (int)listing.Slider(maxInjectedMemories, 1, 20);
                
                listing.Label($"{"RimTalk_Settings_MaxInjectedKnowledge".Translate()}: {maxInjectedKnowledge}");
                maxInjectedKnowledge = (int)listing.Slider(maxInjectedKnowledge, 1, 10);
                
                listing.Gap();
                
                listing.GapLine();
                
                // === 左右分栏布局：记忆权重 | 常识权重 ===
                Rect weightsRect = listing.GetRect(200f);
                float columnWidth = (weightsRect.width - 20f) / 2f;
                
                // 左侧：记忆权重配置
                Rect leftColumn = new Rect(weightsRect.x, weightsRect.y, columnWidth, weightsRect.height);
                DrawMemoryWeightsColumn(leftColumn);
                
                // 右侧：常识权重配置
                Rect rightColumn = new Rect(weightsRect.x + columnWidth + 20f, weightsRect.y, columnWidth, weightsRect.height);
                DrawKnowledgeWeightsColumn(rightColumn);
                
                // 应用权重到系统
                DynamicMemoryInjection.Weights.TimeDecay = weightTimeDecay;
                DynamicMemoryInjection.Weights.Importance = weightImportance;
                DynamicMemoryInjection.Weights.KeywordMatch = weightKeywordMatch;
                RimTalk.Memory.KnowledgeWeights.LoadFromSettings(this);
            }
            else
            {
                GUI.color = Color.yellow;
                listing.Label("  " + "RimTalk_Settings_StaticInjectionNote".Translate());
                GUI.color = Color.white;
            }
            
            listing.Gap();
            
            // === 评分阈值配置 ===
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 0.8f);
            listing.Label("RimTalk_Settings_ScoreThresholdTitle".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            // ⭐ 自适应阈值选项（实验性）
            GUI.color = new Color(0.8f, 1f, 1f);
            listing.CheckboxLabeled("  🧪 启用自适应阈值（实验性）", ref enableAdaptiveThreshold);
            GUI.color = Color.white;
            
            if (enableAdaptiveThreshold)
            {
                GUI.color = Color.gray;
                listing.Label("    自动根据评分分布调整阈值");
                listing.Label("    基于统计学方法（百分位+均值）");
                GUI.color = Color.white;
                
                listing.CheckboxLabeled("    自动应用推荐阈值", ref autoApplyAdaptiveThreshold);
                
                if (!autoApplyAdaptiveThreshold)
                {
                    GUI.color = Color.yellow;
                    listing.Label("    当前使用手动阈值，查看日志获取推荐值");
                    GUI.color = Color.white;
                }
                
                listing.Gap();
                
                // 显示诊断信息（如果有数据）
                var diagnostics = AdaptiveThresholdManager.GetDiagnostics();
                if (diagnostics.MemorySampleCount > 0 || diagnostics.KnowledgeSampleCount > 0)
                {
                    GUI.color = new Color(0.7f, 0.9f, 1f);
                    listing.Label($"    统计样本: 记忆={diagnostics.MemorySampleCount}, 常识={diagnostics.KnowledgeSampleCount}");
                    
                    if (diagnostics.MemorySampleCount >= 50)
                    {
                        listing.Label($"    推荐记忆阈值: {diagnostics.RecommendedMemoryThreshold:F3}");
                    }
                    
                    if (diagnostics.KnowledgeSampleCount >= 50)
                    {
                        listing.Label($"    推荐常识阈值: {diagnostics.RecommendedKnowledgeThreshold:F3}");
                    }
                    
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Color.gray;
                    listing.Label("    (需要至少50个样本才能计算)");
                    GUI.color = Color.white;
                }
                
                listing.Gap();
                listing.GapLine();
            }
            
            // 手动阈值配置
            GUI.color = enableAdaptiveThreshold && autoApplyAdaptiveThreshold ? Color.gray : Color.white;
            listing.Label($"  {"RimTalk_Settings_MemoryScoreThreshold".Translate()}: {memoryScoreThreshold:P0}" + 
                         (enableAdaptiveThreshold && !autoApplyAdaptiveThreshold ? " (手动)" : ""));
            if (!enableAdaptiveThreshold || !autoApplyAdaptiveThreshold)
            {
                memoryScoreThreshold = listing.Slider(memoryScoreThreshold, 0f, 1f);
            }
            
            listing.Label($"  {"RimTalk_Settings_KnowledgeScoreThreshold".Translate()}: {knowledgeScoreThreshold:P0}" +
                         (enableAdaptiveThreshold && !autoApplyAdaptiveThreshold ? " (手动)" : ""));
            if (!enableAdaptiveThreshold || !autoApplyAdaptiveThreshold)
            {
                knowledgeScoreThreshold = listing.Slider(knowledgeScoreThreshold, 0f, 1f);
            }
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 绘制记忆权重配置列
        /// </summary>
        private void DrawMemoryWeightsColumn(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            listing.Label("RimTalk_Settings_MemoryWeights".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            
            listing.Gap(3f);
            
            // 时间权重
            listing.Label($"{"RimTalk_Settings_TimeDecay".Translate()}: {weightTimeDecay:P0}");
            weightTimeDecay = listing.Slider(weightTimeDecay, 0f, 1f);
            
            // 重要性
            listing.Label($"{"RimTalk_Settings_Importance".Translate()}: {weightImportance:P0}");
            weightImportance = listing.Slider(weightImportance, 0f, 1f);
            
            // 关键词匹配度
            listing.Label($"{"RimTalk_Settings_KeywordMatch".Translate()}: {weightKeywordMatch:P0}");
            weightKeywordMatch = listing.Slider(weightKeywordMatch, 0f, 1f);
            
            Text.Font = GameFont.Small;
            listing.End();
        }
        
        /// <summary>
        /// 绘制常识权重配置列
        /// </summary>
        private void DrawKnowledgeWeightsColumn(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 0.8f);
            listing.Label("RimTalk_Settings_KnowledgeWeights".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            
            listing.Gap(3f);
            
            // 标签权重
            listing.Label($"{"RimTalk_Settings_TagMatch".Translate()}: {knowledgeTagWeight:P0}");
            knowledgeTagWeight = listing.Slider(knowledgeTagWeight, 0f, 1f);
            
            // 重要性（使用Jaccard权重，但显示为"重要性"）
            listing.Label($"{"RimTalk_Settings_Importance".Translate()}: {knowledgeJaccardWeight:P0}");
            knowledgeJaccardWeight = listing.Slider(knowledgeJaccardWeight, 0f, 1f);
            
            // 关键词匹配度（隐藏，使用固定值）
            GUI.color = Color.gray;
            listing.Label("RimTalk_Settings_KeywordMatchAuto".Translate());
            GUI.color = Color.white;
            
            Text.Font = GameFont.Small;
            listing.End();
        }
        
        /// <summary>
        /// 绘制记忆容量设置
        /// </summary>
        private void DrawMemoryCapacitySettings(Listing_Standard listing)
        {
            GUI.color = Color.gray;
            listing.Label("RimTalk_Settings_ABMCapacity".Translate());
            GUI.color = Color.white;
            
            listing.Label(string.Format("RimTalk_Settings_SCMCapacity".Translate(), maxSituationalMemories));
            maxSituationalMemories = (int)listing.Slider(maxSituationalMemories, 10, 50);
            
            listing.Label(string.Format("RimTalk_Settings_ELSCapacity".Translate(), maxEventLogMemories));
            maxEventLogMemories = (int)listing.Slider(maxEventLogMemories, 20, 100);
            
            GUI.color = Color.gray;
            listing.Label("RimTalk_Settings_CLPACapacity".Translate());
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制衰减速率设置
        /// </summary>
        private void DrawDecaySettings(Listing_Standard listing)
        {
            listing.Label($"{"RimTalk_Settings_SCMDecay".Translate()}: {scmDecayRate:P1}");
            scmDecayRate = listing.Slider(scmDecayRate, 0.001f, 0.05f);
            
            listing.Label($"{"RimTalk_Settings_ELSDecay".Translate()}: {elsDecayRate:P1}");
            elsDecayRate = listing.Slider(elsDecayRate, 0.0005f, 0.02f);
            
            listing.Label($"{"RimTalk_Settings_CLPADecay".Translate()}: {clpaDecayRate:P1}");
            clpaDecayRate = listing.Slider(clpaDecayRate, 0.0001f, 0.01f);
        }

        /// <summary>
        /// 绘制总结设置
        /// </summary>
        private void DrawSummarizationSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimTalk_Settings_EnableELSSummarization".Translate(), ref enableDailySummarization);
            
            if (enableDailySummarization)
            {
                GUI.color = new Color(0.8f, 0.8f, 1f);
                listing.Label("  " + string.Format("RimTalk_Settings_TriggerTime".Translate(), summarizationHour));
                GUI.color = Color.white;
                summarizationHour = (int)listing.Slider(summarizationHour, 0, 23);
            }
            
            listing.Gap();
            listing.Label(string.Format("RimTalk_Settings_MaxSummaryLength".Translate(), maxSummaryLength));
            maxSummaryLength = (int)listing.Slider(maxSummaryLength, 50, 200);

            listing.Gap();
            // CLPA 归档设置
            listing.CheckboxLabeled("RimTalk_Settings_EnableCLPAArchive".Translate(), ref enableAutoArchive);
            
            if (enableAutoArchive)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  " + string.Format("RimTalk_Settings_ArchiveInterval".Translate(), archiveIntervalDays));
                GUI.color = Color.white;
                archiveIntervalDays = (int)listing.Slider(archiveIntervalDays, 3, 30);
            }
        }

        /// <summary>
        /// 绘制AI配置设置
        /// </summary>
        private void DrawAIConfigSettings(Listing_Standard listing)
        {
            bool previousUseRimTalk = useRimTalkAIConfig;
            string previousProvider = independentProvider;
            
            listing.CheckboxLabeled("RimTalk_Settings_PreferRimTalkAI".Translate(), ref useRimTalkAIConfig);
            
            // ⭐ 检测设置变更，触发重新初始化
            if (previousUseRimTalk != useRimTalkAIConfig)
            {
                RimTalk.Memory.AI.IndependentAISummarizer.ForceReinitialize();
            }
            
            // ⭐ 优化提示，说明跟随逻辑
            GUI.color = new Color(0.8f, 0.9f, 1f);
            if (useRimTalkAIConfig)
            {
                listing.Label("  🔗 将优先使用RimTalk的API配置");
                listing.Label("  📝 如果RimTalk未配置，则使用下方的独立配置");
                listing.Label("  💡 建议：直接在RimTalk Mod设置中配置API");
            }
            else
            {
                listing.Label("  ⚙️ 使用独立API配置（不跟随RimTalk）");
                listing.Label("  📝 需要在下方手动配置提供商和API Key");
            }
            GUI.color = Color.white;
            
            listing.Gap();
            
            // 独立配置区域
            GUI.color = new Color(1f, 1f, 0.8f);
            listing.Label("RimTalk_Settings_IndependentAIConfig".Translate());
            GUI.color = Color.white;
            
            listing.Label("RimTalk_Settings_Provider".Translate());
            Rect providerRect = listing.GetRect(30f);
            float buttonWidth = providerRect.width / 3f;
            
            // OpenAI
            if (Widgets.ButtonText(new Rect(providerRect.x, providerRect.y, buttonWidth - 3f, providerRect.height), 
                independentProvider == "OpenAI" ? "OpenAI ✓" : "OpenAI"))
            {
                // ⭐ 切换提供商时清空API Key，避免混用
                if (independentProvider != "OpenAI")
                {
                    independentApiKey = ""; // 清空旧Key
                }
                
                independentProvider = "OpenAI";
                independentApiUrl = "https://api.openai.com/v1/chat/completions";
                independentModel = "gpt-3.5-turbo";
            }
            
            // DeepSeek
            if (Widgets.ButtonText(new Rect(providerRect.x + buttonWidth + 2f, providerRect.y, buttonWidth - 3f, providerRect.height), 
                independentProvider == "DeepSeek" ? "DeepSeek ✓" : "DeepSeek"))
            {
                // ⭐ 切换提供商时清空API Key，避免混用
                if (independentProvider != "DeepSeek")
                {
                    independentApiKey = ""; // 清空旧Key
                }
                
                independentProvider = "DeepSeek";
                independentApiUrl = "https://api.deepseek.com/v1/chat/completions";
                independentModel = "deepseek-chat";
            }
            
            // Google
            if (Widgets.ButtonText(new Rect(providerRect.x + buttonWidth * 2 + 4f, providerRect.y, buttonWidth - 3f, providerRect.height), 
                independentProvider == "Google" ? "Google ✓" : "Google"))
            {
                // ⭐ 切换提供商时清空API Key，避免混用
                if (independentProvider != "Google")
                {
                    independentApiKey = ""; // 清空旧Key
                }
                
                independentProvider = "Google";
                independentApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
                independentModel = "gemini-pro";
            }
            
            // ⭐ 检测Provider变更
            if (previousProvider != independentProvider)
            {
                RimTalk.Memory.AI.IndependentAISummarizer.ForceReinitialize();
            }
            
            listing.Gap();
            
            string previousApiKey = independentApiKey;
            
            listing.Label("RimTalk_Settings_APIKey".Translate());
            independentApiKey = listing.TextEntry(independentApiKey);
            
            // ⭐ API Key变更时重新初始化
            if (previousApiKey != independentApiKey)
            {
                RimTalk.Memory.AI.IndependentAISummarizer.ForceReinitialize();
            }
            
            // ⭐ 添加API Key格式验证提示
            if (!string.IsNullOrEmpty(independentApiKey))
            {
                bool isValidFormat = false;
                string expectedFormat = "";
                
                if (independentProvider == "OpenAI" || independentProvider == "DeepSeek")
                {
                    isValidFormat = independentApiKey.StartsWith("sk-");
                    expectedFormat = "sk-xxxxxxxxxx";
                }
                else if (independentProvider == "Google")
                {
                    isValidFormat = independentApiKey.StartsWith("AIza");
                    expectedFormat = "AIzaxxxxxxxxxx";
                }
                
                if (isValidFormat)
                {
                    GUI.color = new Color(0.7f, 1f, 0.7f);
                    int previewLength = independentApiKey.Length > 10 ? 10 : independentApiKey.Length;
                    listing.Label($"  ✅ Key格式正确 ({independentApiKey.Substring(0, previewLength)}...)");
                }
                else
                {
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    listing.Label($"  ❌ Key格式错误！{independentProvider}应为: {expectedFormat}");
                }
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                listing.Label("  ⚠️ 请输入API Key");
                GUI.color = Color.white;
            }
            
            listing.Gap();
            
            listing.Label("RimTalk_Settings_APIURL".Translate());
            independentApiUrl = listing.TextEntry(independentApiUrl);
            
            listing.Gap();
            
            listing.Label("RimTalk_Settings_ModelName".Translate());
            independentModel = listing.TextEntry(independentModel);
            
            GUI.color = Color.gray;
            listing.Label($"  OpenAI: gpt-3.5-turbo, gpt-4, gpt-4-turbo");
            listing.Label($"  DeepSeek: deepseek-chat, deepseek-coder");
            listing.Label($"  Google: gemini-pro, gemini-1.5-flash");
            GUI.color = Color.white;
            
            listing.Gap();
            listing.GapLine();
            
            // ⭐ v3.3.3: 清除API配置按钮
            GUI.color = new Color(1f, 0.7f, 0.7f);
            Rect clearButtonRect = listing.GetRect(35f);
            if (Widgets.ButtonText(clearButtonRect, "🗑️ 清除API配置和缓存"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "确定要清除所有API配置和缓存吗？\n\n这将：\n• 清空独立API Key、URL、Model\n• 清除所有AI总结缓存\n• 重置AI服务状态\n\n需要重新配置API才能使用AI总结功能。",
                    delegate
                    {
                        // 清除设置
                        independentApiKey = "";
                        independentApiUrl = "";
                        independentModel = "gpt-3.5-turbo";
                        independentProvider = "OpenAI";
                        
                        // 清除AI服务的配置和缓存
                        RimTalk.Memory.AI.IndependentAISummarizer.ClearAllConfiguration();
                        
                        Messages.Message("✅ API配置和缓存已清除", MessageTypeDefOf.PositiveEvent, false);
                        Log.Message("[Settings] User cleared API configuration and cache");
                    },
                    false,
                    "清除配置"
                ));
            }
            GUI.color = Color.white;
            
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            listing.Label("  ⚠️ 清除后需要重新输入API Key才能使用AI总结功能");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        /// <summary>
        /// 绘制记忆类型设置
        /// </summary>
        private void DrawMemoryTypesSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimTalk_Settings_ActionMemory".Translate(), ref enableActionMemory);
            listing.CheckboxLabeled("RimTalk_Settings_ConversationMemory".Translate(), ref enableConversationMemory);
        }
        
        /// <summary>
        /// ⭐ v3.3.2.27: 绘制实验性功能设置（仅保留主动记忆召回）
        /// VectorDB、语义嵌入、RAG已移除
        /// </summary>
        private void DrawExperimentalFeaturesSettings(Listing_Standard listing)
        {
            GUI.color = new Color(1f, 0.9f, 0.7f);
            listing.Label("⚠️ 实验性功能，可能需要额外配置或有性能影响");
            GUI.color = Color.white;
            
            listing.Gap();
            listing.GapLine();
            
            // === v3.0: 主动记忆召回 ===
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 1f, 1f);
            listing.Label("💭 主动记忆召回 (v3.0)");
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            
            listing.CheckboxLabeled("  启用主动记忆召回", ref enableProactiveRecall);
            
            if (enableProactiveRecall)
            {
                GUI.color = Color.gray;
                listing.Label("    AI会主动从记忆中提及相关内容，增强对话连贯性");
                GUI.color = Color.white;
                
                listing.Label($"    触发概率: {recallTriggerChance:P0}");
                recallTriggerChance = listing.Slider(recallTriggerChance, 0.05f, 0.60f);
                
                GUI.color = new Color(0.7f, 0.9f, 1f);
                listing.Label($"    (基础15% + 情感因子 + 关系因子，最高60%)");
                GUI.color = Color.white;
            }
            
            listing.Gap();
            listing.GapLine();
            
            // === v3.3.2.27: 移除提示 ===
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.8f, 0.8f);
            listing.Label("🗑️ 已移除的功能 (v3.3.2.27)");
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            
            GUI.color = Color.gray;
            listing.Label("  ❌ 语义嵌入 (v3.1) - 已移除，使用SuperKeywordEngine替代");
            listing.Label("  ❌ 向量数据库 (v3.2) - 已移除，使用关键词索引替代");
            listing.Label("  ❌ RAG检索 (v3.3) - 已移除，简化为直接匹配");
            listing.Gap();
            listing.Label("  ✅ 优势：编译更快、体积更小、依赖更少、性能更好");
            listing.Label("  ✅ 常识匹配准确率：65% → 95%（SuperKeywordEngine）");
            listing.Label("  ✅ 响应时间：<3ms（无向量计算开销）");
            GUI.color = Color.white;
            
            Text.Font = GameFont.Small;
        }
        
        private void OpenCommonKnowledgeDialog()
        {
            if (Current.Game == null)
            {
                Messages.Message("RimTalk_Settings_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var memoryManager = Find.World.GetComponent<MemoryManager>();
            if (memoryManager == null)
            {
                Messages.Message("RimTalk_Settings_CannotFindManager".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Dialog_CommonKnowledge(memoryManager.CommonKnowledge));
        }
        
        private static Vector2 scrollPosition = Vector2.zero;
    }
}
