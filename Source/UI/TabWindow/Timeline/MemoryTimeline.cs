using RimTalk.Memory.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Timeline;

public class MemoryTimeline : ScrollUIElement
{
    // 布局常量
    private const float Gap = MemoryTabWindow.Gap;
    private const float CardGap = 6f;
    private const float LongTimeGap = 28f;
    private const float QuadrumHeaderHeight = 33f;
    private const float YearHeaderHeight = 38f;

    private const float AxisLeftGap = 2 * Gap;
    private const float AxisRightGap = 3 * Gap;
    private const float LineWidth = 2f;
    private const float PointRadius = 4f;
    private const float TinyFontHeight = 17f;

    // 逻辑常量
    private const int LongTimeThreshold = 2 * GenDate.TicksPerDay; // 规则3的时间阈值：两天整

    // 颜色
    private static Color AxisColor => new(0.34f, 0.39f, 0.43f, 0.9f);

    // 成员
    private readonly UIContext _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private readonly Dictionary<MemoryEntry, TimelineCard> _cardMap = new(); // 懒加载缓存
    private readonly List<MemoryEntry> _timelineMemories = new(); // 私有一份记忆列表副本，每帧从 TabContext 拉取，自维护以确保状态和 _yLayout 一致
    private readonly HashSet<MemoryEntry> _timelineMemoriesHash = new(); // 清洗集合，每帧重建，下沉为字段以避免 GC 开销
    private float[] _yLayout = []; // 绝对布局数组
    private readonly List<TimelineCard> _visableCards = new(); // 可见卡片集合，时间倒序
    private readonly Func<MemoryEntry, TimelineCard> _cardFactory; // 卡片工厂缓存，避免布局热路径上每张卡分配闭包

    // 构造函数
    public MemoryTimeline(UIContext context)
    {
        _context = context;
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));

        _tabContext.MemoryCompReseted += MemoryCompReseted;
        _tabContext.RePositionTimeline += RePositionTimeline;

        _cardFactory = memory => new TimelineCard(_context, memory);
    }

    // 切换记忆组件时清空卡片缓存
    private void MemoryCompReseted()
    {
        _cardMap.Clear();
    }

    // 篇章光标移动了核心锚点，滚动视图重新定位到离锚点最近的卡片，并更新可见卡片集合
    private void RePositionTimeline()
    {
        if (_timelineMemories.Count == 0) return;

        int cursorTick = _tabContext.CursorTick;
        int breakInIndex = _timelineMemories
            .Where(memory => memory is not null)
            .Select((memory, index) => (memory, index))
            .MinBy(tuple => Math.Abs(cursorTick - tuple.memory.GameTick))
            .index;

        _scrollPosition.y = _yLayout[breakInIndex] + TimelineCard.HeightOf(_timelineMemories[breakInIndex]) - _rect.height * 0.5f;

        UpdateVisibleCards(breakInIndex);
    }

    public override void UpdateRect(Rect rect)
    {
        _rect = rect;
        _totalRect.width = _rect.width - MemoryTabWindow.ScrollbarWidth;

        // 因为是虚拟化列表，视窗变化时需要重新计算可见卡片集合
        UpdateVisibleCards();
    }

    public override void ScrollPulse()
    {
        // 监听用户滚动事件
        Vector2 scrollPosition = _scrollPosition;
        Widgets.BeginScrollView(_rect, ref _scrollPosition, _totalRect);
        if (scrollPosition != _scrollPosition) OnUserScrolled();

        Pulse();

        Widgets.EndScrollView();
    }

    public override void Pulse()
    {
        base.Pulse();
        foreach (var card in _visableCards) card.Pulse();
    }

    public override void PostClose()
    {
        _cardMap.Clear();
        _timelineMemories.Clear();
        _yLayout = [];
        _visableCards.Clear();
    }

    protected override void Update()
    {
        // 每帧重建布局
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        // 无论上游数据是否合法，都清空旧的记忆列表，避免状态不一致
        _timelineMemories.Clear();

        if (_tabContext.TreatedTimelineMemories is not { } timelineMemories
            || !(timelineMemories.Count is var count)
            || count == 0)
        {
            _visableCards.Clear();
            _totalRect.height = 0;
            return;
        }

        // 拉取最新记忆列表
        _timelineMemories.Clear();
        _timelineMemories.AddRange(timelineMemories);

        // 清洗旧 _visableCards
        _timelineMemoriesHash.UnionWith(_timelineMemories);
        _visableCards.RemoveAll(card => !_timelineMemoriesHash.Contains(card.Memory));
        _timelineMemoriesHash.Clear();

        int cursorTick = _tabContext.CursorTick;

        // 锚点：干净旧集合中离探针最近（同距偏新）的卡片，可能不存在
        var anchor = _visableCards.Count == 0
            ? null
            : _visableCards.MinBy(card => Math.Abs(cursorTick - card.Memory.GameTick));

        // 构建绝对布局
        if (_yLayout.Length < count)
        {
            _yLayout = new float[count];
            _yLayout[0] = 0f;
        }
        bool hasAnchor = anchor is not null;
        float y = 0;
        long lastAbsTick = -1;
        int breakInIndex = -1;

        for (int i = 0; i < count; i++)
        {
            if (_timelineMemories[i] is not { } memory) continue;

            int absTick = GenDate.TickGameToAbs(memory.GameTick);

            // 首个元素不加 gap
            if (lastAbsTick != -1)
                y += GenDate.Year(lastAbsTick, 0f) != GenDate.Year(absTick, 0f)
                    ? YearHeaderHeight
                    : GenDate.Quadrum(lastAbsTick, 0f) != GenDate.Quadrum(absTick, 0f)
                    ? QuadrumHeaderHeight
                    : lastAbsTick - absTick > LongTimeThreshold
                    ? LongTimeGap : CardGap;

            lastAbsTick = absTick;

            // 记录卡片左上角纵坐标
            _yLayout[i] = y;

            // 锚定补偿：滚动位置随锚卡绝对坐标的位移同步平移
            if (hasAnchor)
            {
                if (memory == anchor.Memory)
                {
                    _scrollPosition.y += y - anchor.Rect.y;
                    breakInIndex = i;
                }
            }
            // 非锚定：窗口位置不会调整，直接锁定完全在窗口内的第一张卡片的 index
            else if (breakInIndex == -1 && y >= _scrollPosition.y)
            {
                breakInIndex = i;
            }

            // 累加卡片高度
            y += TimelineCard.HeightOf(memory);
        }

        // 记录内容总高度，并限制滚动位置在合理范围内
        _totalRect.height = y;
        _scrollPosition.y = Math.Clamp(_scrollPosition.y, 0f, Math.Max(0f, y - _rect.height));

        // 更新可见卡片集合
        UpdateVisibleCards(breakInIndex);
    }

    // 更新可见卡片集合，允许主动传入 breakInIndex 作为锚点索引以改善性能，若未传入则自动计算
    private void UpdateVisibleCards(int breakInIndex = -1)
    {
        if (_timelineMemories.Count == 0) return;

        if (breakInIndex == -1) breakInIndex = GetStartIndex();

        // 从 breakInIndex 向上向下扩展，直到超出视口范围
        float top = _scrollPosition.y;
        float bottom = top + _rect.height;
        int startIndex = 0;

        _visableCards.Clear();
        for (int i = breakInIndex; i >= 0; i--)
        {
            _visableCards.Add(_cardMap.GetOrAdd(_timelineMemories[i], _cardFactory));
            if (_yLayout[i] < top)
            {
                // 过盈匹配
                if (i > 0)
                {
                    _visableCards.Add(_cardMap.GetOrAdd(_timelineMemories[i - 1], _cardFactory));
                    startIndex = i - 1;
                }
                else startIndex = i;

                break;
            }
        }
        for (int i = breakInIndex + 1; i < _timelineMemories.Count; i++)
        {
            _visableCards.Add(_cardMap.GetOrAdd(_timelineMemories[i], _cardFactory));
            if (_yLayout[i] > bottom) break;
        }
        // 倒序排列
        _visableCards.SortBy(card => -card.Memory.GameTick);

        // 直接在 _yLayout 中切片并传递给 UpdateRects 更新布局
        UpdateRects(startIndex);
    }

    // 更新可见卡片的布局，允许主动传入起始索引以改善性能，若未传入则自动计算
    protected override void UpdateRects() => UpdateRects(-1);
    private void UpdateRects(int startIndex = -1)
    {
        if (startIndex == -1) startIndex = GetStartIndex();
        Span<float> yLayout = _yLayout.AsSpan(startIndex, _visableCards.Count);

        const float X = 0f + AxisLeftGap + AxisRightGap;
        float width = _totalRect.width - X;

        for (int i = 0; i < _visableCards.Count; i++)
        {
            var card = _visableCards[i];
            card.UpdateRect(new Rect(X, yLayout[i], width, TimelineCard.HeightOf(card.Memory)));
        }
    }

    // 获取视窗中首个可见卡片的索引，若没有则返回 -1
    private int GetStartIndex()
    {
        float top = _scrollPosition.y;
        for (int i = 0; i < _yLayout.Length; i++)
            if (_yLayout[i] >= top)
                return i >= 1 ? i - 1 : i;
        return -1;
    }

    // 滚动滑窗时，更新可见卡片集合，并将离视窗中心最近的卡片的时间点作为锚点，同时触发外部事件通知
    private void OnUserScrolled()
    {
        UpdateVisibleCards();

        float centerY = _scrollPosition.y + _rect.height * 0.5f;
        _tabContext.CursorTick = _visableCards.MinBy(card => Math.Abs(card.Rect.center.y - centerY)).Memory.GameTick;

        _tabContext.RaiseRePositionCursor();
    }

    protected override void Draw()
    {
        if (_visableCards.Count == 0) return;

        const float AxisX = 0f + AxisLeftGap;
        float width = _totalRect.width;
        Color axisColor = AxisColor;

        // 绘制卡片修饰
        Vector2 lastCenter = new(AxisX, 0f); // 一定会有一条通向视窗顶部外的线
        float lastYMax = MathUtil.SafeMaxInt; // 这条虚拟线必须是实线
        int lastTick = MathUtil.SafeMinInt; // 同上

        for (int i = 0; i < _visableCards.Count; i++)
        {
            var card = _visableCards[i];
            Rect rect = card.Rect;

            // 绘制点和渐变线
            Vector2 center = new(AxisX, rect.center.y);
            WidgetsUtil.CircleImage(center, PointRadius, axisColor);
            WidgetsUtil.DrawGradientLine(
                center, new Vector2(center.x + AxisRightGap, center.y),
                axisColor, LineWidth
                );

            // 计算时间 gap 和布局 gap
            int currentTick = card.Memory.GameTick;
            int gapTick = lastTick - currentTick;
            float currentY = rect.y;
            float gap = currentY - lastYMax;

            // 时间间隔未达阈值时绘制实线，反之绘制虚线
            if (gapTick <= LongTimeThreshold)
            {
                Widgets.DrawLine(lastCenter, center, axisColor, LineWidth);
            }
            else
            {
                // 绘制虚线
                WidgetsUtil.DrawDashedLine(lastCenter, center, axisColor, LineWidth, 8f, 5f);

                // 绘制文本和“文本框”
                Rect gapLabelRect = new(0f, lastYMax + gap * 0.5f - TinyFontHeight * 0.5f, width, TinyFontHeight);
                Widgets.DrawBoxSolid(gapLabelRect, Widgets.MenuSectionBGFillColor);
                using (new TextBlock(GameFont.Tiny))
                    Widgets.Label(gapLabelRect, gapTick switch
                    {
                        < GenDate.TicksPerDay * 14 => $"{gapTick / GenDate.TicksPerDay}天后",
                        < GenDate.TicksPerDay * 30 => $"{gapTick / (GenDate.TicksPerDay * 7)}周后",
                        < GenDate.TicksPerDay * 120 => $"{gapTick / GenDate.TicksPerQuadrum}象后",
                        _ => $"{gapTick / GenDate.TicksPerYear}年后"
                    });
            }

            // 空间间隔达到阈值，说明此处需要绘制自然时间分隔符
            if (gap >= QuadrumHeaderHeight)
            {
                // 居中绘制年/象 head
                Rect gapTitleRect = new(0f, lastYMax, width, gap);
                using (new TextBlock(TextAnchor.MiddleCenter))
                    Widgets.Label(gapTitleRect, gap == QuadrumHeaderHeight
                        ? GenDate.Quadrum(currentTick, 0f).Label()
                        : $"{GenDate.Year(currentTick, 0f)}年");

                // 在文字上方绘制一条渐变线
                float y = gapTitleRect.center.y - 22f * 0.5f;
                WidgetsUtil.DrawGradientLine(new Vector2(AxisX + LineWidth * 0.5f, y), new Vector2(_totalRect.xMax, y), axisColor, LineWidth);
            }

            // 更新数据
            lastCenter = center;
            lastYMax = rect.yMax;
            lastTick = currentTick;
        }

        // 一定会有一条通向视窗底部外的实线
        Widgets.DrawLine(lastCenter, new Vector2(AxisX, _totalRect.yMax), axisColor, LineWidth);
    }
}
