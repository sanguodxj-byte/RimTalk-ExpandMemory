using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.MemoryPatch;
using System;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// Mind Stream Timeline - Multi-Select Memory Cards
    /// ★ v3.3.19: 完全重构 - 时间线卡片布局 + 拖拽多选 + 批量操作
    /// </summary>
    public class MainTabWindow_Memory : MainTabWindow
    {
        // ==================== Data & State ====================
        private Pawn selectedPawn = null;
        private FourLayerMemoryComp currentMemoryComp = null;
        
        // ⭐ 新增：显示所有类人生物选项
        private bool showAllHumanlikes = false;
        
        // Multi-select support
        private HashSet<MemoryEntry> selectedMemories = new HashSet<MemoryEntry>();
        private MemoryEntry lastSelectedMemory = null;
        
        // Drag selection
        private bool isDragging = false;
        private Vector2 dragStartPos = Vector2.zero;
        private Vector2 dragCurrentPos = Vector2.zero;
        
        // UI State
        private Vector2 timelineScrollPosition = Vector2.zero;
        private MemoryType? filterType = null;
        
        // Layer filters
        private bool showABM = true;
        private bool showSCM = true;
        private bool showELS = true;
        private bool showCLPA = true;
        
        // Layout constants
        private const float TOP_BAR_HEIGHT = 50f;
        private const float CONTROL_PANEL_WIDTH = 220f;
        private const float SPACING = 10f;
        private const float CARD_WIDTH_FULL = 600f;
        private const float CARD_SPACING = 8f;
        
        public override Vector2 RequestedTabSize => new Vector2(1200f, 700f);

        // ==================== Main Layout ====================
        
        public override void DoWindowContents(Rect inRect)
        {
            // Top Bar
            Rect topBarRect = new Rect(0f, 0f, inRect.width, TOP_BAR_HEIGHT);
            DrawTopBar(topBarRect);
            
            // Content area
            float contentY = TOP_BAR_HEIGHT + SPACING;
            float contentHeight = inRect.height - contentY;
            
            if (selectedPawn == null)
            {
                DrawNoPawnSelected(new Rect(0f, contentY, inRect.width, contentHeight));
                return;
            }
            
            var memoryComp = selectedPawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null)
            {
                DrawNoMemoryComponent(new Rect(0f, contentY, inRect.width, contentHeight));
                return;
            }
            
            currentMemoryComp = memoryComp;
            
            // Left Control Panel
            Rect controlPanelRect = new Rect(0f, contentY, CONTROL_PANEL_WIDTH, contentHeight);
            DrawControlPanel(controlPanelRect);
            
            // Right Timeline
            float timelineX = CONTROL_PANEL_WIDTH + SPACING;
            float timelineWidth = inRect.width - timelineX;
            Rect timelineRect = new Rect(timelineX, contentY, timelineWidth, contentHeight);
            DrawTimeline(timelineRect);
            
            // Handle drag end
            if (Event.current.type == EventType.MouseUp && isDragging)
            {
                isDragging = false;
                Event.current.Use();
            }
        }

        // ==================== Top Bar ====================
        
        private void DrawTopBar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(5f);
            
            // Pawn Selector
            Rect pawnSelectorRect = new Rect(innerRect.x, innerRect.y + 5f, 250f, 35f);
            DrawPawnSelector(pawnSelectorRect);
            
            // ⭐ Show All Humanlikes Checkbox
            Rect checkboxRect = new Rect(innerRect.x + 260f, innerRect.y + 10f, 180f, 25f);
            Widgets.CheckboxLabeled(checkboxRect, "RimTalk_ShowAllHumanlikes".Translate(), ref showAllHumanlikes);
            
            // ⭐ 统计信息栏（移到这里，替换掉总记忆数）
            if (currentMemoryComp != null)
            {
                Rect statsRect = new Rect(innerRect.x + 450f, innerRect.y + 8f, 350f, 30f);
                DrawTopBarStats(statsRect);
            }
            
            // Buttons (right side)
            float buttonWidth = 120f;
            float spacing = 5f;
            float rightX = innerRect.xMax;
            
            // Preview button
            rightX -= buttonWidth;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, 35f), "RimTalk_MindStream_Preview".Translate()))
            {
                Find.WindowStack.Add(new RimTalk.Memory.Debug.Dialog_InjectionPreview());
            }
            
            // Common Knowledge button
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, 35f), "RimTalk_MindStream_Knowledge".Translate()))
            {
                OpenCommonKnowledgeDialog();
            }
        }
        
        // ⭐ 新增：TopBar统计信息显示
        private void DrawTopBarStats(Rect rect)
        {
            if (currentMemoryComp == null)
                return;
            
            int abmCount = currentMemoryComp.ActiveMemories.Count;
            int scmCount = currentMemoryComp.SituationalMemories.Count;
            int elsCount = currentMemoryComp.EventLogMemories.Count;
            int clpaCount = currentMemoryComp.ArchiveMemories.Count;
            
            Text.Font = GameFont.Small;
            
            // 只显示层级统计（居中显示）
            string stats = $"ABM: {abmCount}  SCM: {scmCount}  ELS: {elsCount}  CLPA: {clpaCount}";
            GUI.color = new Color(0.7f, 0.9f, 1f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, rect.height), stats);
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
        
        private void DrawPawnSelector(Rect rect)
        {
            // ⭐ 根据showAllHumanlikes决定显示哪些Pawn
            List<Pawn> colonists;
            if (showAllHumanlikes)
            {
                // 显示所有类人生物
                colonists = Find.CurrentMap?.mapPawns?.AllPawnsSpawned
                    ?.Where(p => p.RaceProps.Humanlike)
                    ?.ToList();
            }
            else
            {
                // 只显示殖民者
                colonists = Find.CurrentMap?.mapPawns?.FreeColonists?.ToList();
            }
            
            if (colonists == null || colonists.Count == 0)
            {
                Widgets.Label(rect, "RimTalk_MindStream_NoColonists".Translate());
                return;
            }
            
            string label = selectedPawn != null ? selectedPawn.LabelShort : "RimTalk_SelectColonist".Translate().ToString();
            
            if (Widgets.ButtonText(rect, label))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var pawn in colonists)
                {
                    Pawn p = pawn;
                    string pawnLabel = p.LabelShort;
                    
                    // 如果是非殖民者，添加标识
                    if (!p.IsColonist)
                    {
                        if (p.Faction != null && p.Faction != Faction.OfPlayer)
                        {
                            pawnLabel += $" ({p.Faction.Name})";
                        }
                        else if (p.IsPrisoner)
                        {
                            pawnLabel += " (Prisoner)";
                        }
                        else if (p.IsSlaveOfColony)
                        {
                            pawnLabel += " (Slave)";
                        }
                    }
                    
                    options.Add(new FloatMenuOption(pawnLabel, delegate 
                    { 
                        selectedPawn = p;
                        selectedMemories.Clear(); // Clear selection when changing pawn
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // Auto-select
            if (selectedPawn == null && colonists.Count > 0)
            {
                selectedPawn = colonists[0];
            }
        }

        // ==================== Control Panel ====================
        
        private void DrawControlPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(SPACING);
            float y = innerRect.y;
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 30f), "RimTalk_MindStream_MemoryFilters".Translate());
            Text.Font = GameFont.Small;
            y += 35f;
            
            // Layer Filters
            y = DrawLayerFilters(innerRect, y);
            y += 10f;
            
            // Type Filters
            y = DrawTypeFilters(innerRect, y);
            y += 10f;
            
            // ⭐ 移除了 DrawStatistics 调用，统计已移到TopBar
            
            // Separator
            Widgets.DrawLineHorizontal(innerRect.x, y, innerRect.width);
            y += 15f;
            
            // Batch Actions
            y = DrawBatchActions(innerRect, y);
            y += 10f;
            
            // Global Actions
            DrawGlobalActions(innerRect, y);
        }
        
        private float DrawLayerFilters(Rect parentRect, float startY)
        {
            float y = startY;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(parentRect.x, y, parentRect.width, 20f), "RimTalk_MindStream_Layers".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 22f;
            
            float checkboxHeight = 24f;
            
            // ABM
            Rect abmRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color abmColor = new Color(0.3f, 0.8f, 1f); // Cyan
            DrawColoredCheckbox(abmRect, "RimTalk_MindStream_ABM".Translate(), ref showABM, abmColor);
            y += checkboxHeight + 2f;
            
            // SCM
            Rect scmRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color scmColor = new Color(0.3f, 1f, 0.5f); // Green
            DrawColoredCheckbox(scmRect, "RimTalk_MindStream_SCM".Translate(), ref showSCM, scmColor);
            y += checkboxHeight + 2f;
            
            // ELS
            Rect elsRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color elsColor = new Color(1f, 0.8f, 0.3f); // Yellow
            DrawColoredCheckbox(elsRect, "RimTalk_MindStream_ELS".Translate(), ref showELS, elsColor);
            y += checkboxHeight + 2f;
            
            // CLPA
            Rect clpaRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color clpaColor = new Color(0.8f, 0.4f, 1f); // Purple
            DrawColoredCheckbox(clpaRect, "RimTalk_MindStream_CLPA".Translate(), ref showCLPA, clpaColor);
            y += checkboxHeight;
            
            return y;
        }
        
        private void DrawColoredCheckbox(Rect rect, string label, ref bool value, Color color)
        {
            // Colored indicator
            Rect colorRect = new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f);
            Widgets.DrawBoxSolid(colorRect, color);
            
            // Checkbox
            Rect checkboxRect = new Rect(rect.x + 8f, rect.y, rect.width - 8f, rect.height);
            Widgets.CheckboxLabeled(checkboxRect, label, ref value);
        }
        
        private float DrawTypeFilters(Rect parentRect, float startY)
        {
            float y = startY;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(parentRect.x, y, parentRect.width, 20f), "RimTalk_MindStream_Type".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 22f;
            
            float buttonHeight = 28f;
            float spacing = 2f;
            
            // All
            bool isAllSelected = filterType == null;
            if (isAllSelected)
                GUI.color = new Color(0.5f, 0.7f, 1f);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_All".Translate()))
            {
                filterType = null;
                selectedMemories.Clear();
            }
            GUI.color = Color.white;
            y += buttonHeight + spacing;
            
            // Conversation
            bool isConvSelected = filterType == MemoryType.Conversation;
            if (isConvSelected)
                GUI.color = new Color(0.5f, 0.7f, 1f);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_Conversation".Translate()))
            {
                filterType = MemoryType.Conversation;
                selectedMemories.Clear();
            }
            GUI.color = Color.white;
            y += buttonHeight + spacing;
            
            // Action
            bool isActionSelected = filterType == MemoryType.Action;
            if (isActionSelected)
                GUI.color = new Color(0.5f, 0.7f, 1f);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_Action".Translate()))
            {
                filterType = MemoryType.Action;
                selectedMemories.Clear();
            }
            GUI.color = Color.white;
            y += buttonHeight;
            
            return y;
        }
        
        private float DrawBatchActions(Rect parentRect, float startY)
        {
            float y = startY;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(parentRect.x, y, parentRect.width, 20f), "RimTalk_MindStream_BatchActions".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 22f;
            
            float buttonHeight = 32f;
            float spacing = 5f;
            bool hasSelection = selectedMemories.Count > 0;
            
            // Summarize Selected (SCM -> ELS)
            GUI.enabled = hasSelection && selectedMemories.Any(m => m.layer == MemoryLayer.Situational);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), 
                hasSelection ? "RimTalk_MindStream_SummarizeN".Translate(selectedMemories.Count) : "RimTalk_MindStream_SummarizeSelected".Translate()))
            {
                SummarizeSelectedMemories();
            }
            GUI.enabled = true;
            y += buttonHeight + spacing;
            
            // Archive Selected (ELS -> CLPA)
            GUI.enabled = hasSelection && selectedMemories.Any(m => m.layer == MemoryLayer.EventLog);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), 
                hasSelection ? "RimTalk_MindStream_ArchiveN".Translate(selectedMemories.Count) : "RimTalk_MindStream_ArchiveSelected".Translate()))
            {
                ArchiveSelectedMemories();
            }
            GUI.enabled = true;
            y += buttonHeight + spacing;
            
            // Delete Selected
            GUI.enabled = hasSelection;
            GUI.color = hasSelection ? new Color(1f, 0.4f, 0.4f) : Color.white;
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), 
                hasSelection ? "RimTalk_MindStream_DeleteN".Translate(selectedMemories.Count) : "RimTalk_MindStream_DeleteSelected".Translate()))
            {
                DeleteSelectedMemories();
            }
            GUI.color = Color.white;
            GUI.enabled = true;
            y += buttonHeight;
            
            return y;
        }
        
        private void DrawGlobalActions(Rect parentRect, float startY)
        {
            float y = startY;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(parentRect.x, y, parentRect.width, 20f), "RimTalk_MindStream_GlobalActions".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 22f;
            
            float buttonHeight = 32f;
            float spacing = 5f;
            
            // Summarize All
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_SummarizeAll".Translate()))
            {
                SummarizeAll();
            }
            y += buttonHeight + spacing;
            
            // Archive All
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_ArchiveAll".Translate()))
            {
                ArchiveAll();
            }
        }

        // ==================== Timeline ====================
        
        private void DrawTimeline(Rect rect)
        {
            if (currentMemoryComp == null)
                return;
            
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(5f);
            
            // Get filtered memories
            var memories = GetFilteredMemories();
            
            // Calculate card heights
            float totalHeight = 0f;
            var cardHeights = new List<float>();
            foreach (var memory in memories)
            {
                float height = GetCardHeight(memory.layer);
                cardHeights.Add(height);
                totalHeight += height + CARD_SPACING;
            }
            
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, totalHeight);
            
            // Handle drag selection
            HandleDragSelection(innerRect, viewRect);
            
            // Draw timeline
            Widgets.BeginScrollView(innerRect, ref timelineScrollPosition, viewRect, true);
            
            float y = 0f;
            for (int i = 0; i < memories.Count; i++)
            {
                var memory = memories[i];
                float height = cardHeights[i];
                Rect cardRect = new Rect(0f, y, viewRect.width, height);
                DrawMemoryCard(cardRect, memory);
                y += height + CARD_SPACING;
            }
            
            // Draw selection box
            if (isDragging)
            {
                DrawSelectionBox();
            }
            
            Widgets.EndScrollView();
            
            // Show filter status
            if (filterType != null || !showABM || !showSCM || !showELS || !showCLPA)
            {
                Rect statusRect = new Rect(innerRect.x, innerRect.yMax - 25f, innerRect.width, 20f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(statusRect, "RimTalk_MindStream_ShowingN".Translate(memories.Count));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }
        
        private void DrawMemoryCard(Rect rect, MemoryEntry memory)
        {
            bool isSelected = selectedMemories.Contains(memory);
            Color borderColor = GetLayerColor(memory.layer);
            
            // Background
            if (memory.isPinned)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.2f, 0.1f, 0.5f));
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.9f));
            }
            
            // Border
            if (isSelected)
            {
                Widgets.DrawBox(rect, 2);
                Rect borderRect = rect.ContractedBy(1f);
                GUI.color = new Color(1f, 0.8f, 0.3f);
                Widgets.DrawBox(borderRect, 2);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = borderColor;
                Widgets.DrawBox(rect, 1);
                GUI.color = Color.white;
            }
            
            // Hover highlight
            if (Mouse.IsOver(rect) && !isDragging)
            {
                Widgets.DrawLightHighlight(rect);
            }
            
            Rect innerRect = rect.ContractedBy(8f);
            
            // ⭐ 计算按钮区域（用于检测是否点击在按钮上）
            float buttonSize = 24f;
            float buttonSpacing = 4f;
            float buttonAreaX = innerRect.xMax - (buttonSize * 2 + buttonSpacing + 8f);
            Rect buttonAreaRect = new Rect(buttonAreaX, innerRect.y, buttonSize * 2 + buttonSpacing + 8f, buttonSize);
            bool clickedOnButton = false;
            
            // Top-right action buttons
            float buttonX = innerRect.xMax - buttonSize;
            float buttonY = innerRect.y;
            
            // Pin button
            Rect pinButtonRect = new Rect(buttonX, buttonY, buttonSize, buttonSize);
            if (Mouse.IsOver(pinButtonRect))
            {
                Widgets.DrawHighlight(pinButtonRect);
            }
            if (Widgets.ButtonImage(pinButtonRect, memory.isPinned ? TexButton.ReorderUp : TexButton.ReorderDown))
            {
                memory.isPinned = !memory.isPinned;
                if (currentMemoryComp != null)
                {
                    currentMemoryComp.PinMemory(memory.id, memory.isPinned);
                }
                clickedOnButton = true;
                Event.current.Use();
            }
            TooltipHandler.TipRegion(pinButtonRect, memory.isPinned ? "RimTalk_MindStream_Unpin".Translate() : "RimTalk_MindStream_Pin".Translate());
            buttonX -= buttonSize + buttonSpacing;
            
            // Edit button
            Rect editButtonRect = new Rect(buttonX, buttonY, buttonSize, buttonSize);
            if (Mouse.IsOver(editButtonRect))
            {
                Widgets.DrawHighlight(editButtonRect);
            }
            if (Widgets.ButtonImage(editButtonRect, TexButton.Rename))
            {
                if (currentMemoryComp != null)
                {
                    Find.WindowStack.Add(new Dialog_EditMemory(memory, currentMemoryComp));
                }
                clickedOnButton = true;
                Event.current.Use();
            }
            TooltipHandler.TipRegion(editButtonRect, "RimTalk_MindStream_Edit".Translate());
            
            // ⭐ 只在非按钮区域处理点击选择
            if (!clickedOnButton && Widgets.ButtonInvisible(rect) && !isDragging && !buttonAreaRect.Contains(Event.current.mousePosition))
            {
                HandleMemoryClick(memory);
            }
            
            // Content area (avoid button overlap)
            Rect contentRect = new Rect(innerRect.x, innerRect.y, innerRect.width - (buttonSize * 2 + buttonSpacing + 8f), innerRect.height);
            
            // Header
            Text.Font = GameFont.Tiny;
            string layerLabel = GetLayerLabel(memory.layer);
            string typeLabel = memory.type.ToString();
            string timeLabel = memory.TimeAgoString;
            
            string header = $"[{layerLabel}] {typeLabel} • {timeLabel}";
            if (!string.IsNullOrEmpty(memory.relatedPawnName))
            {
                header += $" • {"RimTalk_MindStream_With".Translate()} {memory.relatedPawnName}";
            }
            
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 18f), header);
            GUI.color = Color.white;
            
            // Content
            Text.Font = GameFont.Small;
            float contentY = contentRect.y + 20f;
            float contentHeight = contentRect.height - 40f;
            Rect textRect = new Rect(contentRect.x, contentY, contentRect.width, contentHeight);
            
            string displayText = memory.content;
            int maxLength = GetContentMaxLength(memory.layer);
            if (displayText.Length > maxLength)
            {
                displayText = displayText.Substring(0, maxLength) + "...";
            }
            
            Widgets.Label(textRect, displayText);
            
            // Tooltip for full content
            if (memory.content.Length > maxLength && Mouse.IsOver(textRect))
            {
                TooltipHandler.TipRegion(textRect, memory.content);
            }
            
            // Footer (importance/activity bars)
            float barY = contentRect.yMax - 12f;
            float barWidth = (contentRect.width - 4f) / 2f;
            
            Rect importanceBarRect = new Rect(contentRect.x, barY, barWidth, 8f);
            Widgets.FillableBar(importanceBarRect, Mathf.Clamp01(memory.importance), Texture2D.whiteTexture, BaseContent.ClearTex, false);
            TooltipHandler.TipRegion(importanceBarRect, "RimTalk_MindStream_ImportanceLabel".Translate(memory.importance.ToString("F2")));
            
            Rect activityBarRect = new Rect(contentRect.x + barWidth + 4f, barY, barWidth, 8f);
            Widgets.FillableBar(activityBarRect, Mathf.Clamp01(memory.activity), Texture2D.whiteTexture, BaseContent.ClearTex, false);
            TooltipHandler.TipRegion(activityBarRect, "RimTalk_MindStream_ActivityLabel".Translate(memory.activity.ToString("F2")));
            
            Text.Font = GameFont.Small;
        }
        
        private void HandleMemoryClick(MemoryEntry memory)
        {
            bool ctrl = Event.current.control;
            bool shift = Event.current.shift;
            
            if (ctrl)
            {
                // Toggle selection
                if (selectedMemories.Contains(memory))
                    selectedMemories.Remove(memory);
                else
                    selectedMemories.Add(memory);
                    
                lastSelectedMemory = memory;
            }
            else if (shift && lastSelectedMemory != null)
            {
                // Range selection
                var filteredMemories = GetFilteredMemories();
                int startIndex = filteredMemories.IndexOf(lastSelectedMemory);
                int endIndex = filteredMemories.IndexOf(memory);
                
                if (startIndex >= 0 && endIndex >= 0)
                {
                    int min = Math.Min(startIndex, endIndex);
                    int max = Math.Max(startIndex, endIndex);
                    
                    for (int i = min; i <= max; i++)
                    {
                        selectedMemories.Add(filteredMemories[i]);
                    }
                }
                
                lastSelectedMemory = memory;
            }
            else
            {
                // Single selection
                selectedMemories.Clear();
                selectedMemories.Add(memory);
                lastSelectedMemory = memory;
            }
        }
        
        private void HandleDragSelection(Rect listRect, Rect viewRect)
        {
            Event e = Event.current;
            
            if (e.type == EventType.MouseDown && e.button == 0 && listRect.Contains(e.mousePosition))
            {
                isDragging = true;
                dragStartPos = e.mousePosition - listRect.position + timelineScrollPosition;
                dragCurrentPos = dragStartPos;
                e.Use();
            }
            
            if (isDragging && e.type == EventType.MouseDrag)
            {
                dragCurrentPos = e.mousePosition - listRect.position + timelineScrollPosition;
                
                // Select memories that intersect with drag box
                Rect selectionBox = GetSelectionBox();
                var filteredMemories = GetFilteredMemories();
                
                bool ctrl = Event.current.control;
                if (!ctrl)
                {
                    selectedMemories.Clear();
                }
                
                float y = 0f;
                foreach (var memory in filteredMemories)
                {
                    float height = GetCardHeight(memory.layer);
                    Rect cardRect = new Rect(0f, y, viewRect.width, height);
                    
                    if (selectionBox.Overlaps(cardRect))
                    {
                        selectedMemories.Add(memory);
                    }
                    
                    y += height + CARD_SPACING;
                }
                
                e.Use();
            }
            
            if (e.type == EventType.MouseUp && e.button == 0 && isDragging)
            {
                isDragging = false;
                e.Use();
            }
        }
        
        private void DrawSelectionBox()
        {
            Rect selectionBox = GetSelectionBox();
            Widgets.DrawBox(selectionBox);
            Widgets.DrawBoxSolid(selectionBox, new Color(1f, 0.8f, 0.3f, 0.2f));
        }
        
        private Rect GetSelectionBox()
        {
            float minX = Mathf.Min(dragStartPos.x, dragCurrentPos.x);
            float minY = Mathf.Min(dragStartPos.y, dragCurrentPos.y);
            float maxX = Mathf.Max(dragStartPos.x, dragCurrentPos.x);
            float maxY = Mathf.Max(dragStartPos.y, dragCurrentPos.y);
            
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // ==================== Batch Actions ====================
        
        private void SummarizeSelectedMemories()
        {
            if (currentMemoryComp == null || selectedMemories.Count == 0)
                return;
            
            var scmMemories = selectedMemories.Where(m => m.layer == MemoryLayer.Situational).ToList();
            if (scmMemories.Count == 0)
            {
                Messages.Message("RimTalk_MindStream_NoSCMSelected".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_MindStream_SummarizeConfirm".Translate(scmMemories.Count),
                delegate
                {
                    foreach (var memory in scmMemories)
                    {
                        // Move to ELS
                        currentMemoryComp.SituationalMemories.Remove(memory);
                        memory.layer = MemoryLayer.EventLog;
                        currentMemoryComp.EventLogMemories.Insert(0, memory);
                    }
                    
                    selectedMemories.Clear();
                    Messages.Message("RimTalk_MindStream_SummarizedN".Translate(scmMemories.Count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void ArchiveSelectedMemories()
        {
            if (currentMemoryComp == null || selectedMemories.Count == 0)
                return;
            
            var elsMemories = selectedMemories.Where(m => m.layer == MemoryLayer.EventLog).ToList();
            if (elsMemories.Count == 0)
            {
                Messages.Message("RimTalk_MindStream_NoELSSelected".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_MindStream_ArchiveConfirm".Translate(elsMemories.Count),
                delegate
                {
                    foreach (var memory in elsMemories)
                    {
                        // Move to CLPA
                        currentMemoryComp.EventLogMemories.Remove(memory);
                        memory.layer = MemoryLayer.Archive;
                        currentMemoryComp.ArchiveMemories.Insert(0, memory);
                    }
                    
                    selectedMemories.Clear();
                    Messages.Message("RimTalk_MindStream_ArchivedN".Translate(elsMemories.Count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void DeleteSelectedMemories()
        {
            if (currentMemoryComp == null || selectedMemories.Count == 0)
                return;
            
            int count = selectedMemories.Count;
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_MindStream_DeleteConfirm".Translate(count),
                delegate
                {
                    foreach (var memory in selectedMemories.ToList())
                    {
                        currentMemoryComp.DeleteMemory(memory.id);
                    }
                    
                    selectedMemories.Clear();
                    Messages.Message("RimTalk_MindStream_DeletedN".Translate(count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void SummarizeAll()
        {
            List<Pawn> pawnsToSummarize = new List<Pawn>();
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    var comp = pawn.TryGetComp<PawnMemoryComp>();
                    if (comp != null && comp.GetSituationalMemoryCount() > 0)
                    {
                        pawnsToSummarize.Add(pawn);
                    }
                }
            }
            
            if (pawnsToSummarize.Count > 0)
            {
                var memoryManager = Find.World.GetComponent<MemoryManager>();
                memoryManager?.QueueManualSummarization(pawnsToSummarize);
                Messages.Message("RimTalk_MindStream_QueuedSummarization".Translate(pawnsToSummarize.Count), MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message("RimTalk_MindStream_NoNeedSummarization".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
        
        private void ArchiveAll()
        {
            int count = 0;
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    var comp = pawn.TryGetComp<PawnMemoryComp>();
                    if (comp != null && comp.GetEventLogMemoryCount() > 0)
                    {
                        comp.ManualArchive();
                        count++;
                    }
                }
            }
            
            Messages.Message("RimTalk_MindStream_ArchivedForN".Translate(count), MessageTypeDefOf.PositiveEvent, false);
        }

        // ==================== Helper Methods ====================
        
        private List<MemoryEntry> GetFilteredMemories()
        {
            if (currentMemoryComp == null)
                return new List<MemoryEntry>();
            
            var memories = new List<MemoryEntry>();
            
            if (showABM)
            {
                memories.AddRange(currentMemoryComp.ActiveMemories.Where(m => filterType == null || m.type == filterType.Value));
            }
            
            if (showSCM)
            {
                memories.AddRange(currentMemoryComp.SituationalMemories.Where(m => filterType == null || m.type == filterType.Value));
            }
            
            if (showELS)
            {
                memories.AddRange(currentMemoryComp.EventLogMemories.Where(m => filterType == null || m.type == filterType.Value));
            }
            
            if (showCLPA)
            {
                memories.AddRange(currentMemoryComp.ArchiveMemories.Where(m => filterType == null || m.type == filterType.Value));
            }
            
            // Sort by timestamp (newest first)
            memories = memories.OrderByDescending(m => m.timestamp).ToList();
            
            return memories;
        }
        
        private float GetCardHeight(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 80f;
                case MemoryLayer.Situational:
                    return 100f;
                case MemoryLayer.EventLog:
                    return 130f;
                case MemoryLayer.Archive:
                    return 160f;
                default:
                    return 100f;
            }
        }
        
        private int GetContentMaxLength(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return 80;
                case MemoryLayer.Situational:
                    return 120;
                case MemoryLayer.EventLog:
                    return 200;
                case MemoryLayer.Archive:
                    return 300;
                default:
                    return 120;
            }
        }
        
        private Color GetLayerColor(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return new Color(0.3f, 0.8f, 1f); // Cyan
                case MemoryLayer.Situational:
                    return new Color(0.3f, 1f, 0.5f); // Green
                case MemoryLayer.EventLog:
                    return new Color(1f, 0.8f, 0.3f); // Yellow
                case MemoryLayer.Archive:
                    return new Color(0.8f, 0.4f, 1f); // Purple
                default:
                    return Color.white;
            }
        }
        
        private string GetLayerLabel(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return "ABM";
                case MemoryLayer.Situational:
                    return "SCM";
                case MemoryLayer.EventLog:
                    return "ELS";
                case MemoryLayer.Archive:
                    return "CLPA";
                default:
                    return "UNK";
            }
        }
        
        private void DrawNoPawnSelected(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect, "RimTalk_MindStream_SelectColonist".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        
        private void DrawNoMemoryComponent(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "RimTalk_NoMemoryComponent".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        private void OpenCommonKnowledgeDialog()
        {
            if (Current.Game == null)
            {
                Messages.Message("RimTalk_MindStream_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            var memoryManager = Find.World.GetComponent<MemoryManager>();
            if (memoryManager == null)
            {
                Messages.Message("RimTalk_MindStream_CannotFindManager".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            Find.WindowStack.Add(new Dialog_CommonKnowledge(memoryManager.CommonKnowledge));
        }
    }
}
