using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.Memory.API;
using System;
using System.Linq;
using System.Collections.Generic;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 设置 UI 绘制辅助类 - 拆分 UI 代码以减少主文件大小
    /// v3.3.20: 模块化设置界面
    /// </summary>
    public static class SettingsUIDrawers
    {
        // ==================== AI配置绘制 (RimTalk 风格表格) ====================

        /// <summary>
        /// 绘制 RimTalk 风格的多备选 LLM 配置表格。
        /// 不区分简单/高级模式,也不分云端/本地。
        /// </summary>
        public static void DrawRimTalkStyleApiConfigs(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            if (settings.ApiConfigs == null) settings.ApiConfigs = new List<AI.ApiConfig>();
            if (settings.ApiConfigs.Count == 0)
            {
                settings.ApiConfigs.Add(new AI.ApiConfig { Provider = AI.AIProvider.OpenAI, IsEnabled = true });
            }

            // ---------- Header + Add 按钮 ----------
            Rect headerRect = listing.GetRect(24f);
            float addBtnSize = 24f;
            Rect addButtonRect = new Rect(headerRect.xMax - addBtnSize, headerRect.y, addBtnSize, addBtnSize);
            headerRect.width -= addBtnSize + 5f;
            Widgets.Label(headerRect, "RimTalk_Settings_CloudApiConfigurations".Translate());

            // 描述
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect descRect = listing.GetRect(Text.LineHeight * 2);
            descRect.width -= 35f;
            Widgets.Label(descRect, "RimTalk_Settings_CloudApiConfigurationsDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Add 按钮
            Color prevColor = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                settings.ApiConfigs.Add(new AI.ApiConfig { Provider = AI.AIProvider.OpenAI, IsEnabled = true });
            }
            GUI.color = prevColor;

            listing.Gap(6f);

            // ---------- 表头 ----------
            Rect tableHeaderRect = listing.GetRect(20f);
            float x = tableHeaderRect.x;
            float y = tableHeaderRect.y;
            float height = tableHeaderRect.height;
            float totalWidth = tableHeaderRect.width;

            const float providerWidth = 90f;
            const float modelWidth = 190f;
            const float controlsWidth = 100f;
            const float gap = 5f;

            Widgets.Label(new Rect(x, y, providerWidth, height), "RimTalk_Settings_ProviderHeader".Translate());

            float middleStartX = x + providerWidth + gap;
            Widgets.Label(new Rect(middleStartX, y, 200f, height), "RimTalk_Settings_ApiKeyHeader".Translate());

            Widgets.Label(new Rect(x + totalWidth - controlsWidth - modelWidth - gap, y, modelWidth, height),
                "RimTalk_Settings_ModelHeader".Translate());

            Widgets.Label(new Rect(x + totalWidth - controlsWidth + 5f, y, controlsWidth, height),
                "RimTalk_Settings_EnabledHeader".Translate());

            listing.Gap(3f);

            // ---------- 数据行 ----------
            for (int i = 0; i < settings.ApiConfigs.Count; i++)
            {
                bool deleted = DrawApiConfigRow(listing, settings.ApiConfigs[i], i, settings.ApiConfigs);
                if (deleted)
                {
                    settings.ApiConfigs.RemoveAt(i);
                    i--;
                }
                listing.Gap(2f);
            }

            Text.Font = GameFont.Small;
        }

        private static bool DrawApiConfigRow(Listing_Standard listing, AI.ApiConfig config, int index, List<AI.ApiConfig> configs)
        {
            Text.Font = GameFont.Tiny;

            Rect rowRect = listing.GetRect(22f);
            float x = rowRect.x;
            float y = rowRect.y;
            float height = rowRect.height;
            float totalWidth = rowRect.width;

            const float providerWidth = 90f;
            const float modelWidth = 190f;
            const float controlsWidth = 100f;
            const float gap = 5f;

            float middleZoneWidth = totalWidth - providerWidth - modelWidth - controlsWidth - (gap * 3);
            float middleStartX = x + providerWidth + gap;

            Color originalColor = GUI.color;
            if (!config.IsEnabled) GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);

            // 1. Provider 下拉
            DrawProviderDropdown(x, y, height, providerWidth, config);

            // 2. 中段:ApiKey(+ CustomBaseUrl 当 Provider=Custom 时)
            if (config.Provider == AI.AIProvider.Custom)
            {
                float keyWidth = (middleZoneWidth * 0.4f) - (gap / 2);
                float urlWidth = (middleZoneWidth * 0.6f) - (gap / 2);

                DrawApiKeyInput(middleStartX, y, height, keyWidth, config);
                DrawBaseUrlInput(middleStartX + keyWidth + gap, y, height, urlWidth, config);
            }
            else
            {
                DrawApiKeyInput(middleStartX, y, height, middleZoneWidth, config);
            }

            // 3. Model - 统一走文本输入路径(不再有内置默认模型,用户每次都需填入)
            float modelStartX = middleStartX + middleZoneWidth + gap;
            DrawModelTextInput(modelStartX, y, height, modelWidth, config);

            GUI.color = originalColor;

            // 4. 控件区(启用复选框 + ▲▼ + ×) — 坐标相对 rowRect.x
            float btnSize = 22f;
            float btnGap = 2f;
            float deleteX = x + totalWidth - btnSize;
            float downX = deleteX - btnGap - btnSize;
            float upX = downX - btnGap - btnSize;
            float controlsStartX = x + totalWidth - controlsWidth;
            float checkboxSpaceWidth = upX - controlsStartX;
            float checkboxX = controlsStartX + (checkboxSpaceWidth - 24f) / 2f;

            Rect toggleRect = new Rect(checkboxX, y, 24f, height);
            Widgets.Checkbox(new Vector2(toggleRect.x, toggleRect.y), ref config.IsEnabled, 20f);
            if (Mouse.IsOver(toggleRect)) TooltipHandler.TipRegion(toggleRect, "RimTalk_Settings_EnableDisableTooltip".Translate());

            // 排序按钮
            Rect upButtonRect = new Rect(upX, y, btnSize, height);
            if (Widgets.ButtonText(upButtonRect, "\u25B2") && index > 0)
            {
                (configs[index], configs[index - 1]) = (configs[index - 1], configs[index]);
            }

            Rect downButtonRect = new Rect(upX + btnSize + btnGap, y, btnSize, height);
            if (Widgets.ButtonText(downButtonRect, "\u25BC") && index < configs.Count - 1)
            {
                (configs[index], configs[index + 1]) = (configs[index + 1], configs[index]);
            }

            Rect deleteRect = new Rect(deleteX, y, btnSize, height);
            bool deleteClicked = false;
            bool canDelete = configs.Count > 1;
            Color prevColor = GUI.color;
            GUI.color = canDelete ? new Color(1f, 0.4f, 0.4f) : Color.gray;
            if (Widgets.ButtonText(deleteRect, "×", active: canDelete))
            {
                deleteClicked = true;
            }
            GUI.color = prevColor;

            // 实时合法性提示（仅对启用的配置显示）
            if (config.IsEnabled && !config.IsValid)
            {
                Rect hintRect = listing.GetRect(Text.LineHeight);
                GUI.color = new Color(1f, 0.45f, 0.35f);
                Widgets.Label(hintRect, "  " + "RimTalk_Settings_ApiConfigInvalid".Translate());
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Tiny;
            return deleteClicked;
        }

        private static void DrawProviderDropdown(float x, float y, float height, float width, AI.ApiConfig config)
        {
            Rect providerRect = new Rect(x, y, width, height);
            if (Widgets.ButtonText(providerRect, config.Provider.ToString()))
            {
                var options = new List<FloatMenuOption>();
                foreach (AI.AIProvider provider in Enum.GetValues(typeof(AI.AIProvider)))
                {
                    var captured = provider;
                    options.Add(new FloatMenuOption(captured.ToString(), () =>
                    {
                        config.Provider = captured;
                        // 切换 Provider 时清空自定义字段,回落到 Registry 默认
                        config.CustomUrl = "";
                        config.CustomModelName = "";
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static void DrawApiKeyInput(float x, float y, float height, float width, AI.ApiConfig config)
        {
            Rect rect = new Rect(x, y, width, height);
            config.ApiKey = DrawTextFieldWithPlaceholder(rect, config.ApiKey, "RimTalk_Settings_PlaceholderApiKey".Translate());
        }

        private static void DrawBaseUrlInput(float x, float y, float height, float width, AI.ApiConfig config)
        {
            Rect rect = new Rect(x, y, width, height);
            config.CustomUrl = DrawTextFieldWithPlaceholder(rect, config.CustomUrl, "https://...");
            if (Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, "RimTalk_Settings_BaseUrlTooltip".Translate());
        }

        private static void DrawModelTextInput(float x, float y, float height, float width, AI.ApiConfig config)
        {
            Rect rect = new Rect(x, y, width, height);
            config.CustomModelName = DrawTextFieldWithPlaceholder(rect, config.CustomModelName, "RimTalk_Settings_PlaceholderModelId".Translate());
        }

        private static string DrawTextFieldWithPlaceholder(Rect rect, string text, string placeholder)
        {
            string result = Widgets.TextField(rect, text ?? string.Empty);
            if (string.IsNullOrEmpty(result))
            {
                TextAnchor originalAnchor = Text.Anchor;
                Color originalColor = GUI.color;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
                Rect labelRect = new Rect(rect.x + 5f, rect.y, rect.width - 5f, rect.height);
                Widgets.Label(labelRect, placeholder);
                GUI.color = originalColor;
                Text.Anchor = originalAnchor;
            }
            return result;
        }

        // ==================== 匹配源选择设置绘制 ====================

        // 缓存从 RimTalk 获取的分类变量列表
        private static Dictionary<string, List<(string name, string description, bool isPawnProperty)>> _cachedCategorizedSources = null;

        // 滚动位置
        private static Vector2 _matchingSourcesScrollPosition = Vector2.zero;

        /// <summary>
        /// v4.1: 绘制知识匹配源选择 UI
        /// 使用独立的 ScrollView 绘制，通过 GUI.BeginGroup 实现嵌套
        /// </summary>
        public static void DrawKnowledgeMatchingSourcesSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            GUI.color = new Color(0.8f, 1f, 0.8f);
            listing.Label("RimTalk_Settings_KnowledgeMatchingSources".Translate());
            GUI.color = Color.white;

            GUI.color = Color.gray;
            listing.Label("  " + "RimTalk_Settings_KnowledgeMatchingSourcesDesc".Translate());
            GUI.color = Color.white;
            listing.Gap();

            // 刷新按钮
            Rect refreshRect = listing.GetRect(25f);
            if (Widgets.ButtonText(new Rect(refreshRect.x, refreshRect.y, 120f, 25f), "RimTalk_Settings_RefreshList".Translate()))
            {
                MustacheVariableHelper.ClearCache();
                _cachedCategorizedSources = null;
                Messages.Message("RimTalk_Settings_ListRefreshed".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            // 获取分类数据
            var categorizedSources = GetCachedCategorizedSources();

            // 统计信息
            int totalVars = categorizedSources.Sum(c => c.Value.Count);
            int pawnProps = categorizedSources.Sum(c => c.Value.Count(v => v.isPawnProperty));
            Rect countRect = new Rect(refreshRect.x + 130f, refreshRect.y, 250f, 25f);
            GUI.color = Color.gray;
            Widgets.Label(countRect, "RimTalk_Settings_VariableCount".Translate(totalVars, pawnProps));
            GUI.color = Color.white;

            listing.Gap();

            if (settings.knowledgeMatchingSources == null || settings.knowledgeMatchingSources.Count == 0)
            {
                settings.knowledgeMatchingSources = new List<string> { "dialogue", "context" };
            }

            // 计算内容高度
            float rowHeight = 22f;
            float categoryHeaderHeight = 26f;
            float contentHeight = 0f;
            foreach (var category in categorizedSources)
            {
                contentHeight += categoryHeaderHeight;
                contentHeight += category.Value.Count * rowHeight + 2f;
            }

            // 固定显示区域高度，使用内部 ScrollView
            float displayHeight = Mathf.Min(contentHeight + 10f, 320f);
            Rect outerRect = listing.GetRect(displayHeight);
            Widgets.DrawBoxSolid(outerRect, new Color(0.12f, 0.12f, 0.12f, 0.5f));

            // 使用 GUI.BeginGroup 创建独立的绘制区域
            Rect innerRect = outerRect.ContractedBy(5f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, contentHeight);

            // 开始 ScrollView
            Widgets.BeginScrollView(innerRect, ref _matchingSourcesScrollPosition, viewRect);

            float curY = 0f;
            float width = viewRect.width;

            foreach (var category in categorizedSources)
            {
                // 分类标题
                Rect headerRect = new Rect(0f, curY, width, categoryHeaderHeight - 2f);
                Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.25f, 0.3f, 0.8f));
                GUI.color = new Color(0.9f, 0.95f, 1f);
                Widgets.Label(new Rect(8f, curY + 3f, width - 16f, 20f), $"{category.Key} ({category.Value.Count})");
                GUI.color = Color.white;
                curY += categoryHeaderHeight;

                // 绘制该分类下的变量
                foreach (var variable in category.Value)
                {
                    bool isSelected = settings.knowledgeMatchingSources.Contains(variable.name);
                    bool newValue = isSelected;

                    Rect rowRect = new Rect(0f, curY, width, rowHeight - 2f);

                    // 高亮选中行
                    if (isSelected)
                    {
                        Widgets.DrawBoxSolid(rowRect, new Color(0.2f, 0.4f, 0.2f, 0.3f));
                    }

                    // 鼠标悬停高亮
                    if (Mouse.IsOver(rowRect))
                    {
                        Widgets.DrawHighlight(rowRect);
                    }

                    // 复选框
                    Widgets.Checkbox(new Vector2(8f, curY + 1f), ref newValue);

                    // Pawn 属性标记
                    float nameStartX = 32f;
                    if (variable.isPawnProperty)
                    {
                        Rect tagRect = new Rect(nameStartX, curY + 2f, 45f, 18f);
                        Widgets.DrawBoxSolid(tagRect, new Color(0.3f, 0.5f, 0.7f, 0.5f));
                        GUI.color = new Color(0.7f, 0.9f, 1f);
                        Text.Font = GameFont.Tiny;
                        Widgets.Label(new Rect(tagRect.x + 3f, curY + 3f, 40f, 16f), "Pawn");
                        Text.Font = GameFont.Small;
                        GUI.color = Color.white;
                        nameStartX += 50f;
                    }

                    // 变量名
                    GUI.color = isSelected ? new Color(0.5f, 1f, 0.5f) : new Color(0.9f, 0.9f, 0.7f);
                    Widgets.Label(new Rect(nameStartX, curY + 1f, 140f, 20f), variable.name);
                    GUI.color = Color.white;

                    // 描述
                    Text.Font = GameFont.Tiny;
                    GUI.color = Color.gray;
                    string shortDesc = variable.description.Length > 30
                        ? variable.description.Substring(0, 27) + "..."
                        : variable.description;
                    Widgets.Label(new Rect(nameStartX + 145f, curY + 2f, width - nameStartX - 150f, 18f), shortDesc);
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;

                    // 工具提示
                    string tooltip = variable.isPawnProperty
                        ? "RimTalk_Settings_PawnPropertyTooltip".Translate(variable.name, variable.description)
                        : "RimTalk_Settings_VariableTooltip".Translate(variable.name, variable.description);
                    TooltipHandler.TipRegion(rowRect, tooltip);

                    // 更新选择状态
                    if (newValue != isSelected)
                    {
                        if (newValue)
                        {
                            if (!settings.knowledgeMatchingSources.Contains(variable.name))
                            {
                                settings.knowledgeMatchingSources.Add(variable.name);
                            }
                        }
                        else
                        {
                            settings.knowledgeMatchingSources.Remove(variable.name);
                            if (settings.knowledgeMatchingSources.Count == 0)
                            {
                                settings.knowledgeMatchingSources.Add("dialogue");
                                Messages.Message("RimTalk_Settings_AtLeastOneSource".Translate(), MessageTypeDefOf.RejectInput);
                            }
                        }
                    }

                    curY += rowHeight;
                }

                curY += 2f;
            }

            Widgets.EndScrollView();

            listing.Gap();

            // 显示当前选择统计
            int selectedPawnProps = settings.knowledgeMatchingSources.Count(s =>
                categorizedSources.Any(c => c.Value.Any(v => v.name == s && v.isPawnProperty)));
            int selectedOther = settings.knowledgeMatchingSources.Count - selectedPawnProps;

            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("RimTalk_Settings_SelectedCount".Translate(settings.knowledgeMatchingSources.Count, selectedPawnProps, selectedOther));

            // 显示选择的变量名
            string selectedStr = settings.knowledgeMatchingSources.Count <= 4
                ? string.Join(", ", settings.knowledgeMatchingSources)
                : $"{string.Join(", ", settings.knowledgeMatchingSources.Take(3))}... (+{settings.knowledgeMatchingSources.Count - 3})";
            GUI.color = Color.gray;
            listing.Label($"  {selectedStr}");
            GUI.color = Color.white;

            listing.Gap();
            listing.GapLine();
        }

        /// <summary>
        /// 获取缓存的分类变量列表
        /// </summary>
        private static Dictionary<string, List<(string name, string description, bool isPawnProperty)>> GetCachedCategorizedSources()
        {
            if (_cachedCategorizedSources == null)
            {
                _cachedCategorizedSources = MustacheVariableHelper.GetCategorizedMatchingSources();
            }
            return _cachedCategorizedSources;
        }

        // ==================== 常识链设置绘制 ====================

        public static void DrawKnowledgeChainingSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            // 常识链设置（实验性功能）
            listing.CheckboxLabeled("RimTalk_Settings_EnableKnowledgeChaining".Translate(), ref settings.enableKnowledgeChaining);
            if (settings.enableKnowledgeChaining)
            {
                GUI.color = new Color(1f, 0.8f, 0.5f);
                listing.Label("  " + "RimTalk_Settings_KnowledgeChainingDesc".Translate());
                GUI.color = Color.white;
                listing.Gap();

                listing.Label("RimTalk_Settings_MaxChainingRoundsLabel".Translate(settings.maxChainingRounds));
                settings.maxChainingRounds = (int)listing.Slider(settings.maxChainingRounds, 1, 5);
            }

            listing.Gap();
        }

        // ==================== 提示词规范化设置绘制 ====================

        public static void DrawPromptNormalizationSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            // 背景框
            Rect sectionRect = listing.GetRect(300f);
            Widgets.DrawBoxSolid(sectionRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));

            Listing_Standard inner = new Listing_Standard();
            inner.Begin(sectionRect.ContractedBy(10f));

            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            inner.Label("RimTalk_Settings_ReplacementRulesList".Translate());
            GUI.color = Color.white;

            inner.Gap(5f);

            // 规则列表
            if (settings.normalizationRules == null)
            {
                settings.normalizationRules = new System.Collections.Generic.List<RimTalkMemoryPatchSettings.ReplacementRule>();
            }

            // 绘制每条规则
            for (int i = 0; i < settings.normalizationRules.Count; i++)
            {
                var rule = settings.normalizationRules[i];

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
                if (Widgets.ButtonText(deleteRect, "×"))
                {
                    settings.normalizationRules.RemoveAt(i);
                    i--;
                }
                GUI.color = Color.white;

                inner.Gap(3f);
            }

            // 添加新规则按钮
            Rect addButtonRect = inner.GetRect(30f);
            if (Widgets.ButtonText(addButtonRect, "RimTalk_Settings_AddNewRule".Translate()))
            {
                settings.normalizationRules.Add(new RimTalkMemoryPatchSettings.ReplacementRule("", "", true));
            }

            inner.Gap(5f);

            // 统计信息
            int enabledCount = settings.normalizationRules.Count(r => r.isEnabled);
            GUI.color = Color.gray;
            inner.Label("RimTalk_Settings_EnabledRulesCount".Translate(enabledCount, settings.normalizationRules.Count));
            GUI.color = Color.white;

            // 示例提示
            inner.Gap(3f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            inner.Label("RimTalk_Settings_RuleExample".Translate());
            inner.Label("   " + "RimTalk_Settings_RegexSupport".Translate());
            GUI.color = Color.white;

            inner.End();
        }

        // ==================== 向量增强设置绘制 ====================

        public static void DrawSiliconFlowSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            listing.CheckboxLabeled("RimTalk_Settings_EnableVectorEnhancement".Translate(), ref settings.enableVectorEnhancement);
            if (settings.enableVectorEnhancement)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  " + "RimTalk_Settings_VectorEnhancementDesc".Translate());
                GUI.color = Color.white;
                listing.Gap();

                listing.Label("RimTalk_Settings_SimilarityThresholdLabel".Translate(settings.vectorSimilarityThreshold.ToString("F2")));
                settings.vectorSimilarityThreshold = listing.Slider(settings.vectorSimilarityThreshold, 0.5f, 1.0f);

                listing.Label("RimTalk_Settings_MaxVectorResultsLabel".Translate(settings.maxVectorResults));
                settings.maxVectorResults = (int)listing.Slider(settings.maxVectorResults, 1, 15);

                listing.Gap();

                GUI.color = new Color(1f, 0.9f, 0.7f);
                listing.Label("RimTalk_Settings_CloudEmbeddingConfig".Translate());
                GUI.color = Color.white;

                listing.Label("RimTalk_Settings_EmbeddingAPIKey".Translate() + ":");
                settings.embeddingApiKey = listing.TextEntry(settings.embeddingApiKey);

                listing.Label("RimTalk_Settings_EmbeddingAPIURL".Translate() + ":");
                settings.embeddingApiUrl = listing.TextEntry(settings.embeddingApiUrl);

                listing.Label("RimTalk_Settings_EmbeddingModel".Translate() + ":");
                settings.embeddingModel = listing.TextEntry(settings.embeddingModel);

                GUI.color = Color.gray;
                listing.Label("RimTalk_Settings_EmbeddingAPIKeyTip".Translate());
                GUI.color = Color.white;
            }

            listing.Gap();
        }
    }
}