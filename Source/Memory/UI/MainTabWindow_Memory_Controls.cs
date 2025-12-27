using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Memory;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// MainTabWindow_Memory - Controls 控制面板部分
    /// 包含层级过滤器、类型过滤器和操作按钮
    /// </summary>
    public partial class MainTabWindow_Memory
    {
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
            
            // ? 移除了 DrawStatistics 调用，统计已移到TopBar
            
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
            
            // ? v3.3.32: Store previous values to detect changes
            bool prevShowABM = showABM;
            bool prevShowSCM = showSCM;
            bool prevShowELS = showELS;
            bool prevShowCLPA = showCLPA;
            
            // ABM
            Rect abmRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color abmColor = new Color(0.3f, 0.8f, 1f); // Cyan
            DrawColoredCheckbox(abmRect, "RimTalk_MindStream_ABM".Translate(), ref showABM, abmColor, MemoryLayer.Active);
            y += checkboxHeight + 2f;
            
            // SCM - ? 带右键菜单
            Rect scmRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color scmColor = new Color(0.3f, 1f, 0.5f); // Green
            DrawColoredCheckbox(scmRect, "RimTalk_MindStream_SCM".Translate(), ref showSCM, scmColor, MemoryLayer.Situational);
            y += checkboxHeight + 2f;
            
            // ELS - ? 带右键菜单
            Rect elsRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color elsColor = new Color(1f, 0.8f, 0.3f); // Yellow
            DrawColoredCheckbox(elsRect, "RimTalk_MindStream_ELS".Translate(), ref showELS, elsColor, MemoryLayer.EventLog);
            y += checkboxHeight + 2f;
            
            // CLPA - ? 带右键菜单
            Rect clpaRect = new Rect(parentRect.x, y, parentRect.width, checkboxHeight);
            Color clpaColor = new Color(0.8f, 0.4f, 1f); // Purple
            DrawColoredCheckbox(clpaRect, "RimTalk_MindStream_CLPA".Translate(), ref showCLPA, clpaColor, MemoryLayer.Archive);
            y += checkboxHeight;
            
            // ? v3.3.32: Mark cache dirty if any filter changed
            if (showABM != prevShowABM || showSCM != prevShowSCM || showELS != prevShowELS || showCLPA != prevShowCLPA)
            {
                filtersDirty = true;
            }
            
            return y;
        }
        
        private void DrawColoredCheckbox(Rect rect, string label, ref bool value, Color color, MemoryLayer? rightClickLayer)
        {
            // ? 右键检测（如果指定了层级）- 在绘制之前
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
            
            // ? 添加工具提示提示用户可以右键
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
                if (filterType != null) // ? v3.3.32: Only mark dirty if actually changed
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
                if (filterType != MemoryType.Conversation) // ? v3.3.32: Only mark dirty if actually changed
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
                if (filterType != MemoryType.Action) // ? v3.3.32: Only mark dirty if actually changed
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
            
            // ? 如果没有选中，则操作对象为当前页面所有可见记忆
            // 优先使用缓存的列表
            var targetMemories = hasSelection ? selectedMemories.ToList() : cachedMemories;
            int targetCount = targetMemories.Count;
            
            // ? 修复：总结按钮现在支持 ABM + SCM
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
            
            // ? 导出/导入按钮（并排显示）
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
    }
}
