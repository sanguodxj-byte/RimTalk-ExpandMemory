using RimWorld;
using UnityEngine;

namespace RimTalk.Memory.UI.TabWindow.Chronicle;

/// <summary>
/// CLPA 记忆卡片
/// </summary>
public class ChronicleCard : MemoryCard
{
    // 常量配置
    private const float ShallowCardAlphaFactor = 0.75f;

    // 成员
    private int? _depth = null;

    /// <summary>
    /// 获取或设置记忆卡片的深度值
    /// 获取时，如果 _depth 为 null，则返回默认深度值 DefaultDepth
    /// 设置时，仅在 _depth 为 null 时才会赋值，否则保持原值不变
    /// </summary>
    public int Depth
    {
        get => _depth ?? 0;
        set => _depth ??= value;
    }

    // 背景透明度配置
    protected override float BGAlpha => Depth == 0 ? base.BGAlpha : base.BGAlpha * ShallowCardAlphaFactor;

    // 构造函数
    public ChronicleCard(UIContext context, MemoryEntry memory) : base(context, memory) { }
    public void SetDepth(int depth) => _depth = depth;

    protected override string GetTitle() =>
        $"{GenDate.DateFullStringAt(GenDate.TickGameToAbs(Memory.GameTick), Vector2.zero)} - {GenDate.DateFullStringAt(GenDate.TickGameToAbs(Memory.EndGameTick), Vector2.zero)}";
}
