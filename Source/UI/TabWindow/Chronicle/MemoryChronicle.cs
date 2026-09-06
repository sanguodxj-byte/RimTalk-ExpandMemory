using RimTalk.Memory.Utils;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Chronicle;

/// <summary>
/// CLPA 横向人生篇章导航器
/// </summary>
public class MemoryChronicle : UIElement
{
    // 常量配置
    private const float TitleHeight = 18f;
    private const float AxisHeight = 28f;
    private const float Gap = MemoryTabWindow.Gap;
    private const int MinChronicleSpanTicks = 2 * GenDate.TicksPerDay;

    // 局部共享上下文包装
    public class Context : UIContext
    {
        // 方便内部快速访问
        private readonly MemoryTabWindow.Context _tabContext;

        // 篇章视窗的起止 Tick
        private int _chronicleStartTick = -1;
        private int _chronicleEndTick = MathUtil.SafeMaxInt;

        // 通过属性保证跨度不小于 MinChronicleSpanTicks 且不越过总范围，总范围约束优先
        public int ChronicleStartTick
        {
            get => _chronicleStartTick;
            set => _chronicleStartTick = Math.Max(
                    Math.Min(value, Math.Min(ChronicleEndTick, _tabContext.LifeCurrentTick) - MinChronicleSpanTicks),
                    _tabContext.LifeStartTick
                    );
        }
        public int ChronicleEndTick
        {
            get => _chronicleEndTick;
            set => _chronicleEndTick = Math.Min(
                    Math.Max(value, Math.Max(ChronicleStartTick, _tabContext.LifeStartTick) + MinChronicleSpanTicks),
                    _tabContext.LifeCurrentTick
                    );
        }

        public Context(UIContext parentContext) : base(parentContext)
        {
            _tabContext = GetContext<MemoryTabWindow.Context>() ?? throw new ArgumentException(nameof(parentContext));
        }
        protected override void Update()
        {
            // 因为 LifeStartTick 和 LifeCurrentTick 会随时变化，故需要每帧收束 ChronicleStartTick 和 ChronicleEndTick
            ChronicleStartTick = ChronicleStartTick;
            ChronicleEndTick = ChronicleEndTick;
        }
    }

    // 成员
    private readonly Context _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private Rect _titleRect;

    // 下属
    private readonly ChronicleCardPainter _cardPainter;
    private readonly ChronicleAxis _axis;

    // 构造函数
    public MemoryChronicle(UIContext context)
    {
        _context = new(context);
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>();

        _cardPainter = new(_context);
        _axis = new(_context);
    }

    public override void Pulse()
    {
        _context.Pulse();

        base.Pulse();

        _cardPainter.Pulse();
        _axis.Pulse();
    }

    public override void PostClose()
    {
        _context.PostClose();
        _cardPainter.PostClose();
        _axis.PostClose();
    }

    protected override void UpdateRects()
    {
        // 三段布局：标题条 → 篇章区 → 时间轴底栏，竖向自上而下排列。
        Rect inner = _rect.ContractedBy(Gap);
        float width = inner.width;
        float x = inner.x;
        float y = inner.y;

        // 标题条绘制：篇章数 + 当前视窗跨度（换算成天）
        Rect titleRect = new(x, y, width, TitleHeight);
        y += TitleHeight + Gap;

        // 时间轴绘制：刻度线 + 日期文本
        float bottomY = inner.yMax;
        Rect axisRect = new(x, bottomY - AxisHeight, width, AxisHeight);
        bottomY -= AxisHeight; // 时间轴和卡片区不隔 Gap

        // 篇章栏绘制：背景色 + 篇章卡片 + 重叠徽标
        float chapterHeight = bottomY - y;
        Rect cardPainterRect = new(x, y, width, chapterHeight);

        _titleRect = titleRect;
        _cardPainter.UpdateRect(cardPainterRect);
        _axis.UpdateRect(axisRect);
    }

    protected override void Draw()
    {
        // 绘制背景
        Widgets.DrawMenuSection(_rect);

        using (new TextBlock(GameFont.Tiny, new Color(0.72f, 0.78f, 0.82f)))
            Widgets.Label(_titleRect, "RimTalk.Memory.UI.TabWindow.ChronicleTitle"
                .Translate((_tabContext?.MemoryComp?.ArchiveMemories?.Count ?? 0).Named("COUNT")));
    }
}
