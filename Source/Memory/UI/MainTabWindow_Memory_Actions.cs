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
    /// MainTabWindow_Memory - Actions 批量操作部分
    /// 包含总结、归档、删除等批量操作逻辑
    /// </summary>
    public partial class MainTabWindow_Memory
    {
        // ==================== Batch Actions ====================
        
        private void SummarizeMemories(List<MemoryEntry> targetMemories)
        {
            if (currentMemoryComp == null || targetMemories == null || targetMemories.Count == 0)
                return;
            
            // ? 修复：同时收集 ABM 和 SCM（只排除固定的记忆）
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
                    
                    // ? 总结后清空ABM（因为已经总结过了）
                    foreach (var abm in abmMemories)
                    {
                        currentMemoryComp.ActiveMemories.Remove(abm);
                    }
                    
                    selectedMemories.Clear();
                    filtersDirty = true; // ? v3.3.32: Mark cache dirty after modifying memories
                    Messages.Message("RimTalk_MindStream_SummarizedN".Translate(scmMemories.Count), MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }
        
        private void ArchiveMemories(List<MemoryEntry> targetMemories)
        {
            if (currentMemoryComp == null || targetMemories == null || targetMemories.Count == 0)
                return;
            
            // ? 修复：排除固定的和用户编辑的记忆
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
                    filtersDirty = true; // ? v3.3.32: Mark cache dirty after modifying memories
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
                    filtersDirty = true; // ? v3.3.32: Mark cache dirty after modifying memories
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
    }
}
