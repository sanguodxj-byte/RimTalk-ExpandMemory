﻿using System;
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
    /// ★ v3.3.19: 完全重构 - 图书馆风格布局 + 拖拽多选支持 + 代码拆分
    /// </summary>
    public class Dialog_CommonKnowledge : Window
    {
        // ==================== Data & State ====================
        private CommonKnowledgeLibrary library;
        
        // Multi-select support
        private HashSet<CommonKnowledgeEntry> selectedEntries = new HashSet<CommonKnowledgeEntry>();
        private CommonKnowledgeEntry lastSelectedEntry = null;
        
        // Drag selection
        private bool isDragging = false;
        private bool isMouseDown = false;           // ⭐ 左键已按下但尚未判定为拖拽
        private Vector2 dragStartPos = Vector2.zero;
        private Vector2 dragCurrentPos = Vector2.zero;
        private Vector2 mouseDownScreenPos = Vector2.zero; // ⭐ 按下时的屏幕坐标（用于阈值判定）
        private const float DRAG_THRESHOLD = 5f;    // ⭐ 拖拽判定阈值（像素）
        
        // ⭐ 拖拽排序
        private bool isDragReordering = false;      // 是否处于拖拽排序模式
        private int dragReorderInsertIndex = -1;    // 插入位置索引
        private bool mouseDownOnSelected = false;   // 按下时是否在已选中条目上
        
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
        
        // ⭐ 虚拟化列表
        private VirtualListView<CommonKnowledgeEntry> virtualList;
        
        // Edit fields
        private string editTag = "";
        private string editContent = "";
        private float editImportance = 0.5f;
        private int editTargetPawnId = -1;
        private KeywordMatchMode editMatchMode = KeywordMatchMode.Any;
        private KnowledgeEntryCategory editCategory = KnowledgeEntryCategory.None;
        
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
            
            // ⭐ 初始化虚拟化列表
            virtualList = new VirtualListView<CommonKnowledgeEntry>(
                getItemHeight: (entry) => ENTRY_HEIGHT,
                drawItem: (rect, entry, index) => DrawEntryRow(rect, entry)
            );
            // 设置空列表提示
            virtualList.EmptyLabel = CommonKnowledgeTranslationKeys.SelectOrCreate.Translate();
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
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (isMouseDown && isDragReordering && dragReorderInsertIndex >= 0)
                {
                    // ⭐ 拖拽排序完成：将选中条目移动到目标位置
                    ExecuteDragReorder();
                }
                else if (isMouseDown && !isDragging)
                {
                    // ⭐ 没有进入拖拽模式 → 当作单击处理
                    // 找到鼠标下方的条目
                    float centerX2 = SIDEBAR_WIDTH + SPACING;
                    float centerWidth2 = showRightPanel ? 
                        (inRect.width - SIDEBAR_WIDTH - RIGHT_PANEL_WIDTH - SPACING * 3) : 
                        (inRect.width - SIDEBAR_WIDTH - SPACING * 2);
                    Rect centerRect2 = new Rect(centerX2, contentY, centerWidth2, contentHeight);
                    Rect innerRect2 = centerRect2.ContractedBy(5f);
                    
                    if (innerRect2.Contains(Event.current.mousePosition))
                    {
                        // 转换为 content 坐标
                        float mouseContentY = Event.current.mousePosition.y - innerRect2.y + listScrollPosition.y;
                        var filteredEntries2 = GetFilteredEntries();
                        int clickedIndex = (int)(mouseContentY / ENTRY_HEIGHT);
                        
                        if (clickedIndex >= 0 && clickedIndex < filteredEntries2.Count)
                        {
                            HandleEntryClick(filteredEntries2[clickedIndex]);
                        }
                        else
                        {
                            // 点击空白区域，清除选择
                            selectedEntries.Clear();
                            lastSelectedEntry = null;
                        }
                    }
                }
                
                isMouseDown = false;
                isDragging = false;
                isDragReordering = false;
                dragReorderInsertIndex = -1;
                mouseDownOnSelected = false;
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
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.ClearAll.Translate()))
            {
                ClearAllEntries();
            }
            
            // Delete Selected button
            rightX -= buttonWidth + spacing;
            GUI.enabled = selectedEntries.Count > 0;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.DeleteCount.Translate(selectedEntries.Count)))
            {
                DeleteSelectedEntries();
            }
            GUI.enabled = true;
            
            // Export button
            rightX -= buttonWidth + spacing;
            string exportLabel = selectedEntries.Count > 0 
                ? CommonKnowledgeTranslationKeys.ExportCount.Translate(selectedEntries.Count) 
                : CommonKnowledgeTranslationKeys.ExportAll.Translate();
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), exportLabel))
            {
                ExportToFile();
            }
            
            // Import button
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.Import.Translate()))
            {
                ShowImportDialog();
            }
            
            // New button
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.New.Translate()))
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
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 30f), 
                CommonKnowledgeTranslationKeys.Categories.Translate());
            Text.Font = GameFont.Small;
            y += 35f;
            
            // Category buttons
            float categoryHeight = 35f;
            foreach (KnowledgeCategory category in Enum.GetValues(typeof(KnowledgeCategory)))
            {
                bool isSelected = currentCategory == category;
                
                Rect categoryRect = new Rect(innerRect.x, y, innerRect.width, categoryHeight);
                int categoryCount = GetCategoryCount(category);
                
                // ⭐ 使用辅助方法绘制分类按钮
                if (CommonKnowledgeUIHelpers.DrawCategoryButton(categoryRect, category, isSelected, categoryCount))
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
            if (Widgets.ButtonText(autoGenHeaderRect, 
                $"{autoGenIcon} {CommonKnowledgeTranslationKeys.AutoGenerate.Translate()}"))
            {
                showAutoGenerateSettings = !showAutoGenerateSettings;
            }
            y += 35f;
            
            if (showAutoGenerateSettings)
            {
                Rect autoGenContentRect = new Rect(innerRect.x, y, innerRect.width, 120f);
                // ⭐ 使用辅助方法绘制自动生成设置
                CommonKnowledgeUIHelpers.DrawAutoGenerateSettings(
                    autoGenContentRect, 
                    GeneratePawnStatusKnowledge, 
                    GenerateEventRecordKnowledge
                );
                y += 125f;
            }
            
            // ⭐ 标签测试工具（按钮，点击弹窗）
            Rect tagTestButtonRect = new Rect(innerRect.x, y, innerRect.width, BUTTON_HEIGHT);
            GUI.color = new Color(0.4f, 0.8f, 0.4f); // 浅绿色
            if (Widgets.ButtonText(tagTestButtonRect, "🔍 " + CommonKnowledgeTranslationKeys.TagTest.Translate()))
            {
                ShowTagTestDialog();
            }
            GUI.color = Color.white;
            y += BUTTON_HEIGHT + 10f;
            
            // ⭐ 使用说明按钮（在统计信息上方）
            float helpButtonY = innerRect.yMax - 100f; // 为统计信息留出60px + 间距
            Rect helpButtonRect = new Rect(innerRect.x, helpButtonY, innerRect.width, BUTTON_HEIGHT);
            
            GUI.color = new Color(0.4f, 0.7f, 1f); // 浅蓝色
            if (Widgets.ButtonText(helpButtonRect, "📘 " + CommonKnowledgeTranslationKeys.Help.Translate()))
            {
                ShowHelpDialog();
            }
            GUI.color = Color.white;
            
            // Statistics at bottom
            float statsY = innerRect.yMax - 60f;
            Widgets.DrawLineHorizontal(innerRect.x, statsY - 10f, innerRect.width);
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            int totalCount = library.Entries.Count;
            int enabledCount = library.Entries.Count(e => e.isEnabled);
            int selectedCount = selectedEntries.Count;
            
            Widgets.Label(new Rect(innerRect.x, statsY, innerRect.width, 20f), 
                CommonKnowledgeTranslationKeys.Total.Translate(totalCount));
            Widgets.Label(new Rect(innerRect.x, statsY + 20f, innerRect.width, 20f), 
                CommonKnowledgeTranslationKeys.Enabled.Translate(enabledCount));
            Widgets.Label(new Rect(innerRect.x, statsY + 40f, innerRect.width, 20f), 
                CommonKnowledgeTranslationKeys.Selected.Translate(selectedCount));
            
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
            
            // ⭐ 更新虚拟列表数据
            virtualList.SetItems(filteredEntries);
            virtualList.ScrollPosition = listScrollPosition;
            
            // ⭐ 修复：不在这里处理拖拽，改为手动处理并在ScrollView内绘制选择框
            
            // ⭐ 使用虚拟化渲染
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, filteredEntries.Count * ENTRY_HEIGHT);
            
            // 处理拖拽事件（在BeginScrollView之前）
            HandleDragSelectionEvents(innerRect, viewRect);
            
            Widgets.BeginScrollView(innerRect, ref listScrollPosition, viewRect, true);
            
            // 绘制条目
            float y = 0f;
            foreach (var entry in filteredEntries)
            {
                Rect entryRect = new Rect(0f, y, viewRect.width, ENTRY_HEIGHT);
                DrawEntryRow(entryRect, entry);
                y += ENTRY_HEIGHT;
            }
            
            // ⭐ 修复：在EndScrollView之前绘制选择框或拖拽排序指示线
            if (isDragging)
            {
                DrawSelectionBox();
            }
            else if (isDragReordering && dragReorderInsertIndex >= 0)
            {
                // ⭐ 绘制拖拽排序的插入指示线
                float insertY = dragReorderInsertIndex * ENTRY_HEIGHT;
                Rect lineRect = new Rect(0f, insertY - 2f, viewRect.width, 4f);
                Widgets.DrawBoxSolid(lineRect, new Color(0.3f, 0.8f, 1f, 0.8f));
                
                // 绘制小三角指示
                Rect arrowRect = new Rect(-2f, insertY - 6f, 12f, 12f);
                Widgets.DrawBoxSolid(arrowRect, new Color(0.3f, 0.8f, 1f, 0.9f));
            }
            
            Widgets.EndScrollView();
            
            // 同步滚动位置
            virtualList.ScrollPosition = listScrollPosition;
            
            // Show filter status
            if (!string.IsNullOrEmpty(searchFilter) || currentCategory != KnowledgeCategory.All)
            {
                Rect statusRect = new Rect(innerRect.x, innerRect.yMax - 25f, innerRect.width, 20f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                
                // 显示过滤状态
                Widgets.Label(statusRect, 
                    CommonKnowledgeTranslationKeys.Showing.Translate(filteredEntries.Count, library.Entries.Count));
                
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
            
            // ⭐ 点击选择现在由 DoWindowContents 的 MouseUp 统一处理
            // 不再使用 ButtonInvisible（它会和拖拽框选冲突）
            
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
            // ⭐ 使用辅助方法获取分类颜色
            GUI.color = CommonKnowledgeUIHelpers.GetCategoryColor(entry);
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

        /// <summary>
        /// 执行拖拽排序：将选中的条目移动到 dragReorderInsertIndex 位置
        /// 操作的是 library.Entries（主列表），不是 filteredEntries
        /// </summary>
        private void ExecuteDragReorder()
        {
            var filteredEntries = GetFilteredEntries();
            if (filteredEntries.Count == 0 || selectedEntries.Count == 0) return;
            
            // 1. 确定插入目标在主列表中的位置
            int targetMainIndex;
            if (dragReorderInsertIndex >= filteredEntries.Count)
            {
                // 插入到末尾：找到最后一个过滤条目在主列表中的位置之后
                var lastFiltered = filteredEntries[filteredEntries.Count - 1];
                targetMainIndex = library.Entries.IndexOf(lastFiltered) + 1;
            }
            else if (dragReorderInsertIndex <= 0)
            {
                // 插入到开头：找到第一个过滤条目在主列表中的位置
                var firstFiltered = filteredEntries[0];
                targetMainIndex = library.Entries.IndexOf(firstFiltered);
            }
            else
            {
                // 插入到中间：找到目标位置条目在主列表中的位置
                var targetEntry = filteredEntries[dragReorderInsertIndex];
                targetMainIndex = library.Entries.IndexOf(targetEntry);
            }
            
            // 2. 收集选中的条目（保持原有顺序）
            var entriesToMove = new List<CommonKnowledgeEntry>();
            foreach (var entry in library.Entries)
            {
                if (selectedEntries.Contains(entry))
                {
                    entriesToMove.Add(entry);
                }
            }
            
            if (entriesToMove.Count == 0) return;
            
            // 3. 从主列表中移除选中的条目
            foreach (var entry in entriesToMove)
            {
                library.Entries.Remove(entry);
            }
            
            // 4. 重新计算插入位置（因为移除了条目，索引可能变化）
            // 找到目标位置：如果目标条目还在列表中，插入到它前面
            if (dragReorderInsertIndex < filteredEntries.Count)
            {
                var targetEntry = filteredEntries[dragReorderInsertIndex];
                int newIdx = library.Entries.IndexOf(targetEntry);
                if (newIdx >= 0)
                {
                    targetMainIndex = newIdx;
                }
                else
                {
                    targetMainIndex = library.Entries.Count;
                }
            }
            else
            {
                targetMainIndex = library.Entries.Count;
            }
            
            // 5. 在目标位置插入
            targetMainIndex = Mathf.Clamp(targetMainIndex, 0, library.Entries.Count);
            library.Entries.InsertRange(targetMainIndex, entriesToMove);
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
        
        // ⭐ 重写：鼠标事件处理（左键点选 + 拖拽框选共存）
        // MouseDown → 记录起始位置，不吞事件
        // MouseDrag → 超过阈值才进入框选模式
        // MouseUp → 未进入框选则当作单击
        private void HandleDragSelectionEvents(Rect listRect, Rect viewRect)
        {
            Event e = Event.current;

            // 左键按下：记录起始位置，判断是否在已选中条目上
            if (e.type == EventType.MouseDown && e.button == 0 && listRect.Contains(e.mousePosition))
            {
                isMouseDown = true;
                mouseDownScreenPos = e.mousePosition;
                dragStartPos = e.mousePosition - listRect.position;
                dragCurrentPos = dragStartPos;

                // ⭐ 判断按下位置是否在已选中的条目上
                Vector2 localPos = e.mousePosition - listRect.position;
                float mouseContentY = localPos.y + listScrollPosition.y;
                var filteredEntries = GetFilteredEntries();
                int clickedIdx = (int)(mouseContentY / ENTRY_HEIGHT);

                mouseDownOnSelected = false;
                if (clickedIdx >= 0 && clickedIdx < filteredEntries.Count)
                {
                    mouseDownOnSelected = selectedEntries.Contains(filteredEntries[clickedIdx]);
                }
            }

            // 鼠标移动中：判断是否超过拖拽阈值
            if (isMouseDown && e.type == EventType.MouseDrag && e.button == 0)
            {
                float distance = Vector2.Distance(mouseDownScreenPos, e.mousePosition);

                if (!isDragging && !isDragReordering && distance >= DRAG_THRESHOLD)
                {
                    if (mouseDownOnSelected && selectedEntries.Count > 0)
                    {
                        // ⭐ 在已选中条目上拖拽 → 拖拽排序模式
                        isDragReordering = true;
                    }
                    else
                    {
                        // ⭐ 在未选中区域拖拽 → 框选模式
                        isDragging = true;
                    }
                }

                if (isDragReordering)
                {
                    // ⭐ 拖拽排序：计算插入位置
                    Vector2 localMousePos = e.mousePosition - listRect.position;
                    float contentMouseY = localMousePos.y + listScrollPosition.y;
                    var filteredEntries = GetFilteredEntries();

                    // 计算最近的插入线位置（在条目之间）
                    dragReorderInsertIndex = Mathf.Clamp(
                        Mathf.RoundToInt(contentMouseY / ENTRY_HEIGHT),
                        0, filteredEntries.Count);

                    e.Use();
                }
                else if (isDragging)
                {
                    dragCurrentPos = e.mousePosition - listRect.position;

                    // 转换为content坐标
                    Rect selectionBoxViewport = GetSelectionBox();
                    Rect selectionBoxContent = new Rect(
                        selectionBoxViewport.x,
                        selectionBoxViewport.y + listScrollPosition.y,
                        selectionBoxViewport.width,
                        selectionBoxViewport.height
                    );

                    var filteredEntries = GetFilteredEntries();

                    bool ctrl = Event.current.control;
                    if (!ctrl)
                    {
                        selectedEntries.Clear();
                    }

                    // 检查哪些条目在选择框内
                    float y = 0f;
                    foreach (var entry in filteredEntries)
                    {
                        Rect entryRect = new Rect(0f, y, viewRect.width, ENTRY_HEIGHT);

                        if (selectionBoxContent.Overlaps(entryRect))
                        {
                            selectedEntries.Add(entry);
                        }

                        y += ENTRY_HEIGHT;
                    }

                    e.Use();
                }
            }
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
            Widgets.Label(rect, CommonKnowledgeTranslationKeys.SelectOrCreate.Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        private void DrawMultiSelectionPanel(Rect rect)
        {
            float y = rect.y;
            
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), 
                CommonKnowledgeTranslationKeys.ItemsSelected.Translate(selectedEntries.Count));
            Text.Font = GameFont.Small;
            y += 40f;
            
            // Statistics
            int enabledCount = selectedEntries.Count(e => e.isEnabled);
            int disabledCount = selectedEntries.Count - enabledCount;
            float avgImportance = selectedEntries.Average(e => e.importance);
            
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), 
                CommonKnowledgeTranslationKeys.EnabledCount.Translate(enabledCount));
            y += 25f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), 
                CommonKnowledgeTranslationKeys.DisabledCount.Translate(disabledCount));
            y += 25f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), 
                CommonKnowledgeTranslationKeys.AvgImportance.Translate(avgImportance.ToString("F2")));
            y += 40f;
            
            // Batch actions
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.EnableAll.Translate()))
            {
                foreach (var entry in selectedEntries)
                    entry.isEnabled = true;
            }
            y += BUTTON_HEIGHT + 5f;
            
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.DisableAll.Translate()))
            {
                foreach (var entry in selectedEntries)
                    entry.isEnabled = false;
            }
            y += BUTTON_HEIGHT + 5f;
            
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.ExportItems.Translate(selectedEntries.Count)))
            {
                ExportToFile();
            }
            y += BUTTON_HEIGHT + 5f;
            
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.DeleteItems.Translate(selectedEntries.Count)))
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
            Widgets.Label(new Rect(rect.x, y, rect.width - 100f, 30f), 
                CommonKnowledgeTranslationKeys.Details.Translate());
            Text.Font = GameFont.Small;
            
            // Edit button
            if (Widgets.ButtonText(new Rect(rect.xMax - 90f, y, 90f, 28f), 
                CommonKnowledgeTranslationKeys.Edit.Translate()))
            {
                StartEdit();
            }
            y += 40f;
            
            // Scrollable content
            Rect scrollOuterRect = new Rect(rect.x, y, rect.width, rect.height - y - 50f);
            float scrollHeight = 500f;
            Rect scrollViewRect = new Rect(0f, 0f, rect.width - 16f, scrollHeight);
            
            Widgets.BeginScrollView(scrollOuterRect, ref detailScrollPosition, scrollViewRect);
        
            float scrollY = 0f;
            
            // Tag
            CommonKnowledgeUIHelpers.DrawDetailField(
                new Rect(0f, scrollY, scrollViewRect.width, 50f), 
                CommonKnowledgeTranslationKeys.Tag.Translate(), 
                entry.tag
            );
            scrollY += 55f;
            
            // ⭐ Category
            var entryCat = CommonKnowledgeUIHelpers.GetEntryCategory(entry);
            string catDisplay = CommonKnowledgeUIHelpers.GetCategoryLabel(entryCat);
            if (entry.category == KnowledgeEntryCategory.None)
                catDisplay += " (自动)";
            CommonKnowledgeUIHelpers.DrawDetailField(
                new Rect(0f, scrollY, scrollViewRect.width, 25f),
                "分类",
                catDisplay
            );
            scrollY += 30f;
            
            // Importance
            CommonKnowledgeUIHelpers.DrawDetailField(
                new Rect(0f, scrollY, scrollViewRect.width, 25f), 
                CommonKnowledgeTranslationKeys.Importance.Translate(), 
                entry.importance.ToString("F1")
            );
            scrollY += 30f;
            
            // Status
            string status = entry.isEnabled 
                ? CommonKnowledgeTranslationKeys.StatusEnabled.Translate() 
                : CommonKnowledgeTranslationKeys.StatusDisabled.Translate();
            CommonKnowledgeUIHelpers.DrawDetailField(
                new Rect(0f, scrollY, scrollViewRect.width, 25f), 
                CommonKnowledgeTranslationKeys.Status.Translate(), 
                status
            );
            scrollY += 30f;
            
            // Visibility
            string visibility = CommonKnowledgeUIHelpers.GetVisibilityText(entry);
            CommonKnowledgeUIHelpers.DrawDetailField(
                new Rect(0f, scrollY, scrollViewRect.width, 50f), 
                CommonKnowledgeTranslationKeys.Visibility.Translate(), 
                visibility
            );
            scrollY += 55f;
            
            // Content
            Widgets.Label(new Rect(0f, scrollY, scrollViewRect.width, 20f), 
                CommonKnowledgeTranslationKeys.Content.Translate() + ":");
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
            if (Widgets.ButtonText(deleteRect, CommonKnowledgeTranslationKeys.Delete.Translate()))
            {
                DeleteSelectedEntries();
            }
            GUI.color = Color.white;
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
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), 
                lastSelectedEntry == null 
                    ? CommonKnowledgeTranslationKeys.NewEntry.Translate() 
                    : CommonKnowledgeTranslationKeys.EditEntry.Translate());
            Text.Font = GameFont.Small;
            y += 40f;
            
            // Tag
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), 
                CommonKnowledgeUIHelpers.EnsureColon(CommonKnowledgeTranslationKeys.Tag.Translate()));
            editTag = Widgets.TextField(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), editTag);
            y += 30f;
            
            // ⭐ Category dropdown (after tag)
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), "分类:");
            string catLabel = CommonKnowledgeUIHelpers.GetExplicitCategoryLabel(editCategory);
            // 显示推断提示
            if (editCategory == KnowledgeEntryCategory.None && !string.IsNullOrEmpty(editTag))
            {
                var inferred = CommonKnowledgeUIHelpers.GetEntryCategory(
                    new CommonKnowledgeEntry(editTag, "") { category = KnowledgeEntryCategory.None });
                string inferredName = CommonKnowledgeUIHelpers.GetCategoryLabel(inferred);
                catLabel = "自动推断 → " + inferredName;
            }
            if (Widgets.ButtonText(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), catLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (KnowledgeEntryCategory cat in Enum.GetValues(typeof(KnowledgeEntryCategory)))
                {
                    KnowledgeEntryCategory localCat = cat;
                    options.Add(new FloatMenuOption(
                        CommonKnowledgeUIHelpers.GetExplicitCategoryLabel(cat),
                        delegate { editCategory = localCat; }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 30f;
            
            // Importance
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), 
                CommonKnowledgeUIHelpers.EnsureColon(CommonKnowledgeTranslationKeys.Importance.Translate()));
            editImportance = Widgets.HorizontalSlider(
                new Rect(rect.x + 100f, y, rect.width - 150f, 25f), 
                editImportance, 0f, 1f
            );
            Widgets.Label(new Rect(rect.xMax - 40f, y, 40f, 25f), editImportance.ToString("F1"));
            y += 30f;
            
            // Pawn selection (simplified)
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), 
                CommonKnowledgeUIHelpers.EnsureColon(CommonKnowledgeTranslationKeys.Visibility.Translate()));
            string pawnLabel = editTargetPawnId == -1 
                ? CommonKnowledgeTranslationKeys.Global.Translate().ToString() 
                : $"Pawn #{editTargetPawnId}";
            if (Widgets.ButtonText(new Rect(rect.x + 100f, y, rect.width - 100f, 25f), pawnLabel))
            {
                // ⭐ 使用辅助方法显示Pawn选择菜单
                CommonKnowledgeUIHelpers.ShowPawnSelectionMenu(pawnId => editTargetPawnId = pawnId);
            }
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
            Widgets.Label(new Rect(rect.x, y, 100f, 25f), 
                CommonKnowledgeUIHelpers.EnsureColon(CommonKnowledgeTranslationKeys.Content.Translate()));
            y += 27f;
            
            float contentHeight = rect.yMax - y - 80f;
            Rect contentRect = new Rect(rect.x, y, rect.width, contentHeight);
            Widgets.DrawBoxSolid(contentRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Rect textAreaRect = contentRect.ContractedBy(5f);
            editContent = GUI.TextArea(textAreaRect, editContent ?? "");
            y += contentHeight + 10f;
            
            // Buttons
            float buttonY = rect.yMax - 65f;
            if (Widgets.ButtonText(new Rect(rect.x, buttonY, (rect.width - 5f) / 2f, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.Save.Translate()))
            {
                SaveEntry();
            }
            
            if (Widgets.ButtonText(new Rect(rect.x + (rect.width + 5f) / 2f, buttonY, (rect.width - 5f) / 2f, BUTTON_HEIGHT), 
                CommonKnowledgeTranslationKeys.Cancel.Translate()))
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
                entries = entries.Where(e => CommonKnowledgeUIHelpers.GetEntryCategory(e) == currentCategory);
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
            
            return library.Entries.Count(e => CommonKnowledgeUIHelpers.GetEntryCategory(e) == category);
        }
        
        private Color GetCategoryColor(CommonKnowledgeEntry entry)
        {
            var category = CommonKnowledgeUIHelpers.GetEntryCategory(entry);
            switch (category)
            {
                case KnowledgeCategory.Instructions: return new Color(0.3f, 0.8f, 0.3f);
                case KnowledgeCategory.Lore: return new Color(0.8f, 0.6f, 0.3f);
                case KnowledgeCategory.PawnStatus: return new Color(0.3f, 0.6f, 0.9f);
                case KnowledgeCategory.History: return new Color(0.7f, 0.5f, 0.7f);
                default: return Color.white;
            }
        }
        
        private void CreateNewEntry()
        {
            editTag = "";
            editContent = "";
            editImportance = 0.5f;
            editTargetPawnId = -1;
            editMatchMode = KeywordMatchMode.Any;
            editCategory = KnowledgeEntryCategory.None;
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
                editMatchMode = entry.matchMode;
                editCategory = entry.category;
                lastSelectedEntry = entry;
                editMode = true;
            }
        }
        
        private void SaveEntry()
        {
            if (string.IsNullOrEmpty(editTag) || string.IsNullOrEmpty(editContent))
            {
                Messages.Message(CommonKnowledgeTranslationKeys.TagContentEmpty.Translate(), 
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            if (lastSelectedEntry == null)
            {
                // Create new - 只在创建时设置 isUserEdited
                var newEntry = new CommonKnowledgeEntry(editTag, editContent)
                {
                    importance = editImportance,
                    targetPawnId = editTargetPawnId,
                    isUserEdited = true,  // ⭐ 新创建的条目标记为用户编辑
                    matchMode = editMatchMode,
                    category = editCategory
                };
                library.AddEntry(newEntry);
                selectedEntries.Clear();
                selectedEntries.Add(newEntry);
                lastSelectedEntry = newEntry;
            }
            else
            {
                // Edit existing - 保存时不再修改 isUserEdited 标记
                lastSelectedEntry.tag = editTag;
                lastSelectedEntry.content = editContent;
                lastSelectedEntry.importance = editImportance;
                lastSelectedEntry.targetPawnId = editTargetPawnId;
                // ⭐ 移除：不再每次保存都设置 isUserEdited = true
                // lastSelectedEntry.isUserEdited = true;
                lastSelectedEntry.matchMode = editMatchMode;
                lastSelectedEntry.category = editCategory;
                
                // 清除缓存，确保新标签生效
                lastSelectedEntry.InvalidateCache();
            }
            
            editMode = false;
            Messages.Message(CommonKnowledgeTranslationKeys.EntrySaved.Translate(), 
                MessageTypeDefOf.PositiveEvent, false);
        }
        
        private void DeleteSelectedEntries()
        {
            if (selectedEntries.Count == 0) return;
            
            int count = selectedEntries.Count;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                CommonKnowledgeTranslationKeys.DeleteConfirm.Translate(count),
                delegate
                {
                    foreach (var entry in selectedEntries.ToList())
                    {
                        library.RemoveEntry(entry);
                    }
                    selectedEntries.Clear();
                    lastSelectedEntry = null;
                    Messages.Message(CommonKnowledgeTranslationKeys.Deleted.Translate(count), 
                        MessageTypeDefOf.PositiveEvent, false);
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
            int exportCount = selectedEntries.Count > 0 ? selectedEntries.Count : library.Entries.Count;
            Messages.Message(CommonKnowledgeTranslationKeys.ExportedToClipboard.Translate(exportCount), 
                MessageTypeDefOf.PositiveEvent, false);
        }
        
        private void ShowImportDialog()
        {
            Find.WindowStack.Add(new Dialog_TextInput(
                CommonKnowledgeTranslationKeys.ImportTitle.Translate(),
                CommonKnowledgeTranslationKeys.ImportDescription.Translate(),
                "",
                delegate(string text)
                {
                    int count = library.ImportFromText(text);
                    Messages.Message(CommonKnowledgeTranslationKeys.Imported.Translate(count), 
                        MessageTypeDefOf.PositiveEvent, false);
                },
                null,
                true
            ));
        }
        
        private void ClearAllEntries()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                CommonKnowledgeTranslationKeys.ClearConfirm.Translate(),
                delegate
                {
                    library.Clear();
                    selectedEntries.Clear();
                    lastSelectedEntry = null;
                    Messages.Message(CommonKnowledgeTranslationKeys.AllCleared.Translate(), 
                        MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void GeneratePawnStatusKnowledge()
        {
            try
            {
                var colonists = Find.CurrentMap?.mapPawns?.FreeColonists;
                if (colonists == null || colonists.Count() == 0)
                {
                    Messages.Message(CommonKnowledgeTranslationKeys.NoColonists.Translate(), 
                        MessageTypeDefOf.RejectInput, false);
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
                
                Messages.Message(CommonKnowledgeTranslationKeys.GeneratedPawnStatus.Translate(generated), 
                    MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Messages.Message(CommonKnowledgeTranslationKeys.GenerationFailed.Translate(ex.Message), 
                    MessageTypeDefOf.RejectInput, false);
            }
        }
        
        private void GenerateEventRecordKnowledge()
        {
            try
            {
                EventRecordKnowledgeGenerator.ScanRecentPlayLog();
                Messages.Message(CommonKnowledgeTranslationKeys.EventScanTriggered.Translate(), 
                    MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Messages.Message(CommonKnowledgeTranslationKeys.GenerationFailed.Translate(ex.Message), 
                    MessageTypeDefOf.RejectInput, false);
            }
        }
        
        private void ShowHelpDialog()
        {
            string title = CommonKnowledgeTranslationKeys.HelpTitle.Translate();
            string content = CommonKnowledgeTranslationKeys.HelpContent.Translate();
            
            Find.WindowStack.Add(new Dialog_MessageBox(
                content,
                null,
                null,
                null,
                null,
                title
            ));
        }
        
        private void ShowTagTestDialog()
        {
            Find.WindowStack.Add(new Dialog_TagTest());
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
