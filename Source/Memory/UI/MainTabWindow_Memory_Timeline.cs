using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Memory;
using System;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// MainTabWindow_Memory - Timeline 时间线绘制部分
    /// 包含时间线、记忆卡片绘制和拖拽选择
    /// </summary>
    public partial class MainTabWindow_Memory
    {
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
            
            // ? 高性能虚拟化：二分查找 + 提前退出
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
            
            // ? 修复：在EndScrollView之前绘制选择框，使其在正确的坐标系中
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
            
            // ? 计算按钮区域（用于检测是否点击在按钮上）
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
                // ? v3.3.32: No need to mark dirty for pin/unpin as it doesn't affect filtering
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
                    // ? v3.3.32: Mark dirty when opening edit dialog
                    // User might change layer or type which affects filtering
                    filtersDirty = true;
                }
                clickedOnButton = true;
                Event.current.Use();
            }
            TooltipHandler.TipRegion(editButtonRect, "RimTalk_MindStream_Edit".Translate());
            
            // ? 只在非按钮区域处理点击选择
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
            
            string header = $"[{layerLabel}] {typeLabel} ? {timeLabel}";
            if (!string.IsNullOrEmpty(memory.relatedPawnName))
            {
                header += $" ? {"RimTalk_MindStream_With".Translate()} {memory.relatedPawnName}";
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
            
            // ? 修复：在viewport坐标系中开始拖拽
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
                // ? 修复：同样在viewport坐标系中
                dragCurrentPos = e.mousePosition - listRect.position;
                
                // Select memories that intersect with drag box
                // ? 修复：转换为content坐标（加上scroll offset）
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
    }
}
