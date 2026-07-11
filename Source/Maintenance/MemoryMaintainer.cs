using RimTalk.MemoryPatch;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory.Maintenance
{
    /// <summary>
    /// 机械维护器：绑定单个 <see cref="FourLayerMemoryComp"/>，
    /// 集中承担记忆的Activity 衰减、低活跃清理、容量治理、Pin 状态及其直接层级迁移
    /// </summary>
    /// <remarks>
    /// 本类是无持久状态 POCO，构造时由 <see cref="FourLayerMemoryComp"/> 持有，
    /// 不单独写入 Scribe，读档后继续引用所属 Comp 中恢复的四层数据
    /// </remarks>
    public class MemoryMaintainer
    {
        // 父组件（记忆组件）的引用
        // 不可能为空，若为空则放任后续逻辑崩溃，以暴露问题
        private readonly FourLayerMemoryComp _memoryComp;

        // 内部快捷访问
        // 由上游背书非空
        private List<MemoryEntry> ABMList => _memoryComp.ActiveMemories;
        private List<MemoryEntry> SCMList => _memoryComp.SituationalMemories;
        private List<MemoryEntry> ELSList => _memoryComp.EventLogMemories;
        private List<MemoryEntry> CLPAList => _memoryComp.ArchiveMemories;

        // 实例构造函数
        public MemoryMaintainer(FourLayerMemoryComp memoryComp)
        {
            _memoryComp = memoryComp;
        }

        /// <summary>
        /// 执行各层级记忆衰减
        /// </summary>
        public void RunDecay()
        {
            var settings = RimTalkMemoryPatchMod.Settings;

            foreach (var sCM in SCMList)
                sCM?.Decay(settings.scmDecayRate);

            foreach (var eLS in ELSList)
                eLS?.Decay(settings.elsDecayRate);

            foreach (var cLPA in CLPAList)
                cLPA?.Decay(settings.clpaDecayRate);
        }

        /// <summary>
        /// 清理 activity 低于阈值的非固定记忆，空元素会被顺便清理
        /// 不清理 ABM
        /// </summary>
        public void CleanupLowActivityMemories()
        {
            SCMList.RemoveAll(m => m?.ShouldBeCleaned ?? true);
            ELSList.RemoveAll(m => m?.ShouldBeCleaned ?? true);
            CLPAList.RemoveAll(m => m?.ShouldBeCleaned ?? true);
        }

        /// <summary>
        /// 强制执行各层容量限制
        /// 超额时按最低 activity、再按最旧时间淘汰非固定条目
        /// </summary>
        public void EnforceMemoryLimits()
        {
            var settings = RimTalkMemoryPatchMod.Settings;

            TrimToCapacity(SCMList, settings.maxSituationalMemories);
            TrimToCapacity(ELSList, settings.maxEventLogMemories);
            TrimToCapacity(CLPAList, settings.maxArchiveMemories);
        }

        // 超额时按最低 activity、再按最旧时间淘汰非固定条目
        // 空元素会被顺便清理
        private static void TrimToCapacity(List<MemoryEntry> memoryList, int max)
        {
            // 清理 null
            memoryList.RemoveAll(m => m is null);

            var nonPinned = memoryList.Where(m => !m.IsPinned).ToList();
            int excess = nonPinned.Count - max;
            if (excess <= 0) return;

            var targetMemories = nonPinned
                .OrderBy(m => m.Activity)
                .ThenBy(m => m.GameTick)
                .Take(excess)
                .ToHashSet();

            memoryList.RemoveAll(targetMemories.Contains);
        }

        /// <summary>
        /// 修改 Pin 状态，自动处理 ABM->SCM 迁移与 RoundMemory 实体化，
        /// 当 memoryId 对应记忆为 RoundMemory 时，复制一份新的 SCM 条目并删除原条目
        /// </summary>
        public void PinMemory(MemoryEntry memory, bool pinned)
        {
            if (memory is null) return;

            // 层级信息或将改为由 UI 端传入
            if (memory.Layer is MemoryLayer.Active)
            {
                ABMList.Remove(memory);

                if (memory is RoundMemory roundMemory)
                {
                    memory = roundMemory.Clone();
                    if (memory is null) return;
                }

                memory.Layer = MemoryLayer.Situational;

                SCMList.Add(memory);
            }

            memory.IsPinned = pinned;
        }

        /// <summary>
        /// 删除指定记忆，返回是否成功删除
        /// </summary>
        public bool Remove(MemoryEntry memory)
        {
            if (memory is null) return false;

            // 或将要求 UI 端传入层级信息
            return ABMList.RemoveAll(m => m == memory) > 0
                | SCMList.RemoveAll(m => m == memory) > 0
                | ELSList.RemoveAll(m => m == memory) > 0
                | CLPAList.RemoveAll(m => m == memory) > 0;
        }
    }
}