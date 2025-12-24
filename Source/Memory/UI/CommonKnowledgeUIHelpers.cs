using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch; // ? 添加命名空间

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 常识库UI绘制委托 - 可复用的UI绘制方法
    /// ★ v3.3.19: 拆分代码 - 分离UI绘制逻辑
    /// </summary>
    public static class CommonKnowledgeUIHelpers
    {
        // ==================== 颜色常量 ====================
        private static readonly Color ColorInstructions = new Color(0.3f, 0.8f, 0.3f);
        private static readonly Color ColorLore = new Color(0.8f, 0.6f, 0.3f);
        private static readonly Color ColorPawnStatus = new Color(0.3f, 0.6f, 0.9f);
        private static readonly Color ColorHistory = new Color(0.7f, 0.5f, 0.7f);
        private static readonly Color ColorOther = Color.white;
        
        // ==================== 分类相关 ====================
        
        /// <summary>
        /// 获取分类显示名称
        /// </summary>
        public static string GetCategoryLabel(KnowledgeCategory category)
        {
            switch (category)
            {
                case KnowledgeCategory.All:
                    return CommonKnowledgeTranslationKeys.CategoryAll.Translate();
                case KnowledgeCategory.Instructions:
                    return CommonKnowledgeTranslationKeys.CategoryInstructions.Translate();
                case KnowledgeCategory.Lore:
                    return CommonKnowledgeTranslationKeys.CategoryLore.Translate();
                case KnowledgeCategory.PawnStatus:
                    return CommonKnowledgeTranslationKeys.CategoryPawnStatus.Translate();
                case KnowledgeCategory.History:
                    return CommonKnowledgeTranslationKeys.CategoryHistory.Translate();
                case KnowledgeCategory.Other:
                    return CommonKnowledgeTranslationKeys.CategoryOther.Translate();
                default:
                    return CommonKnowledgeTranslationKeys.CategoryUnknown.Translate();
            }
        }
        
        /// <summary>
        /// 获取分类颜色
        /// </summary>
        public static Color GetCategoryColor(CommonKnowledgeEntry entry)
        {
            var category = GetEntryCategory(entry);
            switch (category)
            {
                case KnowledgeCategory.Instructions:
                    return ColorInstructions;
                case KnowledgeCategory.Lore:
                    return ColorLore;
                case KnowledgeCategory.PawnStatus:
                    return ColorPawnStatus;
                case KnowledgeCategory.History:
                    return ColorHistory;
                default:
                    return ColorOther;
            }
        }
        
        /// <summary>
        /// 根据标签判断条目分类
        /// </summary>
        public static KnowledgeCategory GetEntryCategory(CommonKnowledgeEntry entry)
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
        
        // ==================== 可见性相关 ====================
        
        /// <summary>
        /// 获取可见性显示文本
        /// </summary>
        public static string GetVisibilityText(CommonKnowledgeEntry entry)
        {
            if (entry.targetPawnId == -1)
                return CommonKnowledgeTranslationKeys.VisibilityGlobal.Translate();
            
            var pawn = Find.Maps?
                .SelectMany(m => m.mapPawns.FreeColonists)
                .FirstOrDefault(p => p.thingIDNumber == entry.targetPawnId);
            
            return pawn != null 
                ? CommonKnowledgeTranslationKeys.VisibilityExclusive.Translate(pawn.LabelShort) 
                : CommonKnowledgeTranslationKeys.VisibilityDeleted.Translate(entry.targetPawnId);
        }
        
        // ==================== 绘制详情字段 ====================
        
        /// <summary>
        /// 绘制详情字段（标签 + 值）
        /// </summary>
        public static void DrawDetailField(Rect rect, string label, string value)
        {
            float labelWidth = 100f;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label + ":");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            Widgets.Label(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height), value);
        }
        
        // ==================== 绘制带颜色的复选框 ====================
        
        /// <summary>
        /// 绘制带颜色指示器的复选框
        /// </summary>
        public static void DrawColoredCheckbox(Rect rect, string label, ref bool value, Color color)
        {
            // 颜色指示器
            Rect colorRect = new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f);
            Widgets.DrawBoxSolid(colorRect, color);
            
            // 复选框
            Rect checkboxRect = new Rect(rect.x + 8f, rect.y, rect.width - 8f, rect.height);
            Widgets.CheckboxLabeled(checkboxRect, label, ref value);
        }
        
        // ==================== 绘制分类按钮 ====================
        
        /// <summary>
        /// 绘制分类按钮（带选中高亮）
        /// </summary>
        public static bool DrawCategoryButton(Rect rect, KnowledgeCategory category, bool isSelected, int count)
        {
            string categoryLabel = GetCategoryLabel(category);
            string label = $"{categoryLabel} ({count})";
            
            // 高亮选中项
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // 按钮（不绘制背景，使用自定义高亮）
            return Widgets.ButtonText(rect, label, drawBackground: false);
        }
        
        // ==================== 绘制自动生成设置 ====================
        
        /// <summary>
        /// 绘制自动生成设置区域
        /// </summary>
        public static void DrawAutoGenerateSettings(Rect rect, Action onGeneratePawnStatus, Action onGenerateEventRecord)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            Rect innerRect = rect.ContractedBy(5f);
            float y = innerRect.y;
            
            var settings = RimTalkMemoryPatchMod.Settings; // ? 修复：使用正确的命名空间
            
            // 殖民者状态
            bool enablePawnStatus = settings.enablePawnStatusKnowledge;
            Widgets.CheckboxLabeled(new Rect(innerRect.x, y, innerRect.width, 25f), 
                CommonKnowledgeTranslationKeys.PawnStatus.Translate(), ref enablePawnStatus);
            settings.enablePawnStatusKnowledge = enablePawnStatus;
            y += 30f;
            
            if (Widgets.ButtonText(new Rect(innerRect.x, y, innerRect.width, 25f), 
                CommonKnowledgeTranslationKeys.GenerateNow.Translate()))
            {
                onGeneratePawnStatus?.Invoke();
            }
            y += 30f;
            
            // 事件记录
            bool enableEventRecord = settings.enableEventRecordKnowledge;
            Widgets.CheckboxLabeled(new Rect(innerRect.x, y, innerRect.width, 25f), 
                CommonKnowledgeTranslationKeys.EventRecord.Translate(), ref enableEventRecord);
            settings.enableEventRecordKnowledge = enableEventRecord;
            y += 30f;
            
            if (Widgets.ButtonText(new Rect(innerRect.x, y, innerRect.width, 25f), 
                CommonKnowledgeTranslationKeys.GenerateNow.Translate()))
            {
                onGenerateEventRecord?.Invoke();
            }
        }
        
        // ==================== 工具方法 - Pawn选择菜单 ====================
        
        /// <summary>
        /// 显示Pawn选择菜单
        /// </summary>
        public static void ShowPawnSelectionMenu(Action<int> onSelected)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            options.Add(new FloatMenuOption(
                CommonKnowledgeTranslationKeys.GlobalAll.Translate(), 
                delegate { onSelected(-1); }
            ));
            
            var colonists = Find.Maps?.SelectMany(m => m.mapPawns.FreeColonists).ToList();
            if (colonists != null && colonists.Count > 0)
            {
                foreach (var pawn in colonists.OrderBy(p => p.LabelShort))
                {
                    int pawnId = pawn.thingIDNumber;
                    options.Add(new FloatMenuOption(
                        CommonKnowledgeTranslationKeys.ExclusiveTo.Translate(pawn.LabelShort), 
                        delegate { onSelected(pawnId); }
                    ));
                }
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
