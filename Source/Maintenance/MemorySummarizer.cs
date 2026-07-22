using RimTalk.Memory.AI;
using RimTalk.Memory.Utils;
using RimTalk.MemoryPatch;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.Maintenance
{
    /// <summary>
    /// 语义总结器：绑定单个 <see cref="FourLayerMemoryComp"/>，
    /// 统一每日总结、手动总结、选中总结、选中归档与周期归档
    /// </summary>
    /// <remarks>
    /// 本类是无持久状态 POCO，构造时由 <see cref="FourLayerMemoryComp"/> 持有，
    /// 不单独写入 Scribe，读档后继续引用所属 Comp 中恢复的四层数据。
    /// 总结方法中大量处理集合，其中不可避免的有多余遍历的情况，
    /// 因总结方法非热点，故为保障可读性而至少暂时不做优化。
    /// </remarks>
    public class MemorySummarizer
    {
        // 父组件（记忆组件）的引用
        // 不可能为空，若为空则放任后续逻辑崩溃，以暴露问题
        private readonly FourLayerMemoryComp _memoryComp;

        // 组件持有者
        // 同上，不可能为空
        private ThingWithComps Parent => _memoryComp.parent;

        // 上次总结的 GameTick
        // 如果为 -1，则表示尚未计算过，首次访问时会计算最近一次潜在的总结 tick 并缓存
        private int _lastSummarizeTick = -1;
        private int LastSummarizeTick => _lastSummarizeTick switch
        {
            -1 => _lastSummarizeTick = ComputeLatestPotentialSummarizeTick(),
            _ => _lastSummarizeTick
        };

        private const int SummarizeIntervalTicks = GenDate.TicksPerDay; // 每日总结间隔，单位 tick

        // 内部快捷访问
        // 由上游背书非空
        private List<MemoryEntry> ABMList => _memoryComp.ActiveMemories;
        private List<MemoryEntry> SCMList => _memoryComp.SituationalMemories;
        private List<MemoryEntry> ELSList => _memoryComp.EventLogMemories;
        private List<MemoryEntry> CLPAList => _memoryComp.ArchiveMemories;

        // 实例构造函数
        public MemorySummarizer(FourLayerMemoryComp memoryComp)
        {
            _memoryComp = memoryComp;
        }

        // ==================== 总结 Summarize ====================
        /// <summary>
        /// 每日自动总结。
        /// 校验配置项、父组件持有者、总结间隔、上次总结 tick 等条件，若满足则执行自动总结。
        /// </summary>
        public void DailySummarize()
        {
            if (// 配置项
                !RimTalkMemoryPatchMod.Settings.EnableDailySummarization
                // 父组件持有者必须是殖民者（暂行）
                || Parent is not Pawn { IsColonist: true }
                // 每日总结有最小间隔（主要用于防止时差问题）
                || GenTicks.TicksGame - LastSummarizeTick < SummarizeIntervalTicks
                // 如果上次总结 tick 与最近一次潜在总结 tick 相同，则说明今天已经总结过了
                || !(ComputeLatestPotentialSummarizeTick() is var latestPotentialSummarizeTick)
                || latestPotentialSummarizeTick <= _lastSummarizeTick)
                return;

            // 执行自动总结
            AutoSummarize(latestPotentialSummarizeTick);

            // 更新上次总结 tick
            // 总结可能失败，但只要尝试过就算一次总结，避免重复尝试
            // 例如，若 LLM 服务不可用，此时重复尝试没有意义
            // AutoSummarize 的分组机制本身就能兜底过去的总结失败
            _lastSummarizeTick = latestPotentialSummarizeTick;
        }

        /// <summary>
        /// 自动总结：抓取 ABM + SCM 中未被总结过、且落在完整总结周期中的条目，按天分组下发进行总结。
        /// 可以被 DailySummarize 调用，也可以被 UI 主动调用。
        /// </summary>
        /// <remarks>
        /// 按天分组的切分逻辑，以及 ComputeLatestPotentialSummarizeTick 的计算机制，
        /// 会导致 parent 如果改变了时区，则可能会切分出不完整的周期，
        /// 类似“倒时差”
        /// </remarks>
        public void AutoSummarize(int latestPotentialSummarizeTick = -1)
        {
            // 懒计算
            if (latestPotentialSummarizeTick == -1)
            {
                latestPotentialSummarizeTick = ComputeLatestPotentialSummarizeTick();
            }

            bool IsSummarizable(MemoryEntry memory) =>
                memory is { IsSummarizing: false, IsSummarized: false } && memory.GameTick < latestPotentialSummarizeTick;

            // 从 ABM + SCM 中抓取符合条件的条目并按天分组，最旧的周期先提交
            foreach (var group in ((List<MemoryEntry>)[
                .. ABMList.Where(IsSummarizable),
                .. SCMList.Where(IsSummarizable)
                ])
                .GroupBy(m => (latestPotentialSummarizeTick - m.GameTick) / GenDate.TicksPerDay)
                .OrderBy(g => g.Key))
            {
                // 若没有可总结的条目，则 foreach 不会执行，总结静默失败
                // 符合自动总结的设计理念
                SummarizeInternal(group);
            }
        }

        /// <summary>
        /// 手动总结：将传入的条目集合作为 query，从 ABM + SCM 中抓取符合条件的条目进行总结
        /// 目前仍在考虑是否移除 MemoryEntry 中的层级字段
        /// 若最终决定不移除，则可以直接筛选传入的条目集合，而无需再从 ABM + SCM 中抓取
        /// </summary>
        public void ManualSummarize(IEnumerable<MemoryEntry> source)
        {
            if (source is null || !source.Any())
                return;

            // 建立 query
            var sourceHashSet = source.ToHashSet();

            // 手动总结权限更高，已总结过的条目也可再次总结
            bool IsSummarizable(MemoryEntry memory) =>
                memory is { IsSummarizing: false } && sourceHashSet.Contains(memory);

            // 从 ABM + SCM 中抓取 query 命中的条目
            var targetMemories = (List<MemoryEntry>)[
                .. ABMList.Where(IsSummarizable),
                .. SCMList.Where(IsSummarizable)
                ];

            if (targetMemories.Count == 0)
            {
                Messages.Message("无可总结条目", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // 执行总结
            SummarizeInternal(targetMemories);
        }

        // 使用收集好的记忆条目集合执行实际总结操作
        private void SummarizeInternal(IEnumerable<MemoryEntry> memories)
        {
            // 转换成列表，按时间升序排序
            // 上游已保证 memories 及 memory 不为空也不为空集合
            var memoryList = memories.OrderBy(m => m.GameTick).ToList();

            // 构建提示词
            string prompt = BuildPrompt(
                memoryList,
                template: RimTalkMemoryPatchMod.Settings.SummarizePrompt,
                backUp: RimTalkMemoryPatchSettings.DefaultSummarizePrompt,
                showHour: true
                );

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Log.Error("[RimTalk.Memory.Maintenance] 总结提示词构建失败");
                return;
            }

            // 提前构建好 new MemoryEntry
            // 相当于和构建提示词同步捕获当前待总结记忆们的快照
            var summaryMemory = BuildEmptySummary(memoryList, MemoryLayer.EventLog);

            // 总结任务正式下发，将源条目标记为正在总结中
            foreach (var memory in memoryList)
            {
                memory.IsSummarizing = true;
            }

            try
            {
                // 入列 AI 请求队列，异步执行
                AIService.EnqueueAIRequest(
                    prompt,
                    callback: result =>
                    {
                        if (string.IsNullOrWhiteSpace(result))
                        {
                            Log.Error("[RimTalk.Memory.Maintenance] AI 总结请求返回空结果，总结失败");
                            return;
                        }

                        // 构建成功，更新 summaryMemory 的内容并入列
                        summaryMemory.Content = result;
                        ELSList?.Add(summaryMemory);

                        // 构建成功，标记源条目为已总结
                        foreach (var m in memoryList)
                            m?.IsSummarized = true;
                    },
                    dispose: () =>
                    {
                        // 无论成功与否，都将源条目标记为不再总结中
                        foreach (var m in memoryList)
                            m?.IsSummarizing = false;
                    }
                    );
            }
            catch (Exception ex)
            {
                foreach (var m in memoryList)
                    m?.IsSummarizing = false;
                Log.Error($"[RimTalk.Memory.Maintenance] 总结过程中发生异常: {ex.Message}");
            }
        }


        // ==================== 归档 Archive ====================
        /// <summary>
        /// 每周期自动归档。
        /// 校验配置项、父组件持有者、总结间隔等条件，若满足则执行自动归档。
        /// </summary>
        public void PeriodicArchive()
        {
            if (// 配置项
                !RimTalkMemoryPatchMod.Settings.EnableAutoArchive
                // 父组件持有者必须是殖民者（暂行）
                || Parent is not Pawn { IsColonist: true }
                // 每周期归档间隔
                || !Parent.IsHashIntervalTick(GenDate.TicksPerDay * RimTalkMemoryPatchMod.Settings.ArchiveIntervalDays))
                return;

            // 执行自动归档
            Archive();
        }

        /// <summary>
        /// Archive 的行为较单一，故手动和自动可直接合并为一个入口，
        /// 如果显式传入了 source，说明是手动归档
        /// </summary>
        public void Archive(IEnumerable<MemoryEntry> source = null)
        {
            // 与手动 summarize 以及自动 summarize/archive 的面向层级 + 抓取机制不同，
            // 手动 Archive 面向记忆类型，只要是 MemoryType.Summarization 的条目都可以被手动归档
            // 手动归档权限更高，已总结过的条目也可再次总结
            var targetMemories = source?.Where(m => m is { Type: MemoryType.Summarization, IsSummarizing: false })
                ?? (List<MemoryEntry>)[.. ELSList.Where(m => m is { IsSummarizing: false, IsSummarized: false })];

            if (!targetMemories.Any())
            {
                // 仅手动归档时才会显示提示信息，自动归档静默失败
                if (source is not null)
                    Messages.Message("无可归档条目", MessageTypeDefOf.RejectInput, historical: false);

                return;
            }

            // 执行归档
            ArchiveInternal(targetMemories);

        }

        // 使用收集好的记忆条目集合执行实际归档操作
        private void ArchiveInternal(IEnumerable<MemoryEntry> memories)
        {
            // 转换成列表，按时间升序排序
            // 上游已保证 memories 及 memory 不为空也不为空集合
            var memoryList = memories.OrderBy(m => m.GameTick).ToList();

            // 构建提示词
            string prompt = BuildPrompt(
                memoryList,
                RimTalkMemoryPatchMod.Settings.ArchivePrompt,
                backUp: RimTalkMemoryPatchSettings.DefaultArchivePrompt,
                showHour: false
                );

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Log.Error("[RimTalk.Memory.Maintenance] 归档提示词构建失败");
                return;
            }

            // 提前构建好 new MemoryEntry
            // 相当于和构建提示词同步捕获当前待归档记忆们的快照
            var archiveMemory = BuildEmptySummary(memoryList, MemoryLayer.Archive);

            // 总结任务正式下发，将源条目标记为正在总结中
            foreach (var memory in memoryList)
            {
                memory.IsSummarizing = true;
            }

            try
            {
                // 入列 AI 请求队列，异步执行
                AIService.EnqueueAIRequest(
                    prompt,
                    callback: result =>
                    {
                        if (string.IsNullOrWhiteSpace(result))
                        {
                            Log.Error("[RimTalk.Memory.Maintenance] AI 归档请求返回空结果，归档失败");
                            return;
                        }

                        // 构建成功，更新 archiveMemory 的内容并入列
                        archiveMemory.Content = result;
                        CLPAList?.Add(archiveMemory);

                        // 构建成功，标记源条目为已总结
                        foreach (var m in memoryList)
                            m?.IsSummarized = true;
                    },
                    dispose: () =>
                    {
                        // 无论成功与否，都将源条目标记为不再总结中
                        foreach (var m in memoryList)
                            m?.IsSummarizing = false;
                    }
                    );
            }
            catch (Exception ex)
            {
                foreach (var m in memoryList)
                    m?.IsSummarizing = false;

                Log.Error($"[RimTalk.Memory.Maintenance] 归档过程中发生异常: {ex.Message}");
            }
        }


        // ==================== 内部工具 ====================

        // 计算组件持有者在当前坐标下，最近一次可能的每日总结的 GameTick
        // 计算的是理论上的最近一次总结 tick，与总结是否事实执行无关
        private int ComputeLatestPotentialSummarizeTick()
        {
            if (Find.TickManager is not { } tickManager) return -1;

            // 计算时考虑坐标偏移
            int summarizeTickToday =
                GenTicks.TicksGame - GenLocalDate.DayTick(Parent)
                + RimTalkMemoryPatchMod.Settings.summarizationHour * GenDate.TicksPerHour;

            return summarizeTickToday <= tickManager.TicksGame ? summarizeTickToday : summarizeTickToday - GenDate.TicksPerDay;
        }

        // 将传入的记忆集合基于模板构建为 prompt
        private string BuildPrompt(List<MemoryEntry> memoryList, string template, string backUp = null, bool showHour = false)
        {
            // 校验模板
            if (string.IsNullOrWhiteSpace(template))
            {
                if (backUp is null) return string.Empty;
                template = backUp;
            }

            // 启动 showHour 时，会在构建记忆块时额外注释12小时制时间
            var location = showHour ? Find.WorldGrid?.LongLatOf(Parent.Tile) ?? Vector2.zero : Vector2.zero;

            string ComputeHour(MemoryEntry memory) =>
                GenDateExtension.GetInGameHour12HString(GenDate.TickGameToAbs(memory.GameTick), location);

            // 构建记忆块
            string memoryListString = string.Join("\n\n", memoryList
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .Select(m =>
                $"{m.AgeString}{(showHour ? $", {ComputeHour(m)}" : string.Empty)}\n{m.Content}"));

            // 组装 prompt
            try
            {
                return string.Format(template, Parent.LabelShort, memoryListString);
            }
            catch (Exception ex)
            {
                // 如果模板格式非法，尝试使用默认模板
                if (backUp is null) return string.Empty;
                Log.Error($"[RimTalk.Memory.Maintenance] 提示词非法，尝试使用默认模板: {ex.Message}");
                return string.Format(backUp, Parent.LabelShort, memoryListString);
            }
        }

        // 将传入的记忆集合构建为一个新的 MemoryEntry
        private MemoryEntry BuildEmptySummary(List<MemoryEntry> memoryList, MemoryLayer targetLayer)
        {
            return new MemoryEntry(
                content: null,
                MemoryType.Summarization,
                targetLayer,
                importance: memoryList.Average(m => m.Importance)
            )
            {
                // 取 memoryList 中最新的 GameTick 作为新条目的时间戳
                // memoryList 是有序的，最后一项即为最新
                GameTick = memoryList[^1].GameTick,

                tags = [.. memoryList.SelectMany(m => m.tags).Distinct()],
                keywords = [.. memoryList.SelectMany(m => m.keywords).Distinct()]
            };
        }
    }

}
