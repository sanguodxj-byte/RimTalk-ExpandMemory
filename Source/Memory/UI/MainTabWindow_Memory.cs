using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// Main tab window for viewing colonist memories - appears in bottom menu bar
    /// </summary>
    public class MainTabWindow_Memory : MainTabWindow
    {
        private Vector2 scrollPosition = Vector2.zero;
        private MemoryType? filterType = null;
        private Pawn selectedPawn = null;
        
        // 四层记忆显示开关
        private bool showABM = true;  // 超短期
        private bool showSCM = true;  // 短期
        private bool showELS = true;  // 中期
        private bool showCLPA = true; // 长期
        
        private PawnMemoryComp currentMemoryComp = null; // 保存当前的记忆组件引用

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(1010f, 640f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Pawn selection
            Rect pawnSelectRect = new Rect(0f, 0f, inRect.width, 50f);
            DrawPawnSelection(pawnSelectRect);

            if (selectedPawn == null)
            {
                Rect noPawnRect = new Rect(0f, 60f, inRect.width, 100f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                Widgets.Label(noPawnRect, "RimTalk_SelectColonist".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                return;
            }

            var memoryComp = selectedPawn.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null)
            {
                Rect noMemoryRect = new Rect(0f, 60f, inRect.width, 100f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(noMemoryRect, "RimTalk_NoMemoryComponent".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            currentMemoryComp = memoryComp; // 保存引用
            Rect contentRect = new Rect(0f, 60f, inRect.width, inRect.height - 60f);
            DrawMemoryContent(contentRect, memoryComp);
        }

        private void DrawPawnSelection(Rect rect)
        {
            Text.Font = GameFont.Small;
            
            // Get all colonists
            List<Pawn> colonists = Find.CurrentMap?.mapPawns?.FreeColonists?.ToList();
            if (colonists == null || colonists.Count == 0)
            {
                Widgets.Label(rect, "RimTalk_NoColonists".Translate());
                return;
            }

            // Dropdown for colonist selection
            Rect labelRect = new Rect(rect.x, rect.y, 150f, rect.height);
            Widgets.Label(labelRect, "RimTalk_SelectColonist".Translate() + ":");

            Rect buttonRect = new Rect(rect.x + 160f, rect.y, 300f, 35f);
            string buttonLabel = selectedPawn != null 
                ? selectedPawn.LabelShort 
                : (string)"RimTalk_ChooseColonist".Translate();
            
            if (Widgets.ButtonText(buttonRect, buttonLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var pawn in colonists)
                {
                    Pawn p = pawn; // Capture for lambda
                    options.Add(new FloatMenuOption(pawn.LabelShort, delegate { selectedPawn = p; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // 立即总结按钮（SCM → ELS）
            if (selectedPawn != null)
            {
                Rect summarizeButtonRect = new Rect(rect.x + 470f, rect.y, 200f, 35f);
                string summarizeLabel = "⚡ 立即总结 (SCM→ELS)";
                
                var memoryComp = selectedPawn.TryGetComp<PawnMemoryComp>();
                bool canSummarize = memoryComp != null && memoryComp.GetSituationalMemoryCount() > 0;
                
                if (!canSummarize)
                {
                    GUI.color = Color.gray;
                    summarizeLabel = "立即总结 (无SCM记忆)";
                }
                
                if (Widgets.ButtonText(summarizeButtonRect, summarizeLabel))
                {
                    if (canSummarize)
                    {
                        Log.Message($"[RimTalk Memory] 🔄 Manual summarization triggered for {selectedPawn.LabelShort}");
                        memoryComp.DailySummarization();
                        Messages.Message($"{selectedPawn.LabelShort} 的短期记忆已总结到中期记忆", MessageTypeDefOf.TaskCompletion);
                    }
                }
                
                GUI.color = Color.white;
                
                // 总结所有人按钮
                Rect summarizeAllButtonRect = new Rect(rect.x + 680f, rect.y, 160f, 35f);
                if (Widgets.ButtonText(summarizeAllButtonRect, "⚡⚡ 总结所有殖民者"))
                {
                    int count = 0;
                    foreach (var map in Find.Maps)
                    {
                        foreach (var pawn in map.mapPawns.FreeColonists)
                        {
                            var comp = pawn.TryGetComp<PawnMemoryComp>();
                            if (comp != null && comp.GetSituationalMemoryCount() > 0)
                            {
                                comp.DailySummarization();
                                count++;
                            }
                        }
                    }
                    
                    Log.Message($"[RimTalk Memory] 🔄 Manual summarization triggered for {count} colonists");
                    Messages.Message($"已为 {count} 名殖民者进行记忆总结", MessageTypeDefOf.TaskCompletion);
                }
            }

            // Auto-select if only one colonist or none selected
            if (selectedPawn == null && colonists.Count > 0)
            {
                selectedPawn = colonists[0];
            }
        }

        private void DrawMemoryContent(Rect rect, PawnMemoryComp memoryComp)
        {
            GUI.BeginGroup(rect);

            // Header
            Rect headerRect = new Rect(0f, 0f, rect.width, 40f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, TranslatorFormattedStringExtensions.Translate("RimTalk_MemoryTitle", selectedPawn.LabelShort));
            Text.Font = GameFont.Small;

            // Filter buttons
            Rect filterRect = new Rect(0f, 45f, rect.width, 30f);
            DrawFilterButtons(filterRect);

            // Memory type toggle buttons (短期/长期切换)
            Rect toggleRect = new Rect(0f, 80f, rect.width, 30f);
            DrawMemoryTypeToggles(toggleRect);

            // Memory stats
            Rect statsRect = new Rect(0f, 115f, rect.width, 40f);
            DrawMemoryStats(statsRect, memoryComp);

            // Memory list
            Rect listRect = new Rect(0f, 160f, rect.width, rect.height - 160f);
            DrawMemoryList(listRect, memoryComp);

            GUI.EndGroup();
        }

        private void DrawFilterButtons(Rect rect)
        {
            // 只显示实际使用的类型：Conversation, Interaction, Action
            var types = new List<MemoryType>
            {
                MemoryType.Conversation,
                MemoryType.Interaction,
                MemoryType.Action
            };
            
            int totalButtons = types.Count + 1; // include "All"
            float buttonWidth = rect.width / totalButtons;

            // All button
            Rect allRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(allRect, "RimTalk_Filter_All".Translate()))
            {
                filterType = null;
            }

            // Specific types
            for (int i = 0; i < types.Count; i++)
            {
                MemoryType type = types[i];
                Rect buttonRect = new Rect(rect.x + buttonWidth * (i + 1), rect.y, buttonWidth, rect.height);
                string buttonLabel = ("RimTalk_Filter_" + type.ToString()).Translate();
                if (Widgets.ButtonText(buttonRect, buttonLabel))
                {
                    filterType = type;
                }
            }
        }

        private void DrawMemoryTypeToggles(Rect rect)
        {
            float buttonWidth = rect.width / 4f; // 4个按钮
            float spacing = 2f;
            
            // ABM 按钮（超短期）
            Rect abmRect = new Rect(rect.x, rect.y, buttonWidth - spacing, rect.height);
            string abmLabel = "ABM" + (showABM ? " ✓" : "");
            if (Widgets.ButtonText(abmRect, abmLabel))
            {
                showABM = !showABM;
            }
            if (Mouse.IsOver(abmRect))
            {
                TooltipHandler.TipRegion(abmRect, "超短期记忆 (Active Buffer Memory)\n当前对话上下文");
            }
            
            // SCM 按钮（短期）
            Rect scmRect = new Rect(rect.x + buttonWidth, rect.y, buttonWidth - spacing, rect.height);
            string scmLabel = "SCM" + (showSCM ? " ✓" : "");
            if (Widgets.ButtonText(scmRect, scmLabel))
            {
                showSCM = !showSCM;
            }
            if (Mouse.IsOver(scmRect))
            {
                TooltipHandler.TipRegion(scmRect, "短期记忆 (Situational Context Memory)\n最近几天的事件和互动");
            }
            
            // ELS 按钮（中期）
            Rect elsRect = new Rect(rect.x + buttonWidth * 2, rect.y, buttonWidth - spacing, rect.height);
            string elsLabel = "ELS" + (showELS ? " ✓" : "");
            if (Widgets.ButtonText(elsRect, elsLabel))
            {
                showELS = !showELS;
            }
            if (Mouse.IsOver(elsRect))
            {
                TooltipHandler.TipRegion(elsRect, "中期记忆 (Event Log Summary)\nAI总结的阶段性事件");
            }
            
            // CLPA 按钮（长期）
            Rect clpaRect = new Rect(rect.x + buttonWidth * 3, rect.y, buttonWidth - spacing, rect.height);
            string clpaLabel = "CLPA" + (showCLPA ? " ✓" : "");
            if (Widgets.ButtonText(clpaRect, clpaLabel))
            {
                showCLPA = !showCLPA;
            }
            if (Mouse.IsOver(clpaRect))
            {
                TooltipHandler.TipRegion(clpaRect, "长期记忆 (Colony Lore & Persona Archive)\n核心人设和重要里程碑");
            }
        }

        private void DrawMemoryStats(Rect rect, PawnMemoryComp memoryComp)
        {
            // 获取四层记忆组件
            FourLayerMemoryComp fourLayerComp = memoryComp as FourLayerMemoryComp;
            
            if (fourLayerComp != null)
            {
                // 四层记忆统计
                Text.Anchor = TextAnchor.MiddleLeft;
                
                string stats = string.Format(
                    "ABM: {0}/3 | SCM: {1}/20 | ELS: {2}/50 | CLPA: {3}",
                    fourLayerComp.ActiveMemories.Count,
                    fourLayerComp.SituationalMemories.Count,
                    fourLayerComp.EventLogMemories.Count,
                    fourLayerComp.ArchiveMemories.Count
                );
                
                Widgets.Label(rect, stats);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                // 兼容旧系统
                Text.Anchor = TextAnchor.MiddleLeft;
                
                string stats = string.Format(
                    "RimTalk_MemoryStats".Translate(),
                    memoryComp.ShortTermMemories.Count.ToString(),
                    RimTalkMemoryPatchMod.Settings.maxShortTermMemories.ToString(),
                    memoryComp.LongTermMemories.Count.ToString(),
                    RimTalkMemoryPatchMod.Settings.maxLongTermMemories.ToString()
                );
                
                Widgets.Label(rect, stats);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        /// <summary>
        /// 显示清除确认对话框
        /// </summary>
        private void ShowClearConfirmation(PawnMemoryComp memoryComp, ClearType clearType)
        {
            string title = "";
            string message = "";
            
            switch (clearType)
            {
                case ClearType.All:
                    title = "确认清除全部记忆";
                    message = $"确定要清除 {selectedPawn.LabelShort} 的所有记忆吗？\n\n" +
                             $"这将删除 {memoryComp.ShortTermMemories.Count} 条短期记忆和 " +
                             $"{memoryComp.LongTermMemories.Count} 条长期记忆。\n\n" +
                             "此操作不可撤销！";
                    break;
                    
                case ClearType.ShortTerm:
                    title = "确认清除短期记忆";
                    message = $"确定要清除 {selectedPawn.LabelShort} 的短期记忆吗？\n\n" +
                             $"这将删除 {memoryComp.ShortTermMemories.Count} 条短期记忆。\n\n" +
                             "此操作不可撤销！";
                    break;
                    
                case ClearType.LongTerm:
                    title = "确认清除长期记忆";
                    message = $"确定要清除 {selectedPawn.LabelShort} 的长期记忆吗？\n\n" +
                             $"这将删除 {memoryComp.LongTermMemories.Count} 条长期记忆。\n\n" +
                             "此操作不可撤销！";
                    break;
            }
            
            Dialog_MessageBox confirmDialog = Dialog_MessageBox.CreateConfirmation(
                message,
                delegate
                {
                    // 确认清除
                    switch (clearType)
                    {
                        case ClearType.All:
                            memoryComp.ClearAllMemories();
                            Messages.Message($"已清除 {selectedPawn.LabelShort} 的所有记忆", MessageTypeDefOf.TaskCompletion);
                            break;
                            
                        case ClearType.ShortTerm:
                            memoryComp.ClearShortTermMemories();
                            Messages.Message($"已清除 {selectedPawn.LabelShort} 的短期记忆", MessageTypeDefOf.TaskCompletion);
                            break;
                            
                        case ClearType.LongTerm:
                            memoryComp.ClearLongTermMemories();
                            Messages.Message($"已清除 {selectedPawn.LabelShort} 的长期记忆", MessageTypeDefOf.TaskCompletion);
                            break;
                    }
                },
                true,
                title
            );
            
            Find.WindowStack.Add(confirmDialog);
        }

        private enum ClearType
        {
            All,
            ShortTerm,
            LongTerm
        }

        private void DrawMemoryList(Rect rect, PawnMemoryComp memoryComp)
        {
            List<MemoryListEntry> allMemories = new List<MemoryListEntry>();
            
            // 获取四层记忆组件
            FourLayerMemoryComp fourLayerComp = memoryComp as FourLayerMemoryComp;
            
            if (fourLayerComp != null)
            {
                // 四层架构显示
                if (showABM)
                {
                    foreach (var memory in fourLayerComp.ActiveMemories)
                    {
                        if (filterType == null || memory.type == filterType.Value)
                        {
                            allMemories.Add(new MemoryListEntry { memory = memory, layer = MemoryLayer.Active });
                        }
                    }
                }
                
                if (showSCM)
                {
                    foreach (var memory in fourLayerComp.SituationalMemories)
                    {
                        if (filterType == null || memory.type == filterType.Value)
                        {
                            allMemories.Add(new MemoryListEntry { memory = memory, layer = MemoryLayer.Situational });
                        }
                    }
                }
                
                if (showELS)
                {
                    foreach (var memory in fourLayerComp.EventLogMemories)
                    {
                        if (filterType == null || memory.type == filterType.Value)
                        {
                            allMemories.Add(new MemoryListEntry { memory = memory, layer = MemoryLayer.EventLog });
                        }
                    }
                }
                
                if (showCLPA)
                {
                    foreach (var memory in fourLayerComp.ArchiveMemories)
                    {
                        if (filterType == null || memory.type == filterType.Value)
                        {
                            allMemories.Add(new MemoryListEntry { memory = memory, layer = MemoryLayer.Archive });
                        }
                    }
                }
            }
            else
            {
                // 兼容旧系统（映射到四层）
                if (showABM || showSCM)
                {
                    foreach (var memory in memoryComp.ShortTermMemories)
                    {
                        if (filterType == null || memory.type == filterType.Value)
                        {
                            allMemories.Add(new MemoryListEntry { memory = memory, layer = MemoryLayer.Situational });
                        }
                    }
                }
                
                if (showELS || showCLPA)
                {
                    foreach (var memory in memoryComp.LongTermMemories)
                    {
                        if (filterType == null || memory.type == filterType.Value)
                        {
                            allMemories.Add(new MemoryListEntry { memory = memory, layer = MemoryLayer.Archive });
                        }
                    }
                }
            }

            // 动态行高
            float totalHeight = 0f;
            var rowHeights = new List<float>();
            foreach (var entry in allMemories)
            {
                float height = GetRowHeight(entry.layer);
                rowHeights.Add(height);
                totalHeight += height;
            }
            
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, totalHeight);
            
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;
            for (int i = 0; i < allMemories.Count; i++)
            {
                var entry = allMemories[i];
                float height = rowHeights[i];
                Rect rowRect = new Rect(0f, y, viewRect.width, height - 5f);
                DrawMemoryRow(rowRect, entry.memory, entry.layer);
                y += height;
            }

            Widgets.EndScrollView();
        }

        private float GetRowHeight(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 70f;  // ABM 最紧凑
                case MemoryLayer.Situational:
                    return 85f;  // SCM 略大
                case MemoryLayer.EventLog:
                    return 110f; // ELS 中等（AI总结内容较长）
                case MemoryLayer.Archive:
                    return 130f; // CLPA 最大（重要内容）
                default:
                    return 85f;
            }
        }

        private void DrawMemoryRow(Rect rect, MemoryEntry memory, MemoryLayer layer)
        {
            // Background - 根据层级使用不同颜色
            Color bgColor;
            switch (layer)
            {
                case MemoryLayer.Active:
                    bgColor = new Color(0.3f, 0.4f, 0.5f, 0.5f); // ABM: 亮蓝色
                    break;
                case MemoryLayer.Situational:
                    bgColor = new Color(0.2f, 0.3f, 0.4f, 0.5f); // SCM: 中蓝色
                    break;
                case MemoryLayer.EventLog:
                    bgColor = new Color(0.3f, 0.25f, 0.2f, 0.5f); // ELS: 棕色
                    break;
                case MemoryLayer.Archive:
                    bgColor = new Color(0.25f, 0.2f, 0.25f, 0.5f); // CLPA: 紫色
                    break;
                default:
                    bgColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    break;
            }
            
            // 如果被固定，使用金色
            if (memory.isPinned)
            {
                bgColor = new Color(0.3f, 0.25f, 0.1f, 0.5f);
            }
            
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect);

            Rect innerRect = rect.ContractedBy(5f);

            // 按钮区域（右侧）
            float buttonWidth = 50f;
            float buttonSpacing = 5f;
            float buttonsStartX = innerRect.xMax - (buttonWidth * 3 + buttonSpacing * 2);
            float buttonY = innerRect.y;
            
            // 编辑按钮
            Rect editButtonRect = new Rect(buttonsStartX, buttonY, buttonWidth, 25f);
            if (Widgets.ButtonText(editButtonRect, "编辑"))
            {
                Find.WindowStack.Add(new Dialog_EditMemory(memory, currentMemoryComp as FourLayerMemoryComp));
            }
            TooltipHandler.TipRegion(editButtonRect, "编辑此记忆的内容、标签和备注");

            // 固定按钮
            Rect pinButtonRect = new Rect(buttonsStartX + buttonWidth + buttonSpacing, buttonY, buttonWidth, 25f);
            string pinLabel = memory.isPinned ? "已固定" : "固定";
            if (Widgets.ButtonText(pinButtonRect, pinLabel))
            {
                memory.isPinned = !memory.isPinned;
                if (currentMemoryComp is FourLayerMemoryComp fourLayerComp)
                {
                    fourLayerComp.PinMemory(memory.id, memory.isPinned);
                }
            }
            string pinTooltip = memory.isPinned ? "取消固定此记忆" : "固定此记忆（不会被删除或衰减）";
            TooltipHandler.TipRegion(pinButtonRect, pinTooltip);

            // 删除按钮
            Rect deleteButtonRect = new Rect(buttonsStartX + (buttonWidth + buttonSpacing) * 2, buttonY, buttonWidth, 25f);
            if (Widgets.ButtonText(deleteButtonRect, "删除"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "确认删除此记忆？",
                    delegate
                    {
                        if (currentMemoryComp is FourLayerMemoryComp fourLayerComp)
                        {
                            fourLayerComp.DeleteMemory(memory.id);
                            Messages.Message("记忆已删除", MessageTypeDefOf.TaskCompletion);
                        }
                    },
                    true
                ));
            }
            TooltipHandler.TipRegion(deleteButtonRect, "永久删除此记忆");

            // Type, time, and layer indicator
            Rect headerRect = new Rect(innerRect.x, innerRect.y, buttonsStartX - innerRect.x - 10f, 20f);
            Text.Font = GameFont.Tiny;
            
            string memoryTypeLabel = ("RimTalk_MemoryType_" + memory.type.ToString()).Translate();
            
            // 显示层级信息（使用缩写）
            string layerLabel = "";
            switch (layer)
            {
                case MemoryLayer.Active:
                    layerLabel = "[ABM]";
                    break;
                case MemoryLayer.Situational:
                    layerLabel = "[SCM]";
                    break;
                case MemoryLayer.EventLog:
                    layerLabel = "[ELS]";
                    break;
                case MemoryLayer.Archive:
                    layerLabel = "[CLPA]";
                    break;
            }
            
            string header = $"{layerLabel} [{memoryTypeLabel}] {memory.TimeAgoString}";
            if (!string.IsNullOrEmpty(memory.relatedPawnName))
                header += " - " + "RimTalk_With".Translate() + " " + memory.relatedPawnName;
            
            // 如果有标签，显示
            if (memory.tags != null && memory.tags.Any())
            {
                header += $" | 标签: {string.Join(", ", memory.tags.Take(2))}";
                if (memory.tags.Count > 2)
                    header += "...";
            }
            
            Widgets.Label(headerRect, header);

            // Content - 根据层级决定显示长度
            Text.Font = GameFont.Small;
            
            int maxLength = GetContentMaxLength(layer);
            string displayText = memory.content;
            bool isTruncated = false;
            
            if (displayText.Length > maxLength)
            {
                displayText = displayText.Substring(0, maxLength - 3) + "...";
                isTruncated = true;
            }
            
            float contentHeight = GetContentHeight(layer);
            Rect contentRect = new Rect(innerRect.x, innerRect.y + 27f, innerRect.width, contentHeight);
            
            // 使用 TextArea 以支持多行显示
            GUI.enabled = false; // 只读
            Text.Font = GetContentFont(layer);
            displayText = GUI.TextArea(contentRect, displayText);
            GUI.enabled = true;
            Text.Font = GameFont.Small;
            
            // 完整内容 Tooltip
            if (isTruncated || memory.content.Length > maxLength)
            {
                Rect tooltipRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentHeight);
                if (Mouse.IsOver(tooltipRect))
                {
                    // 创建多行 tooltip，包含备注
                    string tooltipText = memory.content;
                    if (!string.IsNullOrEmpty(memory.notes))
                    {
                        tooltipText += "\n\n备注: " + memory.notes;
                    }
                    TooltipHandler.TipRegion(tooltipRect, tooltipText);
                }
            }

            // Importance and Activity bars
            float barY = innerRect.y + 27f + contentHeight + 2f;
            
            // Importance bar
            Rect importanceRect = new Rect(innerRect.x, barY, innerRect.width / 2 - 2f, 8f);
            Widgets.FillableBar(importanceRect, Mathf.Clamp01(memory.importance), 
                Texture2D.whiteTexture, BaseContent.ClearTex, false);
            TooltipHandler.TipRegion(importanceRect, $"重要性: {memory.importance:F2}");
            
            // Activity bar
            Rect activityRect = new Rect(innerRect.x + innerRect.width / 2 + 2f, barY, innerRect.width / 2 - 2f, 8f);
            Widgets.FillableBar(activityRect, Mathf.Clamp01(memory.activity), 
                Texture2D.whiteTexture, BaseContent.ClearTex, false);
            TooltipHandler.TipRegion(activityRect, $"活跃度: {memory.activity:F2}");
            
            Text.Font = GameFont.Small;
        }

        private int GetContentMaxLength(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 60;   // ABM: 最短
                case MemoryLayer.Situational:
                    return 100;  // SCM
                case MemoryLayer.EventLog:
                    return 200;  // ELS: AI总结
                case MemoryLayer.Archive:
                    return 300;  // CLPA: 最长
                default:
                    return 100;
            }
        }

        private float GetContentHeight(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 30f;
                case MemoryLayer.Situational:
                    return 45f;
                case MemoryLayer.EventLog:
                    return 70f;
                case MemoryLayer.Archive:
                    return 90f;
                default:
                    return 45f;
            }
        }

        private GameFont GetContentFont(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                case MemoryLayer.Situational:
                    return GameFont.Small;
                case MemoryLayer.EventLog:
                case MemoryLayer.Archive:
                    return GameFont.Tiny; // 较小字体以显示更多内容
                default:
                    return GameFont.Small;
            }
        }

        private class MemoryListEntry
        {
            public MemoryEntry memory;
            public MemoryLayer layer;
        }
    }
}
