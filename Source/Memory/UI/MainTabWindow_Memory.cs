using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using RimTalk.Memory;
using System;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// Mind Stream Timeline - Multi-Select Memory Cards
    /// ★ v3.3.19: 完全重构 - 时间线卡片布局 + 拖拽多选 + 批量操作
    /// ★ v3.3.32: 性能优化 - GetFilteredMemories缓存机制
    /// ★ v3.3.40: 代码重构 - 拆分为多个 partial class 文件
    /// 
    /// 文件结构：
    /// - MainTabWindow_Memory.cs (主文件 - 字段定义和入口)
    /// - MainTabWindow_Memory_TopBar.cs (TopBar 绘制)
    /// - MainTabWindow_Memory_Controls.cs (控制面板)
    /// - MainTabWindow_Memory_Timeline.cs (时间线绘制)
    /// - MainTabWindow_Memory_Actions.cs (批量操作)
    /// - MainTabWindow_Memory_ImportExport.cs (导入导出)
    /// - MainTabWindow_Memory_Utilities.cs (辅助方法)
    /// - MainTabWindow_Memory_Helpers.cs (聚合逻辑)
    /// </summary>
    public partial class MainTabWindow_Memory : MainTabWindow
    {
        // ==================== Data & State ====================
        private Pawn selectedPawn = null;
        private FourLayerMemoryComp currentMemoryComp = null;
        
        // ? 新增：显示所有类人生物选项
        private bool showAllHumanlikes = false;
        
        // Multi-select support
        private HashSet<MemoryEntry> selectedMemories = new HashSet<MemoryEntry>();
        private MemoryEntry lastSelectedMemory = null;
        
        // Drag selection
        private bool isDragging = false;
        private bool isMouseDown = false;           // ⭐ 左键已按下但尚未判定为拖拽
        private Vector2 dragStartPos = Vector2.zero;
        private Vector2 dragCurrentPos = Vector2.zero;
        private Vector2 mouseDownScreenPos = Vector2.zero; // ⭐ 按下时的屏幕坐标（用于阈值判定）
        private const float DRAG_THRESHOLD = 5f;    // ⭐ 拖拽判定阈值（像素）
        
        // UI State
        private Vector2 timelineScrollPosition = Vector2.zero;
        private MemoryType? filterType = null;
        
        // Layer filters
        private bool showABM = true;
        private bool showSCM = true;
        private bool showELS = true;
        private bool showCLPA = true;
        
        // ? v3.3.32: Filtered memories cache
        private List<MemoryEntry> cachedFilteredMemories;
        private bool filtersDirty = true;
        
        // Layout constants
        private const float TOP_BAR_HEIGHT = 50f;
        private const float CONTROL_PANEL_WIDTH = 220f;
        private const float SPACING = 10f;
        private const float CARD_WIDTH_FULL = 600f;
        private const float CARD_SPACING = 8f;
        
        // ? 性能优化：缓存数据
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

        /// <summary>
        /// 使 UI 缓存失效，强制下次绘制时刷新。
        /// 当外部修改了记忆数据时调用此方法。
        /// </summary>
        public void InvalidateCache()
        {
            lastMemoryCount = -1;
            filtersDirty = true;
        }

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
            
            // ? 在绘制任何子组件之前刷新缓存
            CheckAndRefreshCache();
            
            // Left Control Panel
            Rect controlPanelRect = new Rect(0f, contentY, CONTROL_PANEL_WIDTH, contentHeight);
            DrawControlPanel(controlPanelRect);
            
            // Right Timeline
            float timelineX = CONTROL_PANEL_WIDTH + SPACING;
            float timelineWidth = inRect.width - timelineX;
            Rect timelineRect = new Rect(timelineX, contentY, timelineWidth, contentHeight);
            DrawTimeline(timelineRect);
            
            // Handle drag end / single click
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (isMouseDown && !isDragging)
                {
                    // ⭐ 没有进入拖拽模式 → 当作单击处理
                    // 找到鼠标下方的记忆卡片
                    float timelineX2 = CONTROL_PANEL_WIDTH + SPACING;
                    float timelineWidth2 = inRect.width - timelineX2;
                    Rect timelineRect2 = new Rect(timelineX2, contentY, timelineWidth2, contentHeight);
                    Rect innerRect2 = timelineRect2.ContractedBy(5f);
                    
                    if (innerRect2.Contains(Event.current.mousePosition))
                    {
                        float mouseContentY = Event.current.mousePosition.y - innerRect2.y + timelineScrollPosition.y;
                        
                        // 在缓存的 Y 位置中查找点击的卡片
                        MemoryEntry clickedMemory = null;
                        for (int i = 0; i < cachedMemories.Count; i++)
                        {
                            float cardY = cachedCardYPositions[i];
                            float cardH = cachedCardHeights[i];
                            if (mouseContentY >= cardY && mouseContentY < cardY + cardH)
                            {
                                clickedMemory = cachedMemories[i];
                                break;
                            }
                        }
                        
                        if (clickedMemory != null)
                        {
                            HandleMemoryClick(clickedMemory);
                        }
                        else
                        {
                            selectedMemories.Clear();
                            lastSelectedMemory = null;
                        }
                    }
                }
                
                isMouseDown = false;
                isDragging = false;
                Event.current.Use();
            }
        }
    }
}