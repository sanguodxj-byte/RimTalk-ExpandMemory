using RimTalk.MemoryPatch;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory.Capture;

/// <summary>
/// 补充捕获 <see cref="JobMemoryCapturer"/> 无法覆盖的战斗动作。
/// 仅负责征召 <c>Wait_Combat</c> 中由 <c>JobDriver_Wait.CheckForAutoAttack</c>
/// 直接触发、不创建攻击 Job 的自动攻击；通过 <c>Pawn.Notify_UsedVerb</c> 的
/// Postfix 接入，该通知只在 <c>TryCastShot</c> 成功后发出。
/// <para>
/// AttackMelee / AttackStatic 等有攻击 Job 的行为已由 <see cref="JobMemoryCapturer"/>
/// 通过 <c>_jobsAlwaysCapture</c> 在 Job 结束时统一捕获，不再经过此管线。
/// </para>
/// </summary>
public class CombatMemoryCapturer
{
    // --- 静态配置 ---

    // 同一战斗会话的最长活动间隔（半小时）
    private const int SessionTimeoutTicks = GenDate.TicksPerHour / 2;
    // 单次战斗会话的最长持续时间（两小时）
    private const int SessionMaxDurationTicks = 2 * GenDate.TicksPerHour;
    // 战斗动作的基础重要性
    private const float CombatImportance = 0.9f;
    // 对地攻击使用的固定 thingID key（Thing.thingIDNumber 不会取到负值）
    private const int CellTargetId = -1;
    private const string CellTargetName = "目标区域";

    // --- 实例成员 ---

    // 父组件（记忆组件）的引用
    // 与 JobMemoryCapturer 一致，由 FourLayerMemoryComp 在构造时保证非空
    private readonly FourLayerMemoryComp _memoryComp;

    // 最新一次战斗记忆及其聚合会话状态
    // 状态无需序列化：读档后的首次攻击自然开启新会话
    private MemoryEntry _lastCombatMemory;
    private int _startGameTick;
    private int _lastActiveTick;
    private bool _lastAttackWasMelee;

    // 内部以 thingID 为 key 区分目标个体；显示名快照避免 Thing 后续 Discard 不可读
    private Dictionary<string, HashSet<int>> _dictTargetNameToIds;
    // 懒加载
    private Dictionary<string, HashSet<int>> DictTargetNameToIds => _dictTargetNameToIds ??= new();

    // 实例构造函数
    public CombatMemoryCapturer(FourLayerMemoryComp memoryComp)
    {
        _memoryComp = memoryComp;
    }


    // --- 外部调用接口 ---

    /// <summary>
    /// 战斗动作捕获入口。
    /// 由 Pawn.Notify_UsedVerb 的 Postfix 调用；该原版通知只在 TryCastShot 成功后发出，
    /// 因而记录的是实际执行的攻击，而不是开始瞄准但可能被中断的攻击意图。
    /// </summary>
    public static void CaptureAttackEnter(Pawn pawn, Verb verb)
    {
        if (// 配置项
            !RimTalkMemoryPatchMod.Settings.EnableCombatMemory

            // 仅捕获殖民者的有效攻击 Verb
            || pawn is null
            || !pawn.IsColonist
            || verb is null

            // 仅处理 JobCapturer 无法覆盖的 Wait_Combat 自动攻击
            || pawn.CurJobDef != JobDefOf.Wait_Combat

            // 剔除灭火/纵火（Ranged=false）和非暴力 utility（violent=false）等非攻击 Verb
            || !(verb.IsMeleeAttack || verb.verbProps is { Ranged: true, violent: true }))
            return;

        pawn.GetComp<FourLayerMemoryComp>()?.CombatCapturer?.CaptureAttack(verb);
    }


    // --- 实例方法 ---

    /// <summary>
    /// 捕获并聚合一次实际攻击。
    /// 同一攻击方式、活动间隔与会话时长均未超时的情况下就地更新上一条记忆；
    /// 不同个体以 thingID 区分，连发同一目标不增加计数。
    /// </summary>
    private void CaptureAttack(Verb verb)
    {
        int currentTick = GenTicks.TicksGame;
        bool isMelee = verb.IsMeleeAttack;
        int targetId = GetTargetId(verb.CurrentTarget);
        string targetName = GetTargetName(verb.CurrentTarget);

        // --- 战斗记忆聚合管线 ---
        // 同一攻击方式 + 活动间隔与会话总长均未超时 + 存在可堆叠的记忆
        if (currentTick - _startGameTick <= SessionMaxDurationTicks
            && currentTick - _lastActiveTick <= SessionTimeoutTicks
            && isMelee == _lastAttackWasMelee
            && _lastCombatMemory is not null)
        {
            // 录入新目标的 id
            if (!DictTargetNameToIds.TryGetValue(targetName, out var idList))
                idList = DictTargetNameToIds[targetName] = new();

            idList.Add(targetId);

            _lastActiveTick = currentTick;
            _lastCombatMemory.Content = BuildContent();
            return;
        }

        // --- 战斗记忆新建管线 ---
        // 初始化新会话状态
        _startGameTick = _lastActiveTick = currentTick;
        _lastAttackWasMelee = isMelee;
        DictTargetNameToIds.Clear();
        DictTargetNameToIds[targetName] = [targetId];

        // 创建记忆条目并添加到记忆组件
        _lastCombatMemory = new MemoryEntry(
            BuildContent(),
            MemoryType.Action,
            MemoryLayer.Active,
            CombatImportance);
        _memoryComp.ActiveMemories.Add(_lastCombatMemory);
    }

    /// <summary>
    /// 提取攻击目标的稳定 ID。
    /// Thing 以 thingIDNumber 为 key，跨同名个体可分；对地攻击使用固定 key 单独成桶。
    /// </summary>
    private static int GetTargetId(LocalTargetInfo target) =>
        target.HasThing ? target.Thing.thingIDNumber : CellTargetId;

    /// <summary>
    /// 提取攻击目标名称。
    /// 对地攻击没有 Thing，使用稳定的区域描述；Thing 名称在事件发生当刻完成快照。
    /// </summary>
    private static string GetTargetName(LocalTargetInfo target) =>
        target.Thing is { } thing
        ? thing.LabelShort ?? thing.def?.label ?? "目标"
        : CellTargetName;

    /// <summary>
    /// 生成单次或聚合后的战斗记忆文本。
    /// 单一个体：省时长、省计数；多个个体：固定"连续"前缀 + 按显示名分桶（前3，其余以"等"省略）。
    /// 字典本身已按显示名分桶，直接遍历即可，无需二次 GroupBy。
    /// </summary>
    private string BuildContent()
    {
        string action = _lastAttackWasMelee ? "近身攻击" : "射击";

        // 单目标：会话内仅命中一个个体的情形
        if (DictTargetNameToIds.Count == 1
            && DictTargetNameToIds.First() is { Value.Count: 1 } kvp)
            return $"{action}{kvp.Key}";

        // 多目标：按命中个体数降序（前3，其余以"等"省略）
        var names = DictTargetNameToIds
            .OrderByDescending(kv => kv.Value.Count)
            .ToList();

        return $"连续{action}了{string.Join("、", names.Take(3).Select(kv => $"{kv.Value.Count}个{kv.Key}"))}{(names.Count > 3 ? "等" : "")}";
    }
}
