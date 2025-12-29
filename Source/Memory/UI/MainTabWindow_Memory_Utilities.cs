using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Memory;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// MainTabWindow_Memory - Utilities 辅助方法部分
    /// 包含各种辅助方法和对话框
    /// </summary>
    public partial class MainTabWindow_Memory
    {
        // ==================== Helper Methods ====================
        
        /// <summary>
        /// ? v3.3.32: Get filtered memories with caching
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
        /// ? v3.3.32: Rebuild filtered memories cache
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
                "? 单击：选中单条记忆\n" +
                "? Ctrl+单击：多选/取消选择\n" +
                "? Shift+单击：范围选择\n" +
                "? 拖拽框选：批量选择\n\n" +
                "【批量操作】\n" +
                "? 总结：将SCM记忆总结到ELS\n" +
                "? 归档：将ELS记忆归档到CLPA\n" +
                "? 删除：删除选中的记忆\n\n" +
                "【右键功能】\n" +
                "? ELS/CLPA复选框上右键可新建记忆\n\n" +
                "【层级说明】\n" +
                "? ABM (蓝色): 超短期记忆\n" +
                "? SCM (绿色): 短期记忆\n" +
                "? ELS (黄色): 事件日志\n" +
                "? CLPA (紫色): 长期档案";
            
            Find.WindowStack.Add(new Dialog_MessageBox(guide, "关闭", null, "操作指南"));
        }
    }
}