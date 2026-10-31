using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimTalk.Memory.Compat;

#warning VibeCoding 产物。等正式版迭代稳定后移除。
/// <summary>
/// 旧版记忆兼容模块，代码套用自回退前的 CustomScribe。
/// </summary>
public static class CustomScribeLegacy
{
    // 旧版导出文件夹路径：<SaveData>\MemoryExports\
    public static readonly string LegacyExportFolder = Path.Combine(GenFilePaths.SaveDataFolderPath, "MemoryExports");


    // 导入
    /// <summary>
    /// 获取可导入 XML，按最后修改时间从新到旧排列。同时扫描新版与旧版导出目录。
    /// </summary>
    /// <remarks>
    /// 可能返空，表示导入文件夹不存在。
    /// </remarks>
    public static IEnumerable<string> GetImportFiles()
    {
        var files = new List<string>();
        if (Directory.Exists(CustomScribe.ExportFolder))
        {
            files.AddRange(Directory.GetFiles(CustomScribe.ExportFolder, "*.xml"));
        }
        if (Directory.Exists(LegacyExportFolder))
        {
            files.AddRange(Directory.GetFiles(LegacyExportFolder, "*.xml"));
        }
        return files.Count > 0 ? files.Distinct().OrderByDescending(File.GetLastWriteTime) : null;
    }

    /// <summary>
    /// 执行导入，同时兼容新版 FourLayerMemoryComp XML 与旧版 memories 列表 XML
    /// </summary>
    public static void Import(string path, FourLayerMemoryComp targetComp)
    {
        try
        {
            if (targetComp is null || string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            FourLayerMemoryComp importComp = null;
            List<MemoryEntry> legacyMemories = null;

            Scribe.loader.InitLoading(path);
            try
            {
                // 尝试新版导出格式
                Scribe_Deep.Look(ref importComp, "MemoryComp");

                // 兼容旧版导出格式（直接存储 memories 列表）
                if (importComp == null)
                {
                    Scribe_Collections.Look(ref legacyMemories, "memories", LookMode.Deep);
                }
            }
            finally
            {
                Scribe.loader.FinalizeLoading();
            }

            if (importComp != null)
            {
                targetComp.Import(importComp);
            }
            else if (legacyMemories != null && legacyMemories.Count > 0)
            {
                ImportLegacyMemories(legacyMemories, targetComp);
            }
            else
            {
                Log.Warning($"[RimTalk.Memory] No valid memories loaded from {path}");
                return;
            }

            Messages.Message("RimTalk.Memory.ImportSuccessFrom".Translate() + path, MessageTypeDefOf.TaskCompletion, false);
        }
        catch (Exception exception)
        {
            Log.Error($"[RimTalk.Memory] Import failed: {exception}");
        }
    }

    private static void ImportLegacyMemories(List<MemoryEntry> legacyMemories, FourLayerMemoryComp targetComp)
    {
        targetComp.ActiveMemories.Clear();
        targetComp.SituationalMemories.Clear();
        targetComp.EventLogMemories.Clear();
        targetComp.ArchiveMemories.Clear();
        targetComp.SummarizedIds.Clear();

        foreach (var memory in legacyMemories)
        {
            if (memory == null) continue;
            switch (memory.Layer)
            {
                case MemoryLayer.Active:
                    targetComp.ActiveMemories.Add(memory);
                    break;
                case MemoryLayer.Situational:
                    targetComp.SituationalMemories.Add(memory);
                    break;
                case MemoryLayer.EventLog:
                    targetComp.EventLogMemories.Add(memory);
                    break;
                case MemoryLayer.Archive:
                    targetComp.ArchiveMemories.Add(memory);
                    break;
                default:
                    targetComp.ActiveMemories.Add(memory);
                    break;
            }
        }
    }
}
