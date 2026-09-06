using RimTalk.Memory.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Chronicle;

public class ChronicleAxis : UIElement
{
    // 轴标签与刻度线绘制相关
    private const float AxisLabelGap = 4.0f;
    private const float SmallTickWidth = 2f;
    private const float SmallTickHeight = 14f;
    private const float BigTickWidth = 3f;
    private const float BigTickHeight = 18f;
    private const float LabelWidth = MemoryTabWindow.DefaultWidgetWidth;

    // 刻度步长运算相关
    private const int MaxTickCount = 12;
    private const int MaxYearTickCount = 9;
    private const int MaxRangeForStep1 = 1 * (MaxTickCount + 1) * GenDate.TicksPerDay;
    private const int MaxRangeForStep3 = 3 * (MaxTickCount + 1) * GenDate.TicksPerDay;
    private const int MaxRangeForStep5 = 5 * (MaxTickCount + 1) * GenDate.TicksPerDay;
    private const int MaxRangeForStep15 = 15 * (MaxTickCount + 1) * GenDate.TicksPerDay;
    private const int MaxRangeForStep30 = 30 * (MaxTickCount + 1) * GenDate.TicksPerDay;

    // 配色
    private static Color TickColor => new(0.38f, 0.42f, 0.45f);

    // 成员
    private readonly UIContext _context;
    private readonly MemoryChronicle.Context _chronicleContext; // 快速访问
    private (Rect Rect, int ChronicleStartTick, int ChronicleEndTick) _cacheKey = (Rect.zero, -1, -1);
    private readonly List<(float X, string Label)> _drawDayTick = new();
    private readonly List<(float X, string Label)> _drawQuadrumTick = new();
    private readonly List<(float X, string Label)> _drawYearTick = new();

    // 下属
    private readonly ChronicleCursor _cursor; // 时间轴光标

    public ChronicleAxis(UIContext context)
    {
        _context = context;
        _chronicleContext = _context.GetContext<MemoryChronicle.Context>()
            ?? throw new ArgumentNullException(nameof(context));

        _cursor = new(_context);
    }

    public override void Pulse()
    {
        // 入口校验
        if (_chronicleContext.ChronicleStartTick < _chronicleContext.ChronicleEndTick) base.Pulse();

        _cursor.Pulse();
    }

    protected override void UpdateRects() => _cursor.UpdateRect(_rect);

    protected override void Update()
    {
        CacheCheck();
    }
    // 检查缓存是否需要更新，如果 rect 或 tick 范围发生变化，则重新计算刻度列表和绘制缓存。
    private void CacheCheck()
    {
        int chronicleStartTick = _chronicleContext.ChronicleStartTick;
        int chronicleEndTick = _chronicleContext.ChronicleEndTick;
        if ((_rect, chronicleStartTick, chronicleEndTick) == _cacheKey) return;

        // key 校验不通过，开始重建
        _drawQuadrumTick.Clear();
        _drawDayTick.Clear();
        _drawYearTick.Clear();

        float width = _rect.width;
        // 实际绘制会在局部坐标系下进行，所以从 0，0 开始计算
        float x = 0f;

        int startAbsTick = GenDate.TickGameToAbs(chronicleStartTick);
        int endAbsTick = GenDate.TickGameToAbs(chronicleEndTick);

        var tickList = CalculateTickList(startAbsTick, endAbsTick, out int tickStep);
        float xStep = tickStep / (float)(endAbsTick - startAbsTick) * width;

        bool isFirstTick = true;
        foreach (int tick in tickList)
        {
            if (isFirstTick)
            {
                x += (tick - startAbsTick) / (float)(endAbsTick - startAbsTick) * width;
                isFirstTick = false;
            }
            else x += xStep;

            switch (tick)
            {
                case var _ when tick % GenDate.TicksPerYear == 0:
                    _drawYearTick.Add((x, $"{GenDate.Year(tick, 0L)}年"));
                    break;
                case var _ when tick % GenDate.TicksPerQuadrum == 0:
                    _drawQuadrumTick.Add((x, GenDate.Quadrum(tick, 0L).Label()));
                    break;
                default:
                    _drawDayTick.Add((x, $"第{GenDate.DayOfQuadrum(tick, 0L)}天"));
                    break;
            }
        }

        // 更新 key
        _cacheKey = (_rect, chronicleStartTick, chronicleEndTick);
    }

    protected override void Draw()
    {
        // 背景
        Widgets.DrawBoxSolid(_rect, new Color(0.095f, 0.105f, 0.115f, 0.96f));

        Color tickColor = TickColor;

        // 轴线和两端点
        float leftX = _rect.x;
        float topY = _rect.y;
        Widgets.DrawBoxSolid(new Rect(leftX, topY, _rect.width, 2f), tickColor);

        float leftLineX = leftX + BigTickWidth * 0.5f;
        float rightLineX = _rect.xMax - BigTickWidth * 0.5f;
        float bottomY = _rect.yMax;
        WidgetsUtil.DrawGradientLine(new Vector2(leftLineX, topY), new Vector2(leftLineX, bottomY), tickColor, BigTickWidth);
        WidgetsUtil.DrawGradientLine(new Vector2(rightLineX, topY), new Vector2(rightLineX, bottomY), tickColor, BigTickWidth);

        // 使用 CropBlock 来裁剪绘制区域，避免刻度线和标签超出轴范围
        using (new CropBlock(_rect))
        {
            const float Y = 0f;
            const float LabelY = Y + AxisLabelGap;
            float labelHeight = _rect.height - LabelY;

            var font = GameFont.Tiny;
            float tickWidth = SmallTickWidth;
            float tickHeight = SmallTickHeight;

            // 绘制刻度线和标签
            if (_drawDayTick.Count > 0)
            {
                using (new TextBlock(font))
                    foreach (var (x, label) in _drawDayTick)
                    {
                        WidgetsUtil.DrawGradientLine(new Vector2(x, Y), new Vector2(x, Y + tickHeight), tickColor, tickWidth);
                        Widgets.Label(new Rect(x + AxisLabelGap, LabelY, LabelWidth, labelHeight), label);
                    }
                // 从第二级刻度开始，加大刻度线和标签的尺寸
                font = GameFont.Small;
                tickWidth = BigTickWidth;
                tickHeight = BigTickHeight;
            }

            if (_drawQuadrumTick.Count > 0)
            {
                using (new TextBlock(font))
                    foreach (var (x, label) in _drawQuadrumTick)
                    {
                        WidgetsUtil.DrawGradientLine(new Vector2(x, Y), new Vector2(x, Y + tickHeight), tickColor, tickWidth);
                        Widgets.Label(new Rect(x + AxisLabelGap, LabelY, LabelWidth, labelHeight), label);
                    }
                font = GameFont.Small;
                tickWidth = BigTickWidth;
                tickHeight = BigTickHeight;
            }

            if (_drawYearTick.Count > 0)
            {
                using (new TextBlock(font))
                    foreach (var (x, label) in _drawYearTick)
                    {
                        WidgetsUtil.DrawGradientLine(new Vector2(x, Y), new Vector2(x, Y + tickHeight), tickColor, tickWidth);
                        Widgets.Label(new Rect(x + AxisLabelGap, LabelY, LabelWidth, labelHeight), label);
                    }
            }
        }

        // 滚轮缩放
        var current = Event.current;

        if (current.type is EventType.ScrollWheel && _rect.Contains(current.mousePosition))
        {
            // 拉取变量
            float delta = current.delta.y;
            int startTick = _chronicleContext.ChronicleStartTick;
            int endTick = _chronicleContext.ChronicleEndTick;
            float span = endTick - startTick;

            // 计算并分配 tick 变化量
            float change = span * MathF.Pow(MemoryTabWindow.ScrollZoomFactor, delta) - span;
            int startChange, endChange;

            // 游标在区间内时，会基于游标位置来缩放
            if (_chronicleContext.GetContext<MemoryTabWindow.Context>()?.CursorTick is { } cursorTick
                && startTick <= cursorTick && cursorTick <= endTick)
            {
                float cursorRatio = (cursorTick - startTick) / span;
                startChange = (int)(change * cursorRatio);
                endChange = (int)(change * (1 - cursorRatio));
            }
            // 否则居中缩放
            else
            {
                startChange = endChange = (int)change / 2;
            }

            // 注意当前可能导致在放大至上限（2 天）的瞬间，游标位置出现微小偏移。问题不大。
            _chronicleContext.ChronicleStartTick -= startChange;
            _chronicleContext.ChronicleEndTick += endChange;

            current.Use();
        }
    }

    // 计算刻度列表和步长，步长梯度为 {1，3，5，15，30} + {60，120，300}*10^k，即日、象、年、年 * nice tick。
    // 返回时会把刻度和 step 都换算成绝对 tick，便于后续绘制。
    // 因为是非常纯粹的逻辑运算，所以封装成一个静态方法。
    private static IEnumerable<int> CalculateTickList(int startAbsTick, int endAbsTick, out int step)
    {
        // 使用预计算的阈值和 nice step 计算步长
        int range = endAbsTick - startAbsTick;
        float dayStep = range switch
        {
            <= MaxRangeForStep1 => 1f,
            <= MaxRangeForStep3 => 3f,
            <= MaxRangeForStep5 => 5f,
            <= MaxRangeForStep15 => 15f,
            <= MaxRangeForStep30 => 30f,
            _ => MathUtil.CalculateNiceStep(range / (float)GenDate.TicksPerYear, MaxYearTickCount) * GenDate.DaysPerYear,
        };

        // float dayStep 和 GenerateTicksFromStep 返回的 float day 都是整数 float，可以大胆直接强转 int
        step = (int)dayStep * GenDate.TicksPerDay;
        return MathUtil.GenerateTicksFromStep(
            startAbsTick / (float)GenDate.TicksPerDay,
            endAbsTick / (float)GenDate.TicksPerDay,
            dayStep
            ).Select(day => (int)day * GenDate.TicksPerDay);
    }
}
