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
    /// MainTabWindow_Memory - TopBar 绘制部分
    /// 包含顶部栏、Pawn选择器和统计信息显示
    /// </summary>
    public partial class MainTabWindow_Memory
    {
        // ==================== Top Bar ====================
        
        private void DrawTopBar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(5f);
            
            // Pawn Selector
            Rect pawnSelectorRect = new Rect(innerRect.x, innerRect.y + 5f, 250f, 35f);
            DrawPawnSelector(pawnSelectorRect);
            
            // ? Show All Humanlikes Checkbox
            Rect checkboxRect = new Rect(innerRect.x + 260f, innerRect.y + 10f, 180f, 25f);
            Widgets.CheckboxLabeled(checkboxRect, "RimTalk_ShowAllHumanlikes".Translate(), ref showAllHumanlikes);
            
            // ? 统计信息栏（移到这里，替换掉总记忆数）
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
            
            // ? 新增：总结提示词按钮
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
        
        // ? 新增：TopBar统计信息显示
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
            // ? 根据showAllHumanlikes决定显示哪些Pawn
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
                        filtersDirty = true; // ? v3.3.32: Mark cache dirty when pawn changes
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // Auto-select
            if (selectedPawn == null && colonists.Count > 0)
            {
                selectedPawn = colonists[0];
                filtersDirty = true; // ? v3.3.32: Mark cache dirty on first selection
            }
        }
    }
}
