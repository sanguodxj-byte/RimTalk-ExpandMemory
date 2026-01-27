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
    /// MainTabWindow_Memory - ImportExport 导入导出部分
    /// 包含记忆导入导出功能
    /// </summary>
    public partial class MainTabWindow_Memory
    {
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
                
                // ? 修复：使用临时变量存储属性值
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
                
                // ? 导出成功后询问是否打开文件夹
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
            
            // ? 添加"打开文件夹"选项
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
                                    // ⭐ v4.0: ABM 无容量限制
                                    currentMemoryComp.ActiveMemories.Add(memory);
                                    imported++;
                                    break;
                                    
                                case MemoryLayer.Situational:
                                    // ⭐ v4.0: SCM 已废弃，但仍支持导入旧数据
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
                        
                        filtersDirty = true; // ? v3.3.32: Mark cache dirty after importing memories
                        
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