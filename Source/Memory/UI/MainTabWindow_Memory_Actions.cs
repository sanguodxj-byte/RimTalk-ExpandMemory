using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

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

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                $"确定要总结选中的 {targetMemories.Count} 条记忆吗？",
                delegate
                {
                    currentMemoryComp.Summarizer.ManualSummarize(targetMemories);
                    selectedMemories.Clear();
                    filtersDirty = true;

                    // 后续考虑发放更全面的 message。以后再说
                    Messages.Message("总结命令已下发", MessageTypeDefOf.SilentInput, false);
                }
            ));
        }

        private void ArchiveMemories(List<MemoryEntry> targetMemories)
        {
            if (currentMemoryComp == null || targetMemories == null || targetMemories.Count == 0)
                return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                $"确定要归档选中的 {targetMemories.Count} 条记忆吗？",
                delegate
                {
                    currentMemoryComp.Summarizer.Archive(targetMemories);
                    selectedMemories.Clear();
                    filtersDirty = true;

                    // 后续考虑发放更全面的 message。以后再说
                    Messages.Message("归档命令已下发", MessageTypeDefOf.SilentInput, false);
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
                        currentMemoryComp.Maintainer.Remove(memory);
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
                    if (comp != null && comp.SituationalMemories.Count > 0)
                    {
                        pawnsToSummarize.Add(pawn);
                    }
                }
            }

            if (pawnsToSummarize.Count > 0)
            {
                foreach (var pawn in pawnsToSummarize)
                    pawn?.TryGetComp<FourLayerMemoryComp>()?.Summarizer.AutoSummarize();

                Messages.Message("RimTalk_MindStream_QueuedSummarization".Translate(pawnsToSummarize.Count), MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message("RimTalk_MindStream_NoNeedSummarization".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        /* 废弃代码，暂时先不删而是注释掉，之后再善后
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
                        comp.ManualArchive(); // 此方法高度危险，完全没有正确处理固定的记忆
                        count++;
                    }
                }
            }
            
            Messages.Message("RimTalk_MindStream_ArchivedForN".Translate(count), MessageTypeDefOf.PositiveEvent, false);
        }
        */
    }
}