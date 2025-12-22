using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using RimTalk.Memory;
using System.Linq;
using RimTalk.MemoryPatch;
using System;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// Mind Stream Timeline - Multi-Select Memory Cards
    /// ★ v3.3.19: 完全重构 - 时间线卡片布局 + 拖拽多选 + 批量操作
    /// ★ v3.3.32: 性能优化 - GetFilteredMemories缓存机制
    /// </summary>
    public partial class MainTabWindow_Memory : MainTabWindow
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
        
        // ⭐ v3.3.32: Filtered memories cache
        private List<MemoryEntry> cachedFilteredMemories;
        private bool filtersDirty = true;
        
        // Layout constants
        private const float TOP_BAR_HEIGHT = 50f;
        private const float CONTROL_PANEL_WIDTH = 220f;
        private const float SPACING = 10f;
        private const float CARD_WIDTH_FULL = 600f;
        private const float CARD_SPACING = 8f;
        
        // ⭐ 性能优化：缓存数据
        private List<MemoryEntry> cachedMemories = new List<MemoryEntry>();
        private List<float> cachedCardHeights = new List<float>();
        private List<float> cachedCardYPositions = new List<float>();
        private float cachedTotalHeight = 0f;
        
        // 脏检查状态 (用于检测是否需要刷新缓存)
        private int lastMemoryCount = -1;
        private bool lastShowABM;
        private bool lastShowSCM;
        private bool lastShowELS;
        private bool lastShowCLPA;
        private MemoryType? lastFilterType;
        private Pawn lastSelectedPawn;
        private int lastRefreshTick = -1;
        
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
            
            // ⭐ 在绘制任何子组件之前刷新缓存
            CheckAndRefreshCache();
            
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
            
            // Preview button (最右侧)
            rightX -= buttonWidth;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, 35f), "RimTalk_MindStream_Preview".Translate()))
            {
                Find.WindowStack.Add(new Debug.Dialog_InjectionPreview());
            }
            
            // ⭐ 新增：总结提示词按钮
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, 35f), "总结提示词"))
            {
                Find.WindowStack.Add(new Dialog_PromptEditor());
            }
            
            // 常识按钮
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, 35f), "RimTalk_MindStream_Knowledge".Translate()))
            {
                OpenCommonKnowledgeDialog();
            }

            // 操作指南按钮
            rightX -= buttonWidth + spacing;
            if (Widgets.ButtonText(new Rect(rightX, innerRect.y + 5f, buttonWidth, 35f), "RimTalk_MindStream_OperationGuide".Translate()))
            {
                ShowOperationGuide();
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
                        filtersDirty = true; // ⭐ v3.3.32: Mark cache dirty when pawn changes
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // Auto-select
            if (selectedPawn == null && colonists.Count > 0)
            {
                selectedPawn = colonists[0];
                filtersDirty = true; // ⭐ v3.3.32: Mark cache dirty on first selection
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
            
            // ⭐ v3.3.32: Store previous values to detect changes
            bool prevShowABM = showABM;
            bool prevShowSCM = showSCM;
            bool prevShowELS = showELS;
            bool prevShowCLPA = showCLPA;
            
            // ABM
            Rect abmRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color abmColor = new Color(0.3f, 0.8f, 1f); // Cyan
            DrawColoredCheckbox(abmRect, "RimTalk_MindStream_ABM".Translate(), ref showABM, abmColor, MemoryLayer.Active);
            y += checkboxHeight + 2f;
            
            // SCM - ⭐ 带右键菜单
            Rect scmRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color scmColor = new Color(0.3f, 1f, 0.5f); // Green
            DrawColoredCheckbox(scmRect, "RimTalk_MindStream_SCM".Translate(), ref showSCM, scmColor, MemoryLayer.Situational);
            y += checkboxHeight + 2f;
            
            // ELS - ⭐ 带右键菜单
            Rect elsRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color elsColor = new Color(1f, 0.8f, 0.3f); // Yellow
            DrawColoredCheckbox(elsRect, "RimTalk_MindStream_ELS".Translate(), ref showELS, elsColor, MemoryLayer.EventLog);
            y += checkboxHeight + 2f;
            
            // CLPA - ⭐ 带右键菜单
            Rect clpaRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color clpaColor = new Color(0.8f, 0.4f, 1f); // Purple
            DrawColoredCheckbox(clpaRect, "RimTalk_MindStream_CLPA".Translate(), ref showCLPA, clpaColor, MemoryLayer.Archive);
            y += checkboxHeight;
            
            // ⭐ v3.3.32: Mark cache dirty if any filter changed
            if (showABM != prevShowABM || showSCM != prevShowSCM || showELS != prevShowELS || showCLPA != prevShowCLPA)
            {
                filtersDirty = true;
            }
            
            return y;
        }
        
        private void DrawColoredCheckbox(Rect rect, string label, ref bool value, Color color, MemoryLayer? rightClickLayer)
        {
            // ⭐ 右键检测（如果指定了层级）- 在绘制之前
            if (rightClickLayer.HasValue)
            {
                if (Event.current.type == EventType.MouseDown && 
                    Event.current.button == 1 && 
                    rect.Contains(Event.current.mousePosition))
                {
                    ShowCreateMemoryMenu(rightClickLayer.Value);
                    Event.current.Use();
                    return; // 不继续绘制复选框，避免状态变化
                }
            }
            
            // Colored indicator
            Rect colorRect = new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f);
            Widgets.DrawBoxSolid(colorRect, color);
            
            // Checkbox
            Rect checkboxRect = new Rect(rect.x + 8f, rect.y, rect.width - 8f, rect.height);
            Widgets.CheckboxLabeled(checkboxRect, label, ref value);
            
            // ⭐ 添加工具提示提示用户可以右键
            if (rightClickLayer.HasValue && Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, $"右键点击新建{GetLayerLabel(rightClickLayer.Value)}记忆");
            }
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
                if (filterType != null) // ⭐ v3.3.32: Only mark dirty if actually changed
                {
                    filterType = null;
                    selectedMemories.Clear();
                    filtersDirty = true;
                }
            }
            GUI.color = Color.white;
            y += buttonHeight + spacing;
            
            // Conversation
            bool isConvSelected = filterType == MemoryType.Conversation;
            if (isConvSelected)
                GUI.color = new Color(0.5f, 0.7f, 1f);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_Conversation".Translate()))
            {
                if (filterType != MemoryType.Conversation) // ⭐ v3.3.32: Only mark dirty if actually changed
                {
                    filterType = MemoryType.Conversation;
                    selectedMemories.Clear();
                    filtersDirty = true;
                }
            }
            GUI.color = Color.white;
            y += buttonHeight + spacing;
            
            // Action
            bool isActionSelected = filterType == MemoryType.Action;
            if (isActionSelected)
                GUI.color = new Color(0.5f, 0.7f, 1f);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), "RimTalk_MindStream_Action".Translate()))
            {
                if (filterType != MemoryType.Action) // ⭐ v3.3.32: Only mark dirty if actually changed
                {
                    filterType = MemoryType.Action;
                    selectedMemories.Clear();
                    filtersDirty = true;
                }
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
            
            // ⭐ 如果没有选中，则操作对象为当前页面所有可见记忆
            // 优先使用缓存的列表
            var targetMemories = hasSelection ? selectedMemories.ToList() : cachedMemories;
            int targetCount = targetMemories.Count;
            
            // ⭐ 修复：总结按钮现在支持 ABM + SCM
            int abmCount = targetMemories.Count(m => m.layer == MemoryLayer.Active);
            int scmCount = targetMemories.Count(m => m.layer == MemoryLayer.Situational);
            int summarizableCount = abmCount + scmCount;
            
            GUI.enabled = summarizableCount > 0;
            string summarizeLabel;
            if (hasSelection)
            {
                if (abmCount > 0 && scmCount > 0)
                {
                    summarizeLabel = $"总结选中 (ABM:{abmCount} + SCM:{scmCount})";
                }
                else if (abmCount > 0)
                {
                    summarizeLabel = $"总结选中 (ABM:{abmCount})";
                }
                else
                {
                    summarizeLabel = $"总结选中 (SCM:{scmCount})";
                }
            }
            else
            {
                if (abmCount > 0 && scmCount > 0)
                {
                    summarizeLabel = $"总结全部 (ABM:{abmCount} + SCM:{scmCount})";
                }
                else if (abmCount > 0)
                {
                    summarizeLabel = $"总结全部 (ABM:{abmCount})";
                }
                else
                {
                    summarizeLabel = $"总结全部 (SCM:{scmCount})";
                }
            }
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), summarizeLabel))
            {
                SummarizeMemories(targetMemories);
            }
            GUI.enabled = true;
            y += buttonHeight + spacing;
            
            // Archive Selected/All (ELS -> CLPA)
            int elsCount = targetMemories.Count(m => m.layer == MemoryLayer.EventLog);
            GUI.enabled = elsCount > 0;
            string archiveLabel;
            if (hasSelection)
            {
                archiveLabel = "RimTalk_MindStream_ArchiveN".Translate(targetCount).ToString();
            }
            else
            {
                archiveLabel = $"归档全部 ({elsCount})";
            }
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), archiveLabel))
            {
                ArchiveMemories(targetMemories);
            }
            GUI.enabled = true;
            y += buttonHeight + spacing;
            
            // Delete Selected/All
            GUI.enabled = targetCount > 0;
            GUI.color = targetCount > 0 ? new Color(1f, 0.4f, 0.4f) : Color.white;
            string deleteLabel;
            if (hasSelection)
            {
                deleteLabel = "RimTalk_MindStream_DeleteN".Translate(targetCount).ToString();
            }
            else
            {
                deleteLabel = $"删除全部 ({targetCount})";
            }
            if (Widgets.ButtonText(new Rect(parentRect.x, y, parentRect.width, buttonHeight), deleteLabel))
            {
                DeleteMemories(targetMemories);
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
            y += buttonHeight + spacing * 2;
            
            // ⭐ 导出/导入按钮（并排显示）
            float halfWidth = (parentRect.width - spacing) / 2f;
            
            // Export button (left)
            GUI.color = new Color(0.5f, 0.8f, 1f);
            if (Widgets.ButtonText(new Rect(parentRect.x, y, halfWidth, buttonHeight), "RimTalk_Memory_Export".Translate()))
            {
                ExportMemories();
            }
            
            // Import button (right)
            GUI.color = new Color(0.8f, 1f, 0.5f);
            if (Widgets.ButtonText(new Rect(parentRect.x + halfWidth + spacing, y, halfWidth, buttonHeight), "RimTalk_Memory_Import".Translate()))
            {
                ImportMemories();
            }
            GUI.color = Color.white;
        }

        // ==================== Timeline ====================
        
        private void DrawTimeline(Rect rect)
        {
            if (currentMemoryComp == null)
                return;
            
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(5f);
            
            // 使用缓存的数据 (已在 DoWindowContents 中刷新)
            var memories = cachedMemories;
            float totalHeight = cachedTotalHeight;
            
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, totalHeight);
            
            // Handle drag selection
            HandleDragSelection(innerRect, viewRect);
            
            // Draw timeline
            Widgets.BeginScrollView(innerRect, ref timelineScrollPosition, viewRect, true);
            
            // ⭐ 高性能虚拟化：二分查找 + 提前退出
            // 添加缓冲区以确保滚动平滑
            float minVisibleY = timelineScrollPosition.y - 200f;
            float maxVisibleY = timelineScrollPosition.y + innerRect.height + 200f;
            
            int startIndex = 0;
            if (cachedCardYPositions.Count > 0)
            {
                // 使用二分查找快速定位第一个可见元素
                int binaryResult = cachedCardYPositions.BinarySearch(minVisibleY);
                if (binaryResult >= 0)
                {
                    startIndex = binaryResult;
                }
                else
                {
                    // 如果没找到精确匹配，BinarySearch返回按位取反的下一个较大元素索引
                    // 我们取它的前一个作为起始点
                    startIndex = Mathf.Max(0, (~binaryResult) - 1);
                }
            }
            
            for (int i = startIndex; i < memories.Count; i++)
            {
                float y = cachedCardYPositions[i];
                
                // 优化：一旦超出可见范围，立即停止绘制
                if (y > maxVisibleY)
                {
                    break;
                }
                
                var memory = memories[i];
                float height = cachedCardHeights[i];
                
                Rect cardRect = new Rect(0f, y, viewRect.width, height);
                DrawMemoryCard(cardRect, memory);
            }
            
            // ⭐ 修复：在EndScrollView之前绘制选择框，使其在正确的坐标系中
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
        
        private void CheckAndRefreshCache()
        {
            if (currentMemoryComp == null) return;
            
            // 获取当前状态
            int currentCount = currentMemoryComp.ActiveMemories.Count + 
                             currentMemoryComp.SituationalMemories.Count + 
                             currentMemoryComp.EventLogMemories.Count + 
                             currentMemoryComp.ArchiveMemories.Count;
            
            int currentTick = Find.TickManager.TicksGame;
            
            // 检查是否需要刷新
            bool needRefresh = false;
            
            if (selectedPawn != lastSelectedPawn) needRefresh = true;
            else if (currentCount != lastMemoryCount) needRefresh = true;
            else if (showABM != lastShowABM) needRefresh = true;
            else if (showSCM != lastShowSCM) needRefresh = true;
            else if (showELS != lastShowELS) needRefresh = true;
            else if (showCLPA != lastShowCLPA) needRefresh = true;
            else if (filterType != lastFilterType) needRefresh = true;
            else if (currentTick - lastRefreshTick > 60) needRefresh = true; // 每秒强制刷新一次以防内容变化
            
            if (needRefresh)
            {
                RefreshCache(currentCount, currentTick);
            }
        }
        
        private void RefreshCache(int currentCount, int currentTick)
        {
            // 更新状态记录
            lastSelectedPawn = selectedPawn;
            lastMemoryCount = currentCount;
            lastShowABM = showABM;
            lastShowSCM = showSCM;
            lastShowELS = showELS;
            lastShowCLPA = showCLPA;
            lastFilterType = filterType;
            lastRefreshTick = currentTick;
            
            // 重新获取过滤后的列表
            cachedMemories = GetFilteredMemories();
            
            // 重新计算高度和位置
            cachedCardHeights.Clear();
            cachedCardYPositions.Clear();
            cachedTotalHeight = 0f;
            
            foreach (var memory in cachedMemories)
            {
                cachedCardYPositions.Add(cachedTotalHeight);
                float height = GetCardHeight(memory.layer);
                cachedCardHeights.Add(height);
                cachedTotalHeight += height + CARD_SPACING;
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
                // ⭐ v3.3.32: No need to mark dirty for pin/unpin as it doesn't affect filtering
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
                    // ⭐ v3.3.32: Mark dirty when opening edit dialog
                    // User might change layer or type which affects filtering
                    filtersDirty = true;
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
            
            // ⭐ 修复：在viewport坐标系中开始拖拽
            if (e.type == EventType.MouseDown && e.button == 0 && listRect.Contains(e.mousePosition))
            {
                isDragging = true;
                // 转换为viewport坐标（相对于listRect）
                dragStartPos = e.mousePosition - listRect.position;
                dragCurrentPos = dragStartPos;
                e.Use();
            }
            
            if (isDragging && e.type == EventType.MouseDrag)
            {
                // ⭐ 修复：同样在viewport坐标系中
                dragCurrentPos = e.mousePosition - listRect.position;
                
                // Select memories that intersect with drag box
                // ⭐ 修复：转换为content坐标（加上scroll offset）
                Rect selectionBoxViewport = GetSelectionBox();
                Rect selectionBoxContent = new Rect(
                    selectionBoxViewport.x, 
                    selectionBoxViewport.y + timelineScrollPosition.y,
                    selectionBoxViewport.width,
                    selectionBoxViewport.height
                );
                
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
                    
                    if (selectionBoxContent.Overlaps(cardRect))
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
        
        private void SummarizeMemories(List<MemoryEntry> targetMemories)
        {
            if (currentMemoryComp == null || targetMemories == null || targetMemories.Count == 0)
                return;
            
            // ⭐ 修复：同时收集 ABM 和 SCM（只排除固定的记忆）
            var abmMemories = targetMemories
                .Where(m => m.layer == MemoryLayer.Active && !m.isPinned)
                .ToList();
                
            var scmMemories = targetMemories
                .Where(m => m.layer == MemoryLayer.Situational && !m.isPinned)
                .ToList();
            
            var allMemoriesToSummarize = new List<MemoryEntry>();
            allMemoriesToSummarize.AddRange(abmMemories);
            allMemoriesToSummarize.AddRange(scmMemories);
                
            if (allMemoriesToSummarize.Count == 0)
            {
                Messages.Message("没有可总结的记忆（ABM或SCM）", MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            string confirmMessage;
            if (abmMemories.Count > 0 && scmMemories.Count > 0)
            {
                confirmMessage = $"确定要总结 {abmMemories.Count} 条ABM记忆和 {scmMemories.Count} 条SCM记忆吗？";
            }
            else if (abmMemories.Count > 0)
            {
                confirmMessage = $"确定要总结 {abmMemories.Count} 条ABM记忆吗？";
            }
            else
            {
                confirmMessage = $"确定要总结 {scmMemories.Count} 条SCM记忆吗？";
            }
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                confirmMessage,
                delegate
                {
                    AggregateMemories(
                        allMemoriesToSummarize,
                        MemoryLayer.EventLog,
                        currentMemoryComp.SituationalMemories,
                        currentMemoryComp.EventLogMemories,
                        "daily_summary"
                    );
                    
                    // ⭐ 总结后清空ABM（因为已经总结过了）
                    foreach (var abm in abmMemories)
                    {
                        currentMemoryComp.ActiveMemories.Remove(abm);
                    }
                    
                    selectedMemories.Clear();
                    filtersDirty = true; // ⭐ v3.3.32: Mark cache dirty after modifying memories
                    Messages.Message("RimTalk_MindStream_SummarizedN".Translate(scmMemories.Count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void ArchiveMemories(List<MemoryEntry> targetMemories)
        {
            if (currentMemoryComp == null || targetMemories == null || targetMemories.Count == 0)
                return;
            
            // ⭐ 修复：排除固定的和用户编辑的记忆
            var elsMemories = targetMemories
                .Where(m => m.layer == MemoryLayer.EventLog && !m.isPinned && !m.isUserEdited)
                .ToList();
                
            if (elsMemories.Count == 0)
            {
                Messages.Message("RimTalk_MindStream_NoELSSelected".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_MindStream_ArchiveConfirm".Translate(elsMemories.Count),
                delegate
                {
                    AggregateMemories(
                        elsMemories,
                        MemoryLayer.Archive,
                        currentMemoryComp.EventLogMemories,
                        currentMemoryComp.ArchiveMemories,
                        "deep_archive"
                    );
                    
                    selectedMemories.Clear();
                    filtersDirty = true; // ⭐ v3.3.32: Mark cache dirty after modifying memories
                    Messages.Message("RimTalk_MindStream_ArchivedN".Translate(elsMemories.Count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void DeleteMemories(List<MemoryEntry> targetMemories)
        {
            if (currentMemoryComp == null || targetMemories == null || targetMemories.Count == 0)
                return;
            
            int count = targetMemories.Count;
            
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_MindStream_DeleteConfirm".Translate(count),
                delegate
                {
                    foreach (var memory in targetMemories.ToList())
                    {
                        currentMemoryComp.DeleteMemory(memory.id);
                    }
                    
                    selectedMemories.Clear();
                    filtersDirty = true; // ⭐ v3.3.32: Mark cache dirty after modifying memories
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
        
        /// <summary>
        /// ⭐ v3.3.32: Get filtered memories with caching
        /// Returns cached list if available, otherwise rebuilds cache
        /// </summary>
        private List<MemoryEntry> GetFilteredMemories()
        {
            if (filtersDirty || cachedFilteredMemories == null)
            {
                RebuildFilteredMemories();
                filtersDirty = false;
            }
            
            return cachedFilteredMemories;
        }
        
        /// <summary>
        /// ⭐ v3.3.32: Rebuild filtered memories cache
        /// This is the original GetFilteredMemories logic
        /// </summary>
        private void RebuildFilteredMemories()
        {
            if (currentMemoryComp == null)
            {
                cachedFilteredMemories = new List<MemoryEntry>();
                return;
            }
            
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
            cachedFilteredMemories = memories.OrderByDescending(m => m.timestamp).ToList();
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
        
        private void ShowOperationGuide()
        {
            string guide = "=== Mind Stream 操作指南 ===\n\n" +
                "【选择记忆】\n" +
                "• 单击：选中单条记忆\n" +
                "• Ctrl+单击：多选/取消选择\n" +
                "• Shift+单击：范围选择\n" +
                "• 拖拽框选：批量选择\n\n" +
                "【批量操作】\n" +
                "• 总结：将SCM记忆总结到ELS\n" +
                "• 归档：将ELS记忆归档到CLPA\n" +
                "• 删除：删除选中的记忆\n\n" +
                "【右键功能】\n" +
                "• ELS/CLPA复选框上右键可新建记忆\n\n" +
                "【层级说明】\n" +
                "• ABM (蓝色): 超短期记忆\n" +
                "• SCM (绿色): 短期记忆\n" +
                "• ELS (黄色): 事件日志\n" +
                "• CLPA (紫色): 长期档案";
            
            Find.WindowStack.Add(new Dialog_MessageBox(guide, "关闭", null, "操作指南"));
        }
        
        private void ShowCreateMemoryMenu(MemoryLayer layer)
        {
            if (selectedPawn == null || currentMemoryComp == null)
            {
                Messages.Message("请先选择一个殖民者", MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            string layerName = GetLayerLabel(layer);
            
            options.Add(new FloatMenuOption($"添加对话记忆到 {layerName}", delegate
            {
                Find.WindowStack.Add(new Dialog_CreateMemory(selectedPawn, currentMemoryComp, layer, MemoryType.Conversation));
            }));
            
            options.Add(new FloatMenuOption($"添加行动记忆到 {layerName}", delegate
            {
                Find.WindowStack.Add(new Dialog_CreateMemory(selectedPawn, currentMemoryComp, layer, MemoryType.Action));
            }));
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        // ==================== Import/Export ====================
        
        /// <summary>
        /// 导出记忆到XML文件
        /// </summary>
        private void ExportMemories()
        {
            if (selectedPawn == null || currentMemoryComp == null)
            {
                Messages.Message("RimTalk_Memory_ExportNoPawn".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            try
            {
                string fileName = $"{selectedPawn.Name.ToStringShort}_Memories_{Find.TickManager.TicksGame}.xml";
                string savePath = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "MemoryExports");
                
                if (!System.IO.Directory.Exists(savePath))
                {
                    System.IO.Directory.CreateDirectory(savePath);
                }
                
                string fullPath = System.IO.Path.Combine(savePath, fileName);
                
                // 收集所有记忆
                var allMemories = new List<MemoryEntry>();
                allMemories.AddRange(currentMemoryComp.ActiveMemories);
                allMemories.AddRange(currentMemoryComp.SituationalMemories);
                allMemories.AddRange(currentMemoryComp.EventLogMemories);
                allMemories.AddRange(currentMemoryComp.ArchiveMemories);
                
                // ⭐ 修复：使用临时变量存储属性值
                string pawnId = selectedPawn.ThingID;
                string pawnName = selectedPawn.Name.ToStringShort;
                
                // 使用Verse的XML序列化
                Scribe.saver.InitSaving(fullPath, "MemoryExport");
                Scribe_Values.Look(ref pawnId, "pawnId");
                Scribe_Values.Look(ref pawnName, "pawnName");
                Scribe_Collections.Look(ref allMemories, "memories", LookMode.Deep);
                Scribe.saver.FinalizeSaving();
                
                Messages.Message("RimTalk_Memory_ExportSuccess".Translate(allMemories.Count, fileName), 
                    MessageTypeDefOf.PositiveEvent, false);
                
                Log.Message($"[RimTalk] Exported {allMemories.Count} memories to: {fullPath}");
                
                // ⭐ 导出成功后询问是否打开文件夹
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimTalk_Memory_OpenExportFolder".Translate(),
                    delegate
                    {
                        System.Diagnostics.Process.Start(savePath);
                    },
                    true,
                    "RimTalk_Memory_ExportSuccessTitle".Translate()
                ));
            }
            catch (System.Exception ex)
            {
                Messages.Message("RimTalk_Memory_ExportFailed".Translate(ex.Message), 
                    MessageTypeDefOf.RejectInput, false);
                Log.Error($"[RimTalk] Memory export failed: {ex}");
            }
        }
        
        /// <summary>
        /// 从XML文件导入记忆
        /// </summary>
        private void ImportMemories()
        {
            if (selectedPawn == null || currentMemoryComp == null)
            {
                Messages.Message("RimTalk_Memory_ImportNoPawn".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            string savePath = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "MemoryExports");
            
            if (!System.IO.Directory.Exists(savePath))
            {
                Messages.Message("RimTalk_Memory_ImportNoFolder".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            var files = System.IO.Directory.GetFiles(savePath, "*.xml");
            
            if (files.Length == 0)
            {
                Messages.Message("RimTalk_Memory_ImportNoFiles".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            // 创建文件选择菜单
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // ⭐ 添加"打开文件夹"选项
            options.Add(new FloatMenuOption("RimTalk_Memory_OpenFolder".Translate(), delegate
            {
                System.Diagnostics.Process.Start(savePath);
            }));
            
            // 分隔线（用空选项实现）
            options.Add(new FloatMenuOption("─────────────────────", null));
            
            foreach (var file in files.OrderByDescending(f => System.IO.File.GetLastWriteTime(f)))
            {
                string fileName = System.IO.Path.GetFileName(file);
                var fileInfo = new System.IO.FileInfo(file);
                string label = $"{fileName} ({fileInfo.Length / 1024}KB - {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm})";
                
                options.Add(new FloatMenuOption(label, delegate
                {
                    ImportFromFile(file);
                }));
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        /// <summary>
        /// 从指定文件导入记忆
        /// </summary>
        private void ImportFromFile(string filePath)
        {
            try
            {
                List<MemoryEntry> importedMemories = new List<MemoryEntry>();
                string pawnId = "";
                string pawnName = "";
                
                Scribe.loader.InitLoading(filePath);
                Scribe_Values.Look(ref pawnId, "pawnId");
                Scribe_Values.Look(ref pawnName, "pawnName");
                Scribe_Collections.Look(ref importedMemories, "memories", LookMode.Deep);
                Scribe.loader.FinalizeLoading();
                
                if (importedMemories == null || importedMemories.Count == 0)
                {
                    Messages.Message("RimTalk_Memory_ImportEmpty".Translate(), 
                        MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                // 确认导入
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimTalk_Memory_ImportConfirm".Translate(pawnName, importedMemories.Count, selectedPawn.Name.ToStringShort),
                    delegate
                    {
                        int imported = 0;
                        
                        foreach (var memory in importedMemories)
                        {
                            // 根据层级添加到对应列表
                            switch (memory.layer)
                            {
                                case MemoryLayer.Active:
                                    if (currentMemoryComp.ActiveMemories.Count < RimTalkMemoryPatchMod.Settings.maxActiveMemories)
                                    {
                                        currentMemoryComp.ActiveMemories.Add(memory);
                                        imported++;
                                    }
                                    break;
                                    
                                case MemoryLayer.Situational:
                                    if (currentMemoryComp.SituationalMemories.Count < RimTalkMemoryPatchMod.Settings.maxSituationalMemories)
                                    {
                                        currentMemoryComp.SituationalMemories.Add(memory);
                                        imported++;
                                    }
                                    break;
                                    
                                case MemoryLayer.EventLog:
                                    if (currentMemoryComp.EventLogMemories.Count < RimTalkMemoryPatchMod.Settings.maxEventLogMemories)
                                    {
                                        currentMemoryComp.EventLogMemories.Add(memory);
                                        imported++;
                                    }
                                    break;
                                    
                                case MemoryLayer.Archive:
                                    if (currentMemoryComp.ArchiveMemories.Count < RimTalkMemoryPatchMod.Settings.maxArchiveMemories)
                                    {
                                        currentMemoryComp.ArchiveMemories.Add(memory);
                                        imported++;
                                    }
                                    break;
                            }
                        }
                        
                        filtersDirty = true; // ⭐ v3.3.32: Mark cache dirty after importing memories
                        
                        Messages.Message("RimTalk_Memory_ImportSuccess".Translate(imported, importedMemories.Count), 
                            MessageTypeDefOf.PositiveEvent, false);
                        
                        Log.Message($"[RimTalk] Imported {imported}/{importedMemories.Count} memories from: {filePath}");
                    }
                ));
            }
            catch (System.Exception ex)
            {
                Messages.Message("RimTalk_Memory_ImportFailed".Translate(ex.Message), 
                    MessageTypeDefOf.RejectInput, false);
                Log.Error($"[RimTalk] Memory import failed: {ex}");
            }
        }
    }
}
