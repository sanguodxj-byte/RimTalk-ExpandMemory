using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 常识库管理窗口 - Library Style with Drag Multi-Select
    /// ★ v3.3.19: 完全重构 - 图书馆风格布局 + 拖拽多选支持
    /// </summary>
    public class Dialog_CommonKnowledge : Window
    {
        // ==================== Data & State ====================
        private CommonKnowledgeLibrary library;
        
        // Multi-select support
        private HashSet<CommonKnowledgeEntry> selectedEntries = new HashSet<CommonKnowledgeEntry>();
        private CommonKnowledgeEntry lastSelectedEntry = null; // For detail view
        
        // Drag selection
        private bool isDragging = false;
        private Vector2 dragStartPos = Vector2.zero;
        private Vector2 dragCurrentPos = Vector2.zero;
        
        // UI State
        private Vector2 listScrollPosition = Vector2.zero;
        private Vector2 detailScrollPosition = Vector2.zero;
        private string searchFilter = "";
        private bool editMode = false;
        private bool showRightPanel = true;
        
        // Category filter
        private KnowledgeCategory currentCategory = KnowledgeCategory.All;
        
        // Auto-generate settings (collapsed in sidebar)
        private bool showAutoGenerateSettings = false;
        
        // Edit fields
        private string editTag = "";
        private string editContent = "";
        private float editImportance = 0.5f;
        private int editTargetPawnId = -1;
        private string editExcludeKeywords = "";
        private KeywordMatchMode editMatchMode = KeywordMatchMode.Any;
        
        // Layout constants
        private const float TOOLBAR_HEIGHT = 45f;
        private const float SIDEBAR_WIDTH = 240f;
        private const float RIGHT_PANEL_WIDTH = 340f;
        private const float SPACING = 10f;
        private const float BUTTON_HEIGHT = 32f;
        private const float ENTRY_HEIGHT = 70f;

        public override Vector2 InitialSize => new Vector2(1200f, 800f);

        public Dialog_CommonKnowledge(CommonKnowledgeLibrary library)
        {
            this.library = library;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            this.forcePause = false;
        }

        // ==================== Main Layout ====================
        
        public override void DoWindowContents(Rect inRect)
        {
            // Top Toolbar
            Rect toolbarRect = new Rect(0f, 0f, inRect.width, TOOLBAR_HEIGHT);
            DrawToolbar(toolbarRect);
            
            float contentY = TOOLBAR_HEIGHT + SPACING;
            float contentHeight = inRect.height - contentY;
            
            // Left Sidebar
            Rect sidebarRect = new Rect(0f, contentY, SIDEBAR_WIDTH, contentHeight);
            DrawSidebar(sidebarRect);
            
            // Center List
            float centerX = SIDEBAR_WIDTH + SPACING;
            float centerWidth = showRightPanel ? 
                (inRect.width - SIDEBAR_WIDTH - RIGHT_PANEL_WIDTH - SPACING * 3) : 
                (inRect.width - SIDEBAR_WIDTH - SPACING * 2);
            Rect centerRect = new Rect(centerX, contentY, centerWidth, contentHeight);
            DrawCenterList(centerRect);
            
            // Right Panel (Detail/Edit)
            if (showRightPanel)
            {
                float rightX = centerX + centerWidth + SPACING;
                Rect rightRect = new Rect(rightX, contentY, RIGHT_PANEL_WIDTH, contentHeight);
                DrawRightPanel(rightRect);
            }
            
            // Handle input events (must be after all UI drawing)
            if (Event.current.type == EventType.MouseUp && isDragging)
            {
                isDragging = false;
                Event.current.Use();
            }
        }

        // ==================== Toolbar ====================
        
        private void DrawToolbar(Rect rect)
        {
            GUI.Box(rect, "");
            
            Rect innerRect = rect.ContractedBy(5f);
            float x = innerRect.x;
            
            // Search bar (left side)
            float searchWidth = 300f;
            Rect searchRect = new Rect(x, innerRect.y + 5f, searchWidth, 30f);
            searchFilter = Widgets.TextField(searchRect, searchFilter);
            
            // Buttons (right side, stack from right-to-left)
            float buttonWidth = 100f;
            float spacing = 5f;
            
            // Calculate button positions from right to left
            float rightX = innerRect.xMax;
            
            // Clear All button
            rightX -= buttonWidth;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), "RimTalk_Knowledge_ClearAll".Translate()))
            {
                ClearAllEntries();
            }
            
            // Delete Selected button
            rightX -= buttonWidth + spacing;
            GUI.enabled = selectedEntries.Count > 0;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), "RimTalk_Knowledge_DeleteCount".Translate(selectedEntries.Count)))
            {
                DeleteSelectedEntries();
            }
            GUI.enabled = true;
            
            // Export button
            rightX -= buttonWidth + spacing;
            string exportLabel = selectedEntries.Count > 0 ? "RimTalk_Knowledge_ExportCount".Translate(selectedEntries.Count) : "RimTalk_Knowledge_ExportAll".Translate();
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), exportLabel))
            {
                ExportToFile();
            }
            
            // Import button
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), "RimTalk_Knowledge_Import".Translate()))
            {
                ShowImportDialog();
            }
            
            // New button
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), "RimTalk_Knowledge_New".Translate()))
            {
                CreateNewEntry();
            }
            
            // Toggle Right Panel button
            rightX -= 40f + spacing;
            string toggleIcon = showRightPanel ? "◀" : "▶";
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, 40f, BUTTON_HEIGHT), toggleIcon))
            {
                showRightPanel = !showRightPanel;
            }
        }

        // ==================== Sidebar ====================
        
        private void DrawSidebar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            Rect innerRect = rect.ContractedBy(SPACING);
            float y = innerRect.y;
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 30f), "RimTalk_Knowledge_Categories".Translate());
            Text.Font = GameFont.Small;
            y += 35f;
            
            // Category buttons
            float categoryHeight = 35f;
            foreach (KnowledgeCategory category in Enum.GetValues(typeof(KnowledgeCategory)))
            {
                bool isSelected = currentCategory == category;
                
                Rect categoryRect = new Rect(innerRect.x, y, innerRect.width, categoryHeight);
                string categoryLabel = GetCategoryLabel(category);
                int categoryCount = GetCategoryCount(category);
                string label = $"{categoryLabel} ({categoryCount})";
                
                // ⭐ 修复：使用正确的高亮逻辑
                if (isSelected)
                {
                    Widgets.DrawHighlightSelected(categoryRect);
                }
                else if (Mouse.IsOver(categoryRect))
                {
                    Widgets.DrawHighlight(categoryRect);
                }
                
                // ⭐ 修复：确保ButtonText能正常响应点击
                if (Widgets.ButtonText(categoryRect, label, drawBackground: false))
                {
                    currentCategory = category;
                    selectedEntries.Clear();
                }
                
                y += categoryHeight + 5f;
            }
            
            y += 20f;
            
            // Auto-Generate Settings (collapsible)
            Rect autoGenHeaderRect = new Rect(innerRect.x, y, innerRect.width, 30f);
            string autoGenIcon = showAutoGenerateSettings ? "▼" : "▶";
            if (Widgets.ButtonText(autoGenHeaderRect, $"{autoGenIcon} {"RimTalk_Knowledge_AutoGenerate".Translate()}"))
            {
                showAutoGenerateSettings = !showAutoGenerateSettings;
            }
            y += 35f;
            
            if (showAutoGenerateSettings)
            {
                Rect autoGenContentRect = new Rect(innerRect.x, y, innerRect.width, 120f);
                DrawAutoGenerateSettings(autoGenContentRect);
                y += 125f;
            }
            
            // Statistics at bottom
            float statsY = innerRect.yMax - 60f;
            Widgets.DrawLineHorizontal(innerRect.x, statsY - 10f, innerRect.width);
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            int totalCount = library.Entries.Count;
            int enabledCount = library.Entries.Count(e => e.isEnabled);
            int selectedCount = selectedEntries.Count;
            
            Widgets.Label(new Rect(innerRect.x, statsY, innerRect.width, 20f), "RimTalk_Knowledge_Total".Translate(totalCount));
            Widgets.Label(new Rect(innerRect.x, statsY + 20f, innerRect.width, 20f), "RimTalk_Knowledge_Enabled".Translate(enabledCount));
            Widgets.Label(new Rect(innerRect.x, statsY + 40f, innerRect.width, 20f), "RimTalk_Knowledge_Selected".Translate(selectedCount));
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
        
        private void DrawAutoGenerateSettings(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            Rect innerRect = rect.ContractedBy(5f);
            float y = innerRect.y;
            
            var settings = RimTalkMemoryPatchMod.Settings;
            
            // Pawn Status
            bool enablePawnStatus = settings.enablePawnStatusKnowledge;
            Widgets.CheckboxLabeled(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_PawnStatus".Translate(), ref enablePawnStatus);
            settings.enablePawnStatusKnowledge = enablePawnStatus;
            y += 30f;
            
            if (Widgets.ButtonText(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_GenerateNow".Translate()))
            {
                GeneratePawnStatusKnowledge();
            }
            y += 30f;
            
            // Event Record
            bool enableEventRecord = settings.enableEventRecordKnowledge;
            Widgets.CheckboxLabeled(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_EventRecord".Translate(), ref enableEventRecord);
            settings.enableEventRecordKnowledge = enableEventRecord;
            y += 30f;
            
            if (Widgets.ButtonText(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_GenerateNow".Translate()))
            {
                GenerateEventRecordKnowledge();
            }
        }

        // ==================== Center List ====================
        
        private void DrawCenterList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            Rect innerRect = rect.ContractedBy(5f);
            
            // Filter entries
            var filteredEntries = GetFilteredEntries();
            
            // Calculate view rect
            float totalHeight = filteredEntries.Count * ENTRY_HEIGHT;
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, totalHeight);
            
            // Store list rect for drag selection
            Rect listRect = innerRect;
            
            // Handle drag selection input
            HandleDragSelection(listRect, viewRect);
            
            // Begin scroll view
            Widgets.BeginScrollView(innerRect, ref listScrollPosition, viewRect, true);
            
            float y = 0f;
            foreach (var entry in filteredEntries)
            {
                Rect entryRect = new Rect(0f, y, viewRect.width, ENTRY_HEIGHT);
                DrawEntryRow(entryRect, entry);
                y += ENTRY_HEIGHT;
            }
            
            // Draw drag selection box
            if (isDragging)
            {
                DrawSelectionBox();
            }
            
            Widgets.EndScrollView();
            
            // Show filter status
            if (!string.IsNullOrEmpty(searchFilter) || currentCategory != KnowledgeCategory.All)
            {
                Rect statusRect = new Rect(innerRect.x, innerRect.yMax - 25f, innerRect.width, 20f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(statusRect, "RimTalk_Knowledge_Showing".Translate(filteredEntries.Count, library.Entries.Count));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }
        
        private void DrawEntryRow(Rect rect, CommonKnowledgeEntry entry)
        {
            bool isSelected = selectedEntries.Contains(entry);
            
            // Background
            if (isSelected)
            {
                Widgets.DrawHighlight(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawLightHighlight(rect);
            }
            
            // Handle click (without drag)
            if (Widgets.ButtonInvisible(rect) && !isDragging)
            {
                HandleEntryClick(entry);
            }
            
            Rect innerRect = rect.ContractedBy(5f);
            float x = innerRect.x;
            
            // Checkbox
            Rect checkboxRect = new Rect(x, innerRect.y + 5f, 24f, 24f);
            bool wasEnabled = entry.isEnabled;
            Widgets.Checkbox(checkboxRect.position, ref entry.isEnabled);
            x += 30f;
            
            // Tag
            Rect tagRect = new Rect(x, innerRect.y, 150f, 20f);
            Text.Font = GameFont.Small;
            GUI.color = GetCategoryColor(entry);
            Widgets.Label(tagRect, $"[{entry.tag}]");
            GUI.color = Color.white;
            x += 155f;
            
            // Importance
            Rect importanceRect = new Rect(x, innerRect.y, 50f, 20f);
            Widgets.Label(importanceRect, entry.importance.ToString("F1"));
            x += 55f;
            
            // Content preview (multi-line) - 为右侧复选框留出空间
            Rect contentRect = new Rect(innerRect.x + 30f, innerRect.y + 22f, innerRect.width - 95f, 40f);
            Text.Font = GameFont.Tiny;
            string preview = entry.content.Length > 120 ? entry.content.Substring(0, 120) + "..." : entry.content;
            Widgets.Label(contentRect, preview);
            Text.Font = GameFont.Small;
            
            // Extended properties checkboxes (right side)
            const float BUTTON_SIZE = 20f;
            const float BUTTON_SPACING = 5f;
            float rightX = rect.xMax - BUTTON_SIZE * 2 - BUTTON_SPACING - 10f;
            float centerY = rect.y + (rect.height - BUTTON_SIZE) / 2f;

            // Checkbox 1: Can Be Extracted
            bool canBeExtracted = ExtendedKnowledgeEntry.CanBeExtracted(entry);
            Rect extractCheckboxRect = new Rect(rightX, centerY, BUTTON_SIZE, BUTTON_SIZE);
            
            if (Mouse.IsOver(extractCheckboxRect))
            {
                string tooltip = canBeExtracted 
                    ? "RimTalk_CanBeExtractedEnabled".Translate() 
                    : "RimTalk_CanBeExtractedDisabled".Translate();
                TooltipHandler.TipRegion(extractCheckboxRect, tooltip);
            }
            
            bool newExtractValue = canBeExtracted;
            Widgets.Checkbox(extractCheckboxRect.position, ref newExtractValue, BUTTON_SIZE);
            if (newExtractValue != canBeExtracted)
            {
                ExtendedKnowledgeEntry.SetCanBeExtracted(entry, newExtractValue);
            }

            // Checkbox 2: Can Be Matched
            rightX += BUTTON_SIZE + BUTTON_SPACING;
            bool canBeMatched = ExtendedKnowledgeEntry.CanBeMatched(entry);
            Rect matchCheckboxRect = new Rect(rightX, centerY, BUTTON_SIZE, BUTTON_SIZE);
            
            if (Mouse.IsOver(matchCheckboxRect))
            {
                string tooltip = canBeMatched 
                    ? "RimTalk_CanBeMatchedEnabled".Translate() 
                    : "RimTalk_CanBeMatchedDisabled".Translate();
                TooltipHandler.TipRegion(matchCheckboxRect, tooltip);
            }
            
            bool newMatchValue = canBeMatched;
            Widgets.Checkbox(matchCheckboxRect.position, ref newMatchValue, BUTTON_SIZE);
            if (newMatchValue != canBeMatched)
            {
                ExtendedKnowledgeEntry.SetCanBeMatched(entry, newMatchValue);
            }
            
            // Selection indicator
            if (isSelected)
            {
                Rect indicatorRect = new Rect(rect.x, rect.y, 3f, rect.height);
                Widgets.DrawBoxSolid(indicatorRect, new Color(0.3f, 0.6f, 1f));
            }
        }
        
        private void HandleEntryClick(CommonKnowledgeEntry entry)
        {
            bool ctrl = Event.current.control;
            bool shift = Event.current.shift;
            
            if (ctrl)
            {
                // Ctrl+Click: Toggle selection
                if (selectedEntries.Contains(entry))
                    selectedEntries.Remove(entry);
                else
                    selectedEntries.Add(entry);
                    
                lastSelectedEntry = entry;
            }
            else if (shift && lastSelectedEntry != null)
            {
                // Shift+Click: Range selection
                var filteredEntries = GetFilteredEntries();
                int startIndex = filteredEntries.IndexOf(lastSelectedEntry);
                int endIndex = filteredEntries.IndexOf(entry);
                
                if (startIndex >= 0 && endIndex >= 0)
                {
                    int min = Math.Min(startIndex, endIndex);
                    int max = Math.Max(startIndex, endIndex);
                    
                    for (int i = min; i <= max; i++)
                    {
                        selectedEntries.Add(filteredEntries[i]);
                    }
                }
                
                lastSelectedEntry = entry;
            }
            else
            {
                // Normal click: Single selection
                selectedEntries.Clear();
                selectedEntries.Add(entry);
                lastSelectedEntry = entry;
                editMode = false;
            }
        }
        
        private void HandleDragSelection(Rect listRect, Rect viewRect)
        {
            Event e = Event.current;
            
            if (e.type == EventType.MouseDown && e.button == 0 && listRect.Contains(e.mousePosition))
            {
                isDragging = true;
                dragStartPos = e.mousePosition - listRect.position + listScrollPosition;
                dragCurrentPos = dragStartPos;
                e.Use();
            }
            
            if (isDragging && e.type == EventType.MouseDrag)
            {
                dragCurrentPos = e.mousePosition - listRect.position + listScrollPosition;
                
                // Select entries that intersect with drag box
                Rect selectionBox = GetSelectionBox();
                var filteredEntries = GetFilteredEntries();
                
                bool ctrl = Event.current.control;
                if (!ctrl)
                {
                    selectedEntries.Clear();
                }
                
                for (int i = 0; i < filteredEntries.Count; i++)
                {
                    Rect entryRect = new Rect(0f, i * ENTRY_HEIGHT, viewRect.width, ENTRY_HEIGHT);
                    if (selectionBox.Overlaps(entryRect))
                    {
                        selectedEntries.Add(filteredEntries[i]);
                    }
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
            Widgets.DrawBoxSolid(selectionBox, new Color(0.3f, 0.6f, 1f, 0.2f));
        }
        
        private Rect GetSelectionBox()
        {
            float minX = Mathf.Min(dragStartPos.x, dragCurrentPos.x);
            float minY = Mathf.Min(dragStartPos.y, dragCurrentPos.y);
            float maxX = Mathf.Max(dragStartPos.x, dragCurrentPos.x);
            float maxY = Mathf.Max(dragStartPos.y, dragCurrentPos.y);
            
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // ==================== Right Panel ====================
        
        private void DrawRightPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            Rect innerRect = rect.ContractedBy(SPACING);
            
            if (editMode)
            {
                DrawEditPanel(innerRect);
            }
            else if (selectedEntries.Count == 0)
            {
                DrawEmptyPanel(innerRect);
            }
            else if (selectedEntries.Count == 1)
            {
                DrawDetailPanel(innerRect, selectedEntries.First());
            }
            else
            {
                DrawMultiSelectionPanel(innerRect);
            }
        }
        
        private void DrawEmptyPanel(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(rect, "RimTalk_Knowledge_SelectOrCreate".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        private void DrawMultiSelectionPanel(Rect rect)
        {
            float y = rect.y;
            
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), "RimTalk_Knowledge_ItemsSelected".Translate(selectedEntries.Count));
            Text.Font = GameFont.Small;
            y += 40f;
            
            // Statistics
            int enabledCount = selectedEntries.Count(e => e.isEnabled);
            int disabledCount = selectedEntries.Count - enabledCount;
            float avgImportance = selectedEntries.Average(e => e.importance);
            
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), "RimTalk_Knowledge_EnabledCount".Translate(enabledCount));
            y += 25f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), "RimTalk_Knowledge_DisabledCount".Translate(disabledCount));
            y += 25f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), "RimTalk_Knowledge_AvgImportance".Translate(avgImportance.ToString("F2")));
            y += 40f;
            
            // Batch actions
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_Knowledge_EnableAll".Translate()))
            {
                foreach (var entry in selectedEntries)
                    entry.isEnabled = true;
            }
            y += BUTTON_HEIGHT + 5f;
            
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_Knowledge_DisableAll".Translate()))
            {
                foreach (var entry in selectedEntries)
                    entry.isEnabled = false;
            }
            y += BUTTON_HEIGHT + 5f;
            
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_Knowledge_ExportItems".Translate(selectedEntries.Count)))
            {
                ExportToFile();
            }
            y += BUTTON_HEIGHT + 5f;
            
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_Knowledge_DeleteItems".Translate(selectedEntries.Count)))
            {
                DeleteSelectedEntries();
            }
            GUI.color = Color.white;
            y += BUTTON_HEIGHT + 10f;
            
            // Extended properties batch operations
            Widgets.DrawLineHorizontal(rect.x, y, rect.width);
            y += 10f;
            
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_EnableAllExtract".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeExtracted(entry, true);
                }
            }
            y += BUTTON_HEIGHT + 5f;

            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_DisableAllExtract".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeExtracted(entry, false);
                }
            }
            y += BUTTON_HEIGHT + 5f;

            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_EnableAllMatch".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeMatched(entry, true);
                }
            }
            y += BUTTON_HEIGHT + 5f;

            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalk_DisableAllMatch".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeMatched(entry, false);
                }
            }
        }
        
        private void DrawDetailPanel(Rect rect, CommonKnowledgeEntry entry)
        {
            float y = rect.y;
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width - 100f, 30f), "RimTalk_Knowledge_Details".Translate());
            Text.Font = GameFont.Small;
            
            // Edit button
            if (Widgets.ButtonText(new Rect(rect.xMax - 90f, y, 90f, 28f), "RimTalk_Knowledge_Edit".Translate()))
            {
                StartEdit();
            }
            y += 40f;
            
            // Scrollable content
            Rect scrollOuterRect = new Rect(rect.x, y, rect.width, rect.height - y - 50f);
            float scrollHeight = 500f; // Estimate
            Rect scrollViewRect = new Rect(0f, 0f, rect.width - 16f, scrollHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref detailScrollPosition, scrollViewRect);
        
            float scrollY = 0f;
            
            // Tag
            DrawDetailField(new Rect(0f, scrollY, scrollViewRect.width, 50f), "RimTalk_Knowledge_Tag".Translate(), entry.tag);
            scrollY += 55f;
            
            // Importance
            DrawDetailField(new Rect(0f, scrollY, scrollViewRect.width, 25f), "RimTalk_Knowledge_Importance".Translate(), entry.importance.ToString("F1"));
            scrollY += 30f;
            
            // Status
            string status = entry.isEnabled ? "RimTalk_Knowledge_StatusEnabled".Translate() : "RimTalk_Knowledge_StatusDisabled".Translate();
            DrawDetailField(new Rect(0f, scrollY, scrollViewRect.width, 25f), "RimTalk_Knowledge_Status".Translate(), status);
            scrollY += 30f;
            
            // Visibility
            string visibility = GetVisibilityText(entry);
            DrawDetailField(new Rect(0f, scrollY, scrollViewRect.width, 50f), "RimTalk_Knowledge_Visibility".Translate(), visibility);
            scrollY += 55f;
            
            // Exclude Keywords
            if (entry.excludeKeywords != null && entry.excludeKeywords.Count > 0)
            {
                string excludes = string.Join(", ", entry.excludeKeywords);
                DrawDetailField(new Rect(0f, scrollY, scrollViewRect.width, 50f), "排除词", excludes);
                scrollY += 55f;
            }
            
            // Content
            Widgets.Label(new Rect(0f, scrollY, scrollViewRect.width, 20f), "RimTalk_Knowledge_Content".Translate() + ":");
            scrollY += 22f;
            
            Rect contentRect = new Rect(0f, scrollY, scrollViewRect.width, 200f);
            Widgets.DrawBoxSolid(contentRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            Rect contentInnerRect = contentRect.ContractedBy(5f);
            Widgets.Label(contentInnerRect, entry.content);
            scrollY += 205f;
            
            // Extended properties display
            Widgets.DrawLineHorizontal(0f, scrollY, scrollViewRect.width);
            scrollY += 10f;

            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(0f, scrollY, scrollViewRect.width, 25f), "RimTalk_ExtendedProperties".Translate());
            GUI.color = Color.white;
            scrollY += 25f;

            bool canBeExtracted = ExtendedKnowledgeEntry.CanBeExtracted(entry);
            DrawPropertyRow(new Rect(0f, scrollY, scrollViewRect.width, 25f), "RimTalk_CanBeExtracted".Translate(), canBeExtracted);
            scrollY += 25f;

            bool canBeMatched = ExtendedKnowledgeEntry.CanBeMatched(entry);
            DrawPropertyRow(new Rect(0f, scrollY, scrollViewRect.width, 25f), "RimTalk_CanBeMatched".Translate(), canBeMatched);
            scrollY += 25f;
            
            Widgets.EndScrollView();
            
            // Delete button at bottom
            Rect deleteRect = new Rect(rect.x, rect.yMax - 40f, rect.width, BUTTON_HEIGHT);
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(deleteRect, "RimTalk_Knowledge_Delete".Translate()))
            {
                DeleteSelectedEntries();
            }
            GUI.color = Color.white;
        }
        
        private void DrawDetailField(Rect rect, string label, string value)
        {
            float labelWidth = 100f;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label + ":");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            Widgets.Label(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height), value);
        }
        
        private void DrawPropertyRow(Rect rect, string label, bool value)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, rect.height), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            string valueText = value ? "RimTalk_Yes".Translate() : "RimTalk_No".Translate();
            Color valueColor = value ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            
            GUI.color = valueColor;
            Widgets.Label(new Rect(rect.x + 120f, rect.y, rect.width - 120f, rect.height), valueText);
            GUI.color = Color.white;
        }
        
        private void DrawEditPanel(Rect rect)
        {
            float y = rect.y;
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), lastSelectedEntry == null ? "RimTalk_Knowledge_NewEntry".Translate() : "RimTalk_Knowledge_EditEntry".Translate());
            Text.Font = GameFont.Small;
            y += 40f;
            
            // Tag
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "RimTalk_Knowledge_Tag".Translate() + ":");
            editTag = Widgets.TextField(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), editTag);
            y += 30f;
            
            // Importance
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "RimTalk_Knowledge_Importance".Translate() + ":");
            editImportance = Widgets.HorizontalSlider(new Rect(rect.x + 100f, y, rect.width - 150f, 25f), editImportance, 0f, 1f);
            Widgets.Label(new Rect(rect.xMax - 40f, y, 40f, 25f), editImportance.ToString("F1"));
            y += 30f;
            
            // Pawn selection (simplified)
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "RimTalk_Knowledge_Visibility".Translate() + ":");
            string pawnLabel = editTargetPawnId == -1 ? "RimTalk_Knowledge_Global".Translate().ToString() : $"Pawn #{editTargetPawnId}";
            if (Widgets.ButtonText(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), pawnLabel))
            {
                ShowPawnSelectionMenu();
            }
            y += 35f;
            
            // Exclude Keywords
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "排除词:");
            editExcludeKeywords = Widgets.TextField(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), editExcludeKeywords);
            y += 35f;

            // Match Mode
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "匹配模式:");
            if (Widgets.ButtonText(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), editMatchMode.ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (KeywordMatchMode mode in Enum.GetValues(typeof(KeywordMatchMode)))
                {
                    options.Add(new FloatMenuOption(mode.ToString(), delegate { editMatchMode = mode; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 35f;
            
            // Content
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "RimTalk_Knowledge_Content".Translate() + ":");
            y += 27f;
            
            float contentHeight = rect.yMax - y - 80f;
            Rect contentRect = new Rect(rect.x, y, rect.width, contentHeight);
            Widgets.DrawBoxSolid(contentRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Rect textAreaRect = contentRect.ContractedBy(5f);
            editContent = GUI.TextArea(textAreaRect, editContent ?? "");
            y += contentHeight + 10f;
            
            // Buttons
            float buttonY = rect.yMax - 65f;
            if (Widgets.ButtonText(new Rect(rect.x, buttonY, (rect.width - 5f) / 2f, BUTTON_HEIGHT), "RimTalk_Knowledge_Save".Translate()))
            {
                SaveEntry();
            }
            
            if (Widgets.ButtonText(new Rect(rect.x + (rect.width + 5f) / 2f, buttonY, (rect.width - 5f) / 2f, BUTTON_HEIGHT), "RimTalk_Knowledge_Cancel".Translate()))
            {
                editMode = false;
            }
        }

        // ==================== Helper Methods ====================
        
        private List<CommonKnowledgeEntry> GetFilteredEntries()
        {
            var entries = library.Entries.AsEnumerable();
            
            // Category filter
            if (currentCategory != KnowledgeCategory.All)
            {
                entries = entries.Where(e => GetEntryCategory(e) == currentCategory);
            }
            
            // Search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string lower = searchFilter.ToLower();
                entries = entries.Where(e => 
                    e.tag.ToLower().Contains(lower) || 
                    e.content.ToLower().Contains(lower));
            }
            
            return entries.ToList();
        }
        
        private KnowledgeCategory GetEntryCategory(CommonKnowledgeEntry entry)
        {
            if (entry.tag.Contains("规则") || entry.tag.Contains("Instructions"))
                return KnowledgeCategory.Instructions;
            if (entry.tag.Contains("世界观") || entry.tag.Contains("Lore"))
                return KnowledgeCategory.Lore;
            if (entry.tag.Contains("殖民者状态") || entry.tag.Contains("PawnStatus"))
                return KnowledgeCategory.PawnStatus;
            if (entry.tag.Contains("历史") || entry.tag.Contains("History"))
                return KnowledgeCategory.History;
            
            return KnowledgeCategory.Other;
        }
        
        private string GetCategoryLabel(KnowledgeCategory category)
        {
            switch (category)
            {
                case KnowledgeCategory.All: return "RimTalk_Knowledge_CategoryAll".Translate();
                case KnowledgeCategory.Instructions: return "RimTalk_Knowledge_CategoryInstructions".Translate();
                case KnowledgeCategory.Lore: return "RimTalk_Knowledge_CategoryLore".Translate();
                case KnowledgeCategory.PawnStatus: return "RimTalk_Knowledge_CategoryPawnStatus".Translate();
                case KnowledgeCategory.History: return "RimTalk_Knowledge_CategoryHistory".Translate();
                case KnowledgeCategory.Other: return "RimTalk_Knowledge_CategoryOther".Translate();
                default: return "RimTalk_Knowledge_CategoryUnknown".Translate();
            }
        }
        
        private int GetCategoryCount(KnowledgeCategory category)
        {
            if (category == KnowledgeCategory.All)
                return library.Entries.Count;
            
            return library.Entries.Count(e => GetEntryCategory(e) == category);
        }
        
        private Color GetCategoryColor(CommonKnowledgeEntry entry)
        {
            var category = GetEntryCategory(entry);
            switch (category)
            {
                case KnowledgeCategory.Instructions: return new Color(0.3f, 0.8f, 0.3f);
                case KnowledgeCategory.Lore: return new Color(0.8f, 0.6f, 0.3f);
                case KnowledgeCategory.PawnStatus: return new Color(0.3f, 0.6f, 0.9f);
                case KnowledgeCategory.History: return new Color(0.7f, 0.5f, 0.7f);
                default: return Color.white;
            }
        }
        
        private string GetVisibilityText(CommonKnowledgeEntry entry)
        {
            if (entry.targetPawnId == -1)
                return "RimTalk_Knowledge_VisibilityGlobal".Translate();
            
            var pawn = Find.Maps?
                .SelectMany(m => m.mapPawns.FreeColonists)
                .FirstOrDefault(p => p.thingIDNumber == entry.targetPawnId);
            
            return pawn != null ? "RimTalk_Knowledge_VisibilityExclusive".Translate(pawn.LabelShort) : "RimTalk_Knowledge_VisibilityDeleted".Translate(entry.targetPawnId);
        }

        // ==================== Actions ====================
        
        private void CreateNewEntry()
        {
            editTag = "";
            editContent = "";
            editImportance = 0.5f;
            editTargetPawnId = -1;
            editExcludeKeywords = "";
            editMatchMode = KeywordMatchMode.Any;
            lastSelectedEntry = null;
            selectedEntries.Clear();
            editMode = true;
        }
        
        private void StartEdit()
        {
            if (selectedEntries.Count == 1)
            {
                var entry = selectedEntries.First();
                editTag = entry.tag;
                editContent = entry.content;
                editImportance = entry.importance;
                editTargetPawnId = entry.targetPawnId;
                editExcludeKeywords = entry.excludeKeywords != null ? string.Join(", ", entry.excludeKeywords) : "";
                editMatchMode = entry.matchMode;
                lastSelectedEntry = entry;
                editMode = true;
            }
        }
        
        private void SaveEntry()
        {
            if (string.IsNullOrEmpty(editTag) || string.IsNullOrEmpty(editContent))
            {
                Messages.Message("RimTalk_Knowledge_TagContentEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            List<string> excludes = editExcludeKeywords.Split(new[] { ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();
            
            if (lastSelectedEntry == null)
            {
                // Create new
                var newEntry = new CommonKnowledgeEntry(editTag, editContent)
                {
                    importance = editImportance,
                    targetPawnId = editTargetPawnId,
                    isUserEdited = true,
                    excludeKeywords = excludes,
                    matchMode = editMatchMode
                };
                library.AddEntry(newEntry);
                selectedEntries.Clear();
                selectedEntries.Add(newEntry);
                lastSelectedEntry = newEntry;
                
                // ⭐ 调试信息：新建条目的向量同步
                LogVectorSyncDebugInfo(newEntry);
            }
            else
            {
                // Edit existing
                lastSelectedEntry.tag = editTag;
                lastSelectedEntry.content = editContent;
                lastSelectedEntry.importance = editImportance;
                lastSelectedEntry.targetPawnId = editTargetPawnId;
                lastSelectedEntry.isUserEdited = true;
                lastSelectedEntry.excludeKeywords = excludes;
                lastSelectedEntry.matchMode = editMatchMode;
                
                // ⭐ 清除缓存，确保新标签生效
                lastSelectedEntry.InvalidateCache();
                
                // ⭐ 调试信息：编辑条目的向量同步
                if (RimTalkMemoryPatchMod.Settings.enableVectorEnhancement && lastSelectedEntry.isEnabled)
                {
                    try
                    {
                        VectorDB.VectorService.Instance.UpdateKnowledgeVector(lastSelectedEntry.id, lastSelectedEntry.content);
                        LogVectorSyncDebugInfo(lastSelectedEntry);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandMemory] Failed to update vector for entry {lastSelectedEntry.id}: {ex}");
                    }
                }
            }
            
            editMode = false;
            Messages.Message("RimTalk_Knowledge_EntrySaved".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }
        
        /// <summary>
        /// ⭐ 调试信息：记录向量同步的详细信息
        /// </summary>
        private void LogVectorSyncDebugInfo(CommonKnowledgeEntry entry)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
            {
                Log.Message($"[RimTalk-ExpandMemory] Vector enhancement is DISABLED. Entry '{entry.tag}' will NOT be vectorized.");
                return;
            }
            
            if (!entry.isEnabled)
            {
                Log.Message($"[RimTalk-ExpandMemory] Entry '{entry.tag}' is DISABLED. Will NOT be vectorized.");
                return;
            }
            
            try
            {
                // 1. 显示原始内容
                string contentPreview = entry.content.Length > 100 ? entry.content.Substring(0, 100) + "..." : entry.content;
                Log.Message($"[RimTalk-ExpandMemory] ========== Vector Sync Debug ==========");
                Log.Message($"[RimTalk-ExpandMemory] Entry ID: {entry.id}");
                Log.Message($"[RimTalk-ExpandMemory] Entry Tag: {entry.tag}");
                Log.Message($"[RimTalk-ExpandMemory] Content Preview: {contentPreview}");
                
                // 2. 模拟分词过程（调用 VectorService 的私有方法需要反射，这里简化显示）
                // 显示内容长度和基本信息
                Log.Message($"[RimTalk-ExpandMemory] Content Length: {entry.content.Length} characters");
                Log.Message($"[RimTalk-ExpandMemory] Content (lowercase): {entry.content.ToLowerInvariant().Substring(0, Math.Min(100, entry.content.Length))}");
                
                // 3. 尝试查找向量（验证是否成功存储）
                var vectorResults = VectorDB.VectorService.Instance.FindBestLoreIds(entry.content, 1, 0.99f);
                
                if (vectorResults != null && vectorResults.Count > 0 && vectorResults[0].id == entry.id)
                {
                    Log.Message($"[RimTalk-ExpandMemory] ✓ Vector FOUND in database! Similarity: {vectorResults[0].similarity:F4}");
                    Log.Message($"[RimTalk-ExpandMemory] ✓ Model is working correctly!");
                }
                else
                {
                    Log.Warning($"[RimTalk-ExpandMemory] ✗ Vector NOT FOUND in database!");
                    Log.Warning($"[RimTalk-ExpandMemory] ✗ This may indicate a problem with the vector model or tokenization.");
                    
                    // 显示实际找到的结果（如果有）
                    if (vectorResults != null && vectorResults.Count > 0)
                    {
                        Log.Message($"[RimTalk-ExpandMemory] Instead found: ID={vectorResults[0].id}, Similarity={vectorResults[0].similarity:F4}");
                    }
                    else
                    {
                        Log.Message($"[RimTalk-ExpandMemory] No similar vectors found at all.");
                    }
                }
                
                Log.Message($"[RimTalk-ExpandMemory] ========================================");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] Error in vector sync debug: {ex}");
            }
        }
        
        private void DeleteSelectedEntries()
        {
            if (selectedEntries.Count == 0) return;
            
            int count = selectedEntries.Count;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_Knowledge_DeleteConfirm".Translate(count),
                delegate
                {
                    foreach (var entry in selectedEntries.ToList())
                    {
                        library.RemoveEntry(entry);
                    }
                    selectedEntries.Clear();
                    lastSelectedEntry = null;
                    Messages.Message("RimTalk_Knowledge_Deleted".Translate(count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void ExportToFile()
        {
            string content;
            if (selectedEntries.Count > 0)
            {
                // Export selected
                var sb = new System.Text.StringBuilder();
                foreach (var entry in selectedEntries)
                {
                    sb.AppendLine(entry.FormatForExport());
                }
                content = sb.ToString();
            }
            else
            {
                // Export all
                content = library.ExportToText();
            }
            
            GUIUtility.systemCopyBuffer = content;
            Messages.Message("RimTalk_Knowledge_ExportedToClipboard".Translate(selectedEntries.Count > 0 ? selectedEntries.Count : library.Entries.Count), MessageTypeDefOf.PositiveEvent, false);
        }
        
        private void ShowImportDialog()
        {
            Find.WindowStack.Add(new Dialog_TextInput(
                "RimTalk_Knowledge_ImportTitle".Translate(),
                "RimTalk_Knowledge_ImportDescription".Translate(),
                "",
                delegate(string text)
                {
                    int count = library.ImportFromText(text);
                    Messages.Message("RimTalk_Knowledge_Imported".Translate(count), MessageTypeDefOf.PositiveEvent, false);
                },
                null,
                true
            ));
        }
        
        private void ClearAllEntries()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_Knowledge_ClearConfirm".Translate(),
                delegate
                {
                    library.Clear();
                    selectedEntries.Clear();
                    lastSelectedEntry = null;
                    Messages.Message("RimTalk_Knowledge_AllCleared".Translate(), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void ShowPawnSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            options.Add(new FloatMenuOption("RimTalk_Knowledge_GlobalAll".Translate(), delegate { editTargetPawnId = -1; }));
            
            var colonists = Find.Maps?.SelectMany(m => m.mapPawns.FreeColonists).ToList();
            if (colonists != null && colonists.Count > 0)
            {
                foreach (var pawn in colonists.OrderBy(p => p.LabelShort))
                {
                    int pawnId = pawn.thingIDNumber;
                    options.Add(new FloatMenuOption("RimTalk_Knowledge_ExclusiveTo".Translate(pawn.LabelShort), delegate { editTargetPawnId = pawnId; }));
                }
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        private void GeneratePawnStatusKnowledge()
        {
            try
            {
                var colonists = Find.CurrentMap?.mapPawns?.FreeColonists;
                if (colonists == null || colonists.Count() == 0)
                {
                    Messages.Message("RimTalk_Knowledge_NoColonists".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                int currentTick = Find.TickManager.TicksGame;
                int generated = 0;
                
                foreach (var pawn in colonists)
                {
                    try
                    {
                        PawnStatusKnowledgeGenerator.UpdatePawnStatusKnowledge(pawn, library, currentTick);
                        generated++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CommonKnowledge] Failed to generate for {pawn.Name}: {ex.Message}");
                    }
                }
                
                Messages.Message("RimTalk_Knowledge_GeneratedPawnStatus".Translate(generated), MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Messages.Message("RimTalk_Knowledge_GenerationFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
            }
        }
        
        private void GenerateEventRecordKnowledge()
        {
            try
            {
                EventRecordKnowledgeGenerator.ScanRecentPlayLog();
                Messages.Message("RimTalk_Knowledge_EventScanTriggered".Translate(), MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Messages.Message("RimTalk_Knowledge_GenerationFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
            }
        }
    }
    
    // ==================== Enums ====================
    
    public enum KnowledgeCategory
    {
        All,
        Instructions,
        Lore,
        PawnStatus,
        History,
        Other
    }
}
