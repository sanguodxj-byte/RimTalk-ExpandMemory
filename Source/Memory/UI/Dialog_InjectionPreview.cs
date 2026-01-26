
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;
using RimTalk.Memory.API;

namespace RimTalk.Memory.Debug
{
    /// <summary>
    /// 调试预览器 - 分析记忆和常识注入内容
    /// v5.0 重构版
    /// 
    /// 主要功能:
    /// 1. 常识部分: 匹配源选择、Scriban解析、被匹配常识显示
    /// 2. 记忆部分: 保持现状
    /// 
    /// ⭐ 匹配源与 RimTalkSettings.knowledgeMatchingSources 同步
    /// </summary>
    public class Dialog_InjectionPreview : Window
    {
        // ===== 状态字段 =====
        private Pawn selectedPawn;
        private Pawn targetPawn;
        private Vector2 scrollPositionLeft;
        private Vector2 scrollPositionRight;
        private Vector2 scrollPositionMatchSource;
        private Vector2 scrollPositionParsedText;
        
        // ===== 常识匹配源（与Settings同步）=====
        private List<(string name, string description, bool isPawnProperty)> availableMatchingSources;
        private string parsedMatchText = "";
        private List<KnowledgeScore> matchedKnowledge = new List<KnowledgeScore>();
        
        // ===== 记忆预览 =====
        private string memoryPreviewText = "";
        private int cachedMemoryCount = 0;
        
        // ===== UI状态 =====
        private bool showMatchSourcePanel = true;
        private bool showKnowledgePanel = true;
        private bool showMemoryPanel = true;

        public override Vector2 InitialSize => new Vector2(1200f, 800f);

        public Dialog_InjectionPreview()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            
            // 默认选择第一个殖民者
            if (Find.CurrentMap != null)
            {
                selectedPawn = Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault();
            }
            
            // 初始化匹配源
            LoadAvailableMatchingSources();
        }

        public override void DoWindowContents(Rect inRect)
        {
            float yPos = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            Widgets.Label(new Rect(0f, yPos, 600f, 35f), "RimTalk_Preview_Title".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            yPos += 40f;

            // Pawn选择器
            DrawPawnSelectors(new Rect(0f, yPos, inRect.width, 80f));
            yPos += 85f;

            if (selectedPawn == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, inRect.height / 2 - 20f, inRect.width, 40f), 
                    "RimTalk_Preview_NoColonist".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 使用帮助按钮
            Rect helpButtonRect = new Rect(inRect.width - 240f, yPos, 110f, 30f);
            if (Widgets.ButtonText(helpButtonRect, "RimTalk_Preview_Help".Translate()))
            {
                ShowHelpDialog();
            }
            
            // 刷新按钮
            Rect refreshButtonRect = new Rect(inRect.width - 120f, yPos, 110f, 30f);
            if (Widgets.ButtonText(refreshButtonRect, "RimTalk_Preview_Refresh".Translate()))
            {
                RefreshPreview();
            }
            yPos += 35f;

            // 主内容区域：左右两栏
            float contentHeight = inRect.height - yPos - 50f;
            float halfWidth = (inRect.width - 15f) / 2f;
            
            // 左栏：常识部分
            Rect leftRect = new Rect(0f, yPos, halfWidth, contentHeight);
            DrawKnowledgePanel(leftRect);
            
            // 右栏：记忆部分
            Rect rightRect = new Rect(halfWidth + 15f, yPos, halfWidth, contentHeight);
            DrawMemoryPanel(rightRect);
        }

        #region Pawn选择器
        
        private void DrawPawnSelectors(Rect rect)
        {
            // 第一行：当前角色选择器
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, 30f), "RimTalk_Preview_CurrentPawn".Translate());
            GUI.color = Color.white;

            Rect buttonRect = new Rect(rect.x + 130f, rect.y, 200f, 30f);
            string label = selectedPawn != null ? selectedPawn.LabelShort : (string)"RimTalk_Preview_None".Translate();
            if (Widgets.ButtonText(buttonRect, label))
            {
                ShowPawnSelectionMenu(isPrimary: true);
            }

            // 显示选中殖民者的基本信息
            if (selectedPawn != null)
            {
                GUI.color = Color.gray;
                string info = $"{selectedPawn.def.label} | {selectedPawn.gender.GetLabel()}";
                Widgets.Label(new Rect(rect.x + 340f, rect.y + 5f, 300f, 30f), info);
                GUI.color = Color.white;
            }

            // 第二行：目标角色选择器
            float secondRowY = rect.y + 35f;
            GUI.color = new Color(1f, 0.9f, 0.8f);
            Widgets.Label(new Rect(rect.x, secondRowY, 120f, 30f), "RimTalk_Preview_TargetPawn".Translate());
            GUI.color = Color.white;

            Rect targetButtonRect = new Rect(rect.x + 130f, secondRowY, 200f, 30f);
            string targetLabel = targetPawn != null ? targetPawn.LabelShort : (string)"RimTalk_Preview_NoneClickToSelect".Translate();
            if (Widgets.ButtonText(targetButtonRect, targetLabel))
            {
                ShowPawnSelectionMenu(isPrimary: false);
            }

            // 显示目标角色信息
            if (targetPawn != null)
            {
                GUI.color = Color.gray;
                string targetInfo = $"{targetPawn.def.label} | {targetPawn.gender.GetLabel()}";
                Widgets.Label(new Rect(rect.x + 340f, secondRowY + 5f, 250f, 30f), targetInfo);
                GUI.color = Color.white;
                
                // 清除按钮
                Rect clearButtonRect = new Rect(rect.x + 600f, secondRowY, 80f, 30f);
                if (Widgets.ButtonText(clearButtonRect, "RimTalk_Preview_Clear".Translate()))
                {
                    targetPawn = null;
                    RefreshPreview();
                }
            }
        }

        private void ShowPawnSelectionMenu(bool isPrimary)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            if (Find.CurrentMap != null)
            {
                var allHumanlikes = Find.CurrentMap.mapPawns.AllPawnsSpawned
                    .Where(p => p.RaceProps.Humanlike)
                    .OrderBy(p =>
                    {
                        if (p.IsColonist) return 1;
                        if (p.IsPrisoner) return 2;
                        if (p.IsSlaveOfColony) return 3;
                        if (p.HostFaction == Faction.OfPlayer) return 4;
                        return 5;
                    })
                    .ThenBy(p => p.LabelShort);
                
                foreach (var pawn in allHumanlikes)
                {
                    Pawn localPawn = pawn;
                    
                    string optionLabel = pawn.LabelShort;
                    
                    if (pawn.IsColonist)
                        optionLabel += " " + "RimTalk_Preview_Colonist".Translate();
                    else if (pawn.IsPrisoner)
                        optionLabel += " " + "RimTalk_Preview_Prisoner".Translate();
                    else if (pawn.IsSlaveOfColony)
                        optionLabel += " " + "RimTalk_Preview_Slave".Translate();
                    else if (pawn.HostFaction == Faction.OfPlayer)
                        optionLabel += " " + "RimTalk_Preview_Guest".Translate();
                    else if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
                        optionLabel += $" ({pawn.Faction.Name})";
                    
                    if (!isPrimary && selectedPawn != null && pawn == selectedPawn)
                        optionLabel += " " + "RimTalk_Preview_SameAsCurrent".Translate();
                    
                    options.Add(new FloatMenuOption(optionLabel, delegate
                    {
                        if (isPrimary)
                        {
                            selectedPawn = localPawn;
                            if (targetPawn == localPawn)
                                targetPawn = null;
                        }
                        else
                        {
                            targetPawn = localPawn;
                        }
                        RefreshPreview();
                    }));
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Messages.Message("RimTalk_Preview_NoHumanlikes".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
        
        #endregion

        #region 常识面板
        
        private void DrawKnowledgePanel(Rect rect)
        {
            // 面板标题
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            
            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Text.Font = GameFont.Medium;
            GUI.color = new Color(1f, 1f, 0.8f);
            
            string knowledgeTitle = showKnowledgePanel ? "▼ " : "▶ ";
            knowledgeTitle += "RimTalk_Preview_KnowledgeSection".Translate();
            
            if (Widgets.ButtonText(titleRect, knowledgeTitle, false))
            {
                showKnowledgePanel = !showKnowledgePanel;
            }
            
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            
            if (!showKnowledgePanel)
                return;
            
            float yPos = rect.y + 35f;
            float contentWidth = rect.width - 10f;
            
            // 1. 匹配源选择区域
            Rect matchSourceHeaderRect = new Rect(rect.x + 5f, yPos, contentWidth, 25f);
            GUI.color = new Color(0.8f, 0.9f, 1f);
            
            string matchSourceTitle = showMatchSourcePanel ? "▼ " : "▶ ";
            matchSourceTitle += "RimTalk_Preview_MatchingSource".Translate();
            
            if (Widgets.ButtonText(matchSourceHeaderRect, matchSourceTitle, false))
            {
                showMatchSourcePanel = !showMatchSourcePanel;
            }
            GUI.color = Color.white;
            yPos += 28f;
            
            if (showMatchSourcePanel)
            {
                float matchSourceHeight = 150f;
                Rect matchSourceRect = new Rect(rect.x + 5f, yPos, contentWidth, matchSourceHeight);
                DrawMatchingSourceSelector(matchSourceRect);
                yPos += matchSourceHeight + 5f;
            }
            
            // 2. 解析结果区域（可滚动，高度自适应）
            Rect parsedTitleRect = new Rect(rect.x + 5f, yPos, contentWidth, 25f);
            GUI.color = new Color(0.9f, 1f, 0.8f);
            Widgets.Label(parsedTitleRect, "RimTalk_Preview_ParsedMatchText".Translate());
            GUI.color = Color.white;
            yPos += 28f;
            
            // ⭐ 增加高度到 180f，并支持滚动
            float parsedTextHeight = 180f;
            Rect parsedTextRect = new Rect(rect.x + 5f, yPos, contentWidth, parsedTextHeight);
            Widgets.DrawBoxSolid(parsedTextRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            
            if (string.IsNullOrEmpty(parsedMatchText))
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(parsedTextRect, "RimTalk_Preview_NoParsedText".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            else
            {
                // ⭐ 使用滚动视图显示完整内容
                Rect innerRect = parsedTextRect.ContractedBy(5f);
                float textHeight = Text.CalcHeight(parsedMatchText, innerRect.width - 20f);
                Rect viewRect = new Rect(0f, 0f, innerRect.width - 20f, Mathf.Max(textHeight + 10f, innerRect.height));
                
                Widgets.BeginScrollView(innerRect, ref scrollPositionParsedText, viewRect);
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), parsedMatchText);
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }
            yPos += parsedTextHeight + 5f;
            
            // 3. 匹配到的常识区域
            Rect matchedTitleRect = new Rect(rect.x + 5f, yPos, contentWidth, 25f);
            GUI.color = new Color(1f, 0.9f, 0.7f);
            string matchedTitle = "RimTalk_Preview_MatchedKnowledge".Translate(matchedKnowledge.Count);
            Widgets.Label(matchedTitleRect, matchedTitle);
            GUI.color = Color.white;
            yPos += 28f;
            
            Rect knowledgeListRect = new Rect(rect.x + 5f, yPos, contentWidth, rect.yMax - yPos - 10f);
            DrawMatchedKnowledgeList(knowledgeListRect);
        }
        
        /// <summary>
        /// 绘制匹配源选择器
        /// ⭐ 与 Settings.knowledgeMatchingSources 同步
        /// </summary>
        private void DrawMatchingSourceSelector(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f, 0.6f));
            
            if (availableMatchingSources == null || availableMatchingSources.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect.ContractedBy(5f), "RimTalk_Preview_NoMatchingSources".Translate());
                GUI.color = Color.white;
                return;
            }
            
            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings == null) return;
            
            Rect innerRect = rect.ContractedBy(5f);
            float lineHeight = 22f;
            int columns = 3;
            float columnWidth = innerRect.width / columns;
            
            float totalHeight = Mathf.Ceil(availableMatchingSources.Count / (float)columns) * lineHeight;
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 20f, totalHeight);
            
            Widgets.BeginScrollView(innerRect, ref scrollPositionMatchSource, viewRect);
            
            int index = 0;
            foreach (var source in availableMatchingSources)
            {
                int col = index % columns;
                int row = index / columns;
                
                Rect checkboxRect = new Rect(col * columnWidth, row * lineHeight, columnWidth - 5f, lineHeight);
                
                // ⭐ 从Settings读取选中状态
                bool isSelected = settings.knowledgeMatchingSources.Contains(source.name);
                // 使用 [P] 标记 Pawn 属性变量（RimWorld 不支持 emoji）
                string label = source.isPawnProperty ? $"[P] {source.name}" : source.name;
                
                // 使用tooltip显示描述
                TooltipHandler.TipRegion(checkboxRect, source.description);
                
                bool newSelected = isSelected;
                Widgets.CheckboxLabeled(checkboxRect, label, ref newSelected);
                
                // ⭐ 修改同步到Settings
                if (newSelected != isSelected)
                {
                    if (newSelected)
                    {
                        if (!settings.knowledgeMatchingSources.Contains(source.name))
                            settings.knowledgeMatchingSources.Add(source.name);
                    }
                    else
                    {
                        settings.knowledgeMatchingSources.Remove(source.name);
                    }
                    
                    // 立即刷新解析结果
                    RefreshParsedMatchText();
                }
                
                index++;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawMatchedKnowledgeList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            
            if (matchedKnowledge.Count == 0)
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "RimTalk_Preview_NoMatchedKnowledge".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }
            
            Rect innerRect = rect.ContractedBy(5f);
            float entryHeight = 60f;
            float totalHeight = matchedKnowledge.Count * entryHeight;
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 20f, totalHeight);
            
            Widgets.BeginScrollView(innerRect, ref scrollPositionLeft, viewRect);
            
            for (int i = 0; i < matchedKnowledge.Count; i++)
            {
                var ks = matchedKnowledge[i];
                Rect entryRect = new Rect(0f, i * entryHeight, viewRect.width, entryHeight - 5f);
                
                // 背景
                Widgets.DrawBoxSolid(entryRect, new Color(0.15f, 0.18f, 0.15f, 0.4f));
                
                // 标签和评分
                GUI.color = new Color(0.9f, 0.9f, 0.6f);
                string headerText = $"[{i + 1}] [{ks.Entry.tag}] " + "RimTalk_Preview_Score".Translate(ks.Score.ToString("F2"));
                Widgets.Label(new Rect(entryRect.x + 5f, entryRect.y + 2f, entryRect.width - 10f, 20f), headerText);
                GUI.color = Color.white;
                
                // 内容
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                string contentPreview = ks.Entry.content.Length > 100 
                    ? ks.Entry.content.Substring(0, 100) + "..." 
                    : ks.Entry.content;
                Widgets.Label(new Rect(entryRect.x + 5f, entryRect.y + 22f, entryRect.width - 10f, 35f), contentPreview);
                GUI.color = Color.white;
            }
            
            Widgets.EndScrollView();
        }
        
        #endregion

        #region 记忆面板
        
        private void DrawMemoryPanel(Rect rect)
        {
            // 面板标题
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.18f, 0.8f));
            
            Rect titleRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 25f);
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            
            string memoryTitle = showMemoryPanel ? "▼ " : "▶ ";
            memoryTitle += "RimTalk_Preview_MemorySection".Translate(cachedMemoryCount);
            
            if (Widgets.ButtonText(titleRect, memoryTitle, false))
            {
                showMemoryPanel = !showMemoryPanel;
            }
            
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            
            if (!showMemoryPanel)
                return;
            
            float yPos = rect.y + 35f;
            
            // 记忆统计
            if (selectedPawn != null)
            {
                var memoryComp = selectedPawn.TryGetComp<FourLayerMemoryComp>();
                if (memoryComp != null)
                {
                    Rect statsRect = new Rect(rect.x + 5f, yPos, rect.width - 10f, 50f);
                    DrawMemoryStats(statsRect, memoryComp);
                    yPos += 55f;
                }
            }
            
            // 记忆预览内容
            Rect previewRect = new Rect(rect.x + 5f, yPos, rect.width - 10f, rect.yMax - yPos - 10f);
            DrawMemoryPreviewContent(previewRect);
        }
        
        private void DrawMemoryStats(Rect rect, FourLayerMemoryComp memoryComp)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.12f, 0.5f));
            
            float x = rect.x + 5f;
            float lineHeight = 22f;
            
            // 第一行 - 记忆层级统计
            GUI.color = new Color(0.7f, 0.7f, 1f);
            Widgets.Label(new Rect(x, rect.y + 3f, 100f, lineHeight), "RimTalk_Preview_MemoryLayers".Translate());
            GUI.color = Color.white;
            
            x += 100f;
            Widgets.Label(new Rect(x, rect.y + 3f, 120f, lineHeight), 
                $"ABM: {memoryComp.ActiveMemories.Count}");
            
            x += 100f;
            Widgets.Label(new Rect(x, rect.y + 3f, 120f, lineHeight), 
                $"SCM: {memoryComp.SituationalMemories.Count}");
            
            x += 100f;
            Widgets.Label(new Rect(x, rect.y + 3f, 120f, lineHeight), 
                $"ELS: {memoryComp.EventLogMemories.Count}");
            
            x += 100f;
            Widgets.Label(new Rect(x, rect.y + 3f, 120f, lineHeight), 
                $"CLPA: {memoryComp.ArchiveMemories.Count}");
            
            // 第二行 - 注入配置
            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings != null)
            {
                x = rect.x + 5f;
                GUI.color = new Color(0.8f, 0.8f, 1f);
                string configText = "RimTalk_Preview_InjectionConfig".Translate(
                    settings.maxInjectedMemories);
                Widgets.Label(new Rect(x, rect.y + 25f, rect.width - 10f, lineHeight), configText);
                GUI.color = Color.white;
            }
        }
        
        private void DrawMemoryPreviewContent(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f, 0.6f));
            
            if (string.IsNullOrEmpty(memoryPreviewText))
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "RimTalk_Preview_ClickRefresh".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }
            
            Rect innerRect = rect.ContractedBy(5f);
            float contentHeight = Text.CalcHeight(memoryPreviewText, innerRect.width - 20f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 20f, contentHeight + 20f);
            
            Widgets.BeginScrollView(innerRect, ref scrollPositionRight, viewRect);
            
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, contentHeight), memoryPreviewText);
            GUI.color = Color.white;
            
            Widgets.EndScrollView();
        }
        
        #endregion

        #region 数据加载和刷新
        
        // ⭐ 缓存 RimTalk 类型以避免重复反射
        private static Type _scribanParserType;
        private static Type _promptContextType;
        private static Type _promptManagerType;
        private static MethodInfo _renderMethod;
        private static PropertyInfo _lastContextProperty;
        private static bool _rimTalkTypesResolved = false;
        
        private void LoadAvailableMatchingSources()
        {
            availableMatchingSources = MustacheVariableHelper.GetMatchingPropertyCategories();
            
            if (availableMatchingSources == null || availableMatchingSources.Count == 0)
            {
                // 使用备用列表
                availableMatchingSources = new List<(string, string, bool)>
                {
                    ("prompt", "Dialogue prompt", false),
                    ("fullname", "Pawn full name", true),
                    ("role", "Pawn role", true),
                    ("age", "Pawn age", true),
                    ("gender", "Pawn gender", true),
                    ("backstory", "Pawn backstory", true),
                    ("traits", "Pawn traits", true),
                    ("skills", "Skills", true),
                    ("relations", "Relations", true),
                };
            }
            
            // 初始化 RimTalk 类型
            ResolveRimTalkTypes();
        }
        
        /// <summary>
        /// 解析 RimTalk 类型（只需做一次）
        /// </summary>
        private void ResolveRimTalkTypes()
        {
            if (_rimTalkTypesResolved) return;
            _rimTalkTypesResolved = true;
            
            try
            {
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (rimTalkAssembly == null) return;
                
                _scribanParserType = rimTalkAssembly.GetType("RimTalk.Prompt.ScribanParser");
                _promptContextType = rimTalkAssembly.GetType("RimTalk.Prompt.PromptContext");
                _promptManagerType = rimTalkAssembly.GetType("RimTalk.Prompt.PromptManager");
                
                if (_scribanParserType != null)
                {
                    // ScribanParser.Render(string templateText, PromptContext context, bool logErrors = true)
                    _renderMethod = _scribanParserType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static);
                }
                
                if (_promptManagerType != null)
                {
                    // PromptManager.LastContext - 存储上次对话的完整上下文
                    _lastContextProperty = _promptManagerType.GetProperty("LastContext", BindingFlags.Public | BindingFlags.Static);
                }
            }
            catch
            {
                // 静默处理
            }
        }
        
        private void RefreshPreview()
        {
            RefreshParsedMatchText();
            RefreshMatchedKnowledge();
            RefreshMemoryPreview();
        }
        
        /// <summary>
        /// 刷新解析后的匹配文本
        /// ⭐ 使用 RimTalk 的 ScribanParser.Render 静默解析
        /// ⭐ Pawn 属性变量会同时解析所有参与者，用逗号分隔
        /// </summary>
        private void RefreshParsedMatchText()
        {
            var settings = RimTalkMemoryPatchMod.Settings;
            if (selectedPawn == null || settings == null || settings.knowledgeMatchingSources.Count == 0)
            {
                parsedMatchText = "";
                return;
            }
            
            var sb = new StringBuilder();
            
            // 构建参与者列表
            var participants = new List<Pawn> { selectedPawn };
            if (targetPawn != null && targetPawn != selectedPawn)
            {
                participants.Add(targetPawn);
            }
            
            foreach (var sourceName in settings.knowledgeMatchingSources)
            {
                try
                {
                    // 判断是否是Pawn属性
                    bool isPawnProperty = availableMatchingSources
                        .Any(s => s.name == sourceName && s.isPawnProperty);
                    
                    if (isPawnProperty)
                    {
                        // ⭐ 同时解析所有参与者的属性值
                        var values = new List<string>();
                        var pawnNames = new List<string>();
                        
                        foreach (var pawn in participants)
                        {
                            // isPawnProperty = true 使用 Pawn 对象解析
                            string parsed = RenderWithScriban($"{{{{ pawn.{sourceName} }}}}", pawn, null, isPawnProperty: true);
                            if (!string.IsNullOrEmpty(parsed) && !parsed.Contains("{{"))
                            {
                                values.Add(parsed);
                                pawnNames.Add($"@{pawn.LabelShort}");
                            }
                        }
                        
                        if (values.Count > 0)
                        {
                            if (sb.Length > 0) sb.AppendLine();
                            // 格式: [name @当前角色 @目标角色]
                            sb.AppendLine($"[{sourceName} {string.Join(" ", pawnNames)}]");
                            // 值用逗号加换行分隔，可读性更好
                            sb.Append(string.Join(",\n", values));
                        }
                    }
                    else
                    {
                        // 非 Pawn 属性（如 prompt），使用 PromptManager.LastContext 解析
                        // isPawnProperty = false 使用上次对话的完整上下文
                        string parsed = RenderWithScriban($"{{{{ {sourceName} }}}}", selectedPawn, targetPawn, isPawnProperty: false);
                        if (!string.IsNullOrEmpty(parsed) && !parsed.Contains("{{"))
                        {
                            if (sb.Length > 0) sb.AppendLine();
                            sb.AppendLine($"[{sourceName}]");
                            sb.Append(parsed);
                        }
                    }
                }
                catch
                {
                    // 静默处理任何解析错误
                }
            }
            
            parsedMatchText = sb.ToString();
        }
        
        /// <summary>
        /// 使用 RimTalk 的 ScribanParser.Render 进行解析
        /// ⭐ logErrors = false 以静默模式运行
        /// ⭐ isPawnProperty = true 时使用 Pawn 对象解析
        /// ⭐ isPawnProperty = false 时使用 PromptManager.LastContext 解析（包含 prompt 等上下文变量）
        /// </summary>
        private string RenderWithScriban(string template, Pawn pawn, Pawn recipient, bool isPawnProperty = true)
        {
            if (_scribanParserType == null || _renderMethod == null)
                return template;
            
            try
            {
                object ctx;
                
                if (isPawnProperty)
                {
                    // Pawn 属性变量：创建新的 PromptContext
                    if (_promptContextType == null)
                        return template;
                    
                    ctx = Activator.CreateInstance(_promptContextType, new object[] { pawn, null });
                    
                    if (ctx == null)
                        return template;
                    
                    // 设置 AllPawns 列表（用于 recipient 访问）
                    if (recipient != null)
                    {
                        var allPawnsProperty = _promptContextType.GetProperty("AllPawns");
                        if (allPawnsProperty != null)
                        {
                            var pawnsList = new List<Pawn> { pawn, recipient };
                            allPawnsProperty.SetValue(ctx, pawnsList);
                        }
                    }
                    
                    // 设置 IsPreview = true 以获得预览模式的行为
                    var isPreviewProperty = _promptContextType.GetProperty("IsPreview");
                    if (isPreviewProperty != null)
                    {
                        isPreviewProperty.SetValue(ctx, true);
                    }
                }
                else
                {
                    // 上下文变量（如 prompt）：使用 PromptManager.LastContext
                    if (_lastContextProperty == null)
                        return template;
                    
                    ctx = _lastContextProperty.GetValue(null);
                    
                    if (ctx == null)
                    {
                        // 没有上次对话的上下文，返回提示信息
                        return "RimTalk_Preview_NoContext".Translate();
                    }
                    
                    // 设置 IsPreview = true
                    var isPreviewProperty = _promptContextType?.GetProperty("IsPreview");
                    if (isPreviewProperty != null)
                    {
                        isPreviewProperty.SetValue(ctx, true);
                    }
                }
                
                // 调用 ScribanParser.Render(template, ctx, logErrors: false)
                object result = _renderMethod.Invoke(null, new object[] { template, ctx, false });
                return result as string ?? template;
            }
            catch
            {
                return template;
            }
        }
        
        /// <summary>
        /// 刷新匹配的常识列表
        /// </summary>
        private void RefreshMatchedKnowledge()
        {
            matchedKnowledge.Clear();
            
            if (selectedPawn == null || string.IsNullOrEmpty(parsedMatchText))
                return;
            
            var memoryManager = Find.World?.GetComponent<MemoryManager>();
            if (memoryManager?.CommonKnowledge == null)
                return;
            
            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings == null)
                return;
            
            try
            {
                List<KnowledgeScore> scores;
                memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
                    parsedMatchText,
                    settings.maxInjectedKnowledge,
                    out scores,
                    selectedPawn,
                    targetPawn
                );
                
                if (scores != null)
                {
                    matchedKnowledge = scores;
                }
            }
            catch
            {
                // 静默处理错误
            }
        }
        
        /// <summary>
        /// 刷新记忆预览
        /// </summary>
        private void RefreshMemoryPreview()
        {
            memoryPreviewText = "";
            cachedMemoryCount = 0;
            
            if (selectedPawn == null)
                return;
            
            var memoryComp = selectedPawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null)
            {
                memoryPreviewText = "RimTalk_Preview_NoMemoryComp".Translate();
                return;
            }
            
            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings == null)
                return;
            
            try
            {
                var sb = new StringBuilder();
                
                // 使用动态注入获取记忆
                if (settings.useDynamicInjection)
                {
                    List<DynamicMemoryInjection.MemoryScore> memoryScores;
                    string memoryInjection = DynamicMemoryInjection.InjectMemoriesWithDetails(
                        memoryComp,
                        parsedMatchText,
                        settings.maxInjectedMemories,
                        out memoryScores
                    );
                    
                    if (memoryScores != null && memoryScores.Count > 0)
                    {
                        cachedMemoryCount = memoryScores.Count;
                        
                        sb.AppendLine("RimTalk_Preview_SelectedMemories".Translate(memoryScores.Count));
                        sb.AppendLine();
                        
                        for (int i = 0; i < memoryScores.Count; i++)
                        {
                            var score = memoryScores[i];
                            var memory = score.Memory;
                            
                            string layerTag = GetLayerTag(memory.layer);
                            
                            sb.AppendLine($"[{i + 1}] {layerTag} {GetTypeTag(memory.type)}");
                            sb.AppendLine($"    " + "RimTalk_Preview_ScoreDetail".Translate(score.TotalScore.ToString("F3")));
                            sb.AppendLine($"    {memory.content}");
                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        sb.AppendLine("RimTalk_Preview_NoMemoriesAboveThreshold".Translate());
                    }
                }
                else
                {
                    // 静态注入模式
                    sb.AppendLine("RimTalk_Preview_StaticMode".Translate());
                    sb.AppendLine();
                    
                    int count = 0;
                    foreach (var memory in memoryComp.EventLogMemories.Take(5))
                    {
                        count++;
                        sb.AppendLine($"[ELS-{count}] {memory.content}");
                    }
                    foreach (var memory in memoryComp.ArchiveMemories.Take(5))
                    {
                        count++;
                        sb.AppendLine($"[CLPA-{count}] {memory.content}");
                    }
                    
                    cachedMemoryCount = count;
                }
                
                memoryPreviewText = sb.ToString();
            }
            catch (Exception ex)
            {
                memoryPreviewText = "RimTalk_Preview_Error".Translate(ex.Message);
            }
        }
        
        private string GetLayerTag(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active: return "[ABM]";
                case MemoryLayer.Situational: return "[SCM]";
                case MemoryLayer.EventLog: return "[ELS]";
                case MemoryLayer.Archive: return "[CLPA]";
                default: return "[???]";
            }
        }
        
        private string GetTypeTag(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Conversation: return "💬";
                case MemoryType.Action: return "⚡";
                default: return "📝";
            }
        }
        
        /// <summary>
        /// 显示使用帮助对话框
        /// </summary>
        private void ShowHelpDialog()
        {
            string helpContent = "RimTalk_Preview_HelpContent".Translate();
            Find.WindowStack.Add(new Dialog_MessageBox(helpContent, "RimTalk_Close".Translate(), null, null, null, null, false, null, null, WindowLayer.Dialog));
        }
        
        #endregion
    }
}
