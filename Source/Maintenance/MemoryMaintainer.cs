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
                sCM?.Decay(settings.ScmDecayRate);

            foreach (var eLS in ELSList)
                eLS?.Decay(settings.ElsDecayRate);

            foreach (var cLPA in CLPAList)
                cLPA?.Decay(settings.ClpaDecayRate);
        }

        /// <summary>
        /// ABM 寿命检查：基于当前 tick 与创建 tick 的差值，
        /// 超出寿命的 Conversation 类型 ABM 转为 SCM，其余类型直接移除
        /// </summary>
        public void ConvertActiveMemories()
        {
            if (Find.TickManager is not { } tickManager) return;

            // 获取配置并校验有效性
            int lifeSpanHours = RimTalkMemoryPatchMod.Settings.AbmLifespanHours;
            if (lifeSpanHours <= 0) return;

            // 获取 ABM 列表
            var abmList = ABMList;
            if (abmList is null || abmList.Count == 0) return;

            // 准备参数
            int removeCount = 0;
            int currentTick = tickManager.TicksGame;
            int lifeSpanTicks = lifeSpanHours * GenDate.TicksPerHour;

            // 特别的，ABM 列表是按时间升序排列的，故此处可以直接正序遍历并提前 break
            for (; removeCount < abmList.Count; removeCount++)
            {
                if (abmList[removeCount] is not { } abm) continue;

                // 若命中一个记忆的寿命仍在范围内，则后续记忆必然更晚，结束循环
                // 考虑在未来规范为 AbsTick 运算
                if (currentTick - abm.GameTick <= lifeSpanTicks) break;

                // 超时，移入 SCM
                if (abm.Type is MemoryType.Action) continue;

                if (abm is RoundMemory roundMemory)
                    abm = roundMemory.Clone();

                abm.Layer = MemoryLayer.Situational;
                SCMList.Add(abm);
            }

            // 移除原列表中的超时条目
            ABMList.RemoveRange(0, removeCount);
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