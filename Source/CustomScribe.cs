using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    public static class CustomScribe
    {
        // 导出文件夹路径：<SaveData>\RimTalkMemory\MemoryExports\
        public static readonly string ExportFolder = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimTalkMemory\\MemoryExports");


        // 导出
        /// <summary>
        /// 记忆组件导出
        /// </summary>
        public static void Export(FourLayerMemoryComp memoryComp)
        {
            try
            {
                if (memoryComp is null) return;

                // 创建文件夹
                string folder = ExportFolder;
                Directory.CreateDirectory(folder);

                // 基础信息
                string parentName = memoryComp.parent?.LabelShort;
                int thingId = memoryComp.parent?.thingIDNumber ?? -1;

                // 构建文件路径
                string path = Path.Combine(folder, $"{parentName}_{thingId}_{Find.TickManager.TicksGame}.xml");

                // 启动导出
                SafeSaver.Save(path, "MemoryExports", () =>
                {
                    Scribe_Values.Look(ref parentName, "ParentName");
                    Scribe_Values.Look(ref thingId, "ThingId");
                    Scribe_Deep.Look(ref memoryComp, "MemoryComp");
                });
                Messages.Message("RimTalk.Memory.ExportSuccessTo".Translate() + path, MessageTypeDefOf.TaskCompletion, false);
            }
            catch (Exception exception)
            {
                Log.Error($"[RimTalk.Memory] Export failed: {exception}");
            }
        }


        // 导入
        /// <summary>
        /// 获取可导入 XML，按最后修改时间从新到旧排列。
        /// </summary>
        /// <remarks>
        /// 可能返空，表示导入文件夹不存在。
        /// </remarks>
        public static IEnumerable<string> GetImportFiles() =>
            Directory.Exists(ExportFolder)
            ? Directory.GetFiles(ExportFolder, "*.xml").OrderByDescending(File.GetLastWriteTime)
            : null;

        /// <summary>
        /// 打开导出文件夹，若不存在则创建。
        /// </summary>
        public static void OpenExportFolder()
        {
            Directory.CreateDirectory(ExportFolder);
            Process.Start(ExportFolder);
        }

        /// <summary>
        /// 执行导入
        /// </summary>
        public static void Import(string path, FourLayerMemoryComp targetComp)
        {
            try
            {
                if (targetComp is null || string.IsNullOrEmpty(path)) return;

                FourLayerMemoryComp importComp = null;

                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref importComp, "MemoryComp");
                Scribe.loader.FinalizeLoading();

                targetComp.Import(importComp);

                Messages.Message("RimTalk.Memory.ImportSuccessFrom" + path, MessageTypeDefOf.TaskCompletion, false);
            }
            catch (Exception exception)
            {
                Log.Error($"[RimTalk.Memory] Import failed: {exception}");
            }
        }
    }
}
