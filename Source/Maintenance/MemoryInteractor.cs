using System.Collections.Generic;

namespace RimTalk.Memory.Maintenance;

/// <summary>
/// 记忆仓库交互器，
/// 用于向 UI 层提供交互接口
/// </summary>
public class MemoryInteractor
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
    public MemoryInteractor(FourLayerMemoryComp memoryComp)
    {
        _memoryComp = memoryComp;
    }

    /// <summary>
    /// 修改 Pin 状态，自动处理 ABM->SCM 迁移与 RoundMemory 实体化，
    /// 当 memoryId 对应记忆为 RoundMemory 时，复制一份新的 SCM 条目并删除原条目
    /// </summary>
    public void PinMemory(MemoryEntry memory, bool isPinned)
    {
        if (memory is null) return;

        // 层级信息或将改为由 UI 端传入
        if (isPinned && memory.Layer == MemoryLayer.Active)
        {
            ABMList.Remove(memory);

            memory = memory.Privatize();

            memory.Layer = MemoryLayer.Situational;

            SCMList.Add(memory);
        }

        memory.IsPinned = isPinned;
    }

    /// <summary>
    /// 删除指定记忆，返回是否成功删除
    /// </summary>
    public bool RemoveMemory(MemoryEntry memory)
    {
        if (memory is null) return false;

        // 或将要求 UI 端传入层级信息
        return ABMList.RemoveAll(m => m == memory) > 0
            | SCMList.RemoveAll(m => m == memory) > 0
            | ELSList.RemoveAll(m => m == memory) > 0
            | CLPAList.RemoveAll(m => m == memory) > 0;
    }

    /// <summary>
    /// 添加记忆条目到对应层级
    /// </summary>
    public void AddMemory(MemoryEntry memory)
    {
        if (memory is null) return;
        switch (memory.Layer)
        {
            case MemoryLayer.Active: ABMList.Add(memory); return;
            case MemoryLayer.Situational: SCMList.Add(memory); return;
            case MemoryLayer.EventLog: ELSList.Add(memory); return;
            case MemoryLayer.Archive: CLPAList.Add(memory); return;
            default: return;
        }
    }
}
