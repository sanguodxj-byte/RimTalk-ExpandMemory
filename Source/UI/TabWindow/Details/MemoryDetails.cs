using RimTalk.Memory.Utils;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Details;

/// <summary>
/// 记忆详情/编辑区。用于展示和编辑单条记忆的内容、重要性、活跃度、标签和备注等信息。
/// </summary>
public class MemoryDetails : UIElement
{
    private const string NameSpace = "RimTalk.Memory.UI.TabWindow.";

    // 常量配置
    private const float Gap = MemoryTabWindow.Gap;
    private const float WidgetWidth = MemoryTabWindow.DefaultWidgetWidth;
    private const float WidgetHeight = MemoryTabWindow.DefaultWidgetHeight;
    private const float WidgetHeightMedium = 40f;
    private const float TimeYearWidth = 38f; // 年份数字框宽度
    private const float TimeQuadrumWidth = 14f; // 象数字框宽度
    private const float TimeWidth = 22f; // 日/时数字框宽度
    private const float TimeUnitWidth = 14f; // 单位后缀（年/象/日/时）宽度
    private const float TimeStashWidth = 5f; // CLPA 时间编辑行的分隔符宽度
    private const float TimeFieldGap = 2f; // 数字编辑行各元素间距
    private const int MaxEditableYear = 6000; // 时间编辑年份上限（int tick 安全范围）

    private static readonly float TimeHeight = Text.LineHeightOf(GameFont.Small) + 2f;

    // 颜色配置
    private static Color TimeColor => new(0.68f, 0.72f, 0.75f);

    // 局部共享上下文包装
    public class Context : UIContext
    {
        public Context(UIContext parentContext) : base(parentContext) { }

        public MemoryEntry EditingMemory;

        public event Action BeginEdit, SaveEdit, EndEdit;
        public void InvokeBeginEdit() => BeginEdit?.Invoke();
        public void InvokeSaveEdit() => SaveEdit?.Invoke();
        public void InvokeEndEdit() => EndEdit?.Invoke();
    }

    // 成员
    private readonly Context _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private readonly MemoryDetailsMain _mainDetails;

    // 布局缓存
    private Rect _titleRect, _typeRect, _timeRect, _rightButtonRect;
    private Vector2 _topDivider, _bottomDivider;

    // 编辑草稿：时间（年/象/日/时）
    private int
        _startTimeYear, _startTimeQuadrum, _startTimeDay, _startTimeHour, _startTimeSubHourOffset,
        _timeYear, _timeQuadrum, _timeDay, _timeHour, _timeSubHourOffset;
    private string
        _startTimeYearBuf, _startTimeQuadrumBuf, _startTimeDayBuf,
        _timeYearBuf, _timeQuadrumBuf, _timeDayBuf, _timeHourBuf;

    // 构造函数
    public MemoryDetails(UIContext context)
    {
        _context = new(context);
        _tabContext = _context.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));

        _mainDetails = new MemoryDetailsMain(_context);

        _tabContext.FocuseChanged += EndEdit;
    }

    protected override void UpdateRects()
    {
        Rect inRect = _rect.ContractedBy(12f);

        float width = inRect.width;
        float x = inRect.x;
        float y = inRect.y;

        _titleRect = new(x, y, width, WidgetHeightMedium);
        y += WidgetHeightMedium + Gap;

        _typeRect = new(x, y, width, WidgetHeight);
        y += WidgetHeight + Gap;

        _timeRect = new(x, y, width, TimeHeight);
        y += TimeHeight + Gap;

        _topDivider = new(x, y);

        float yBottom = inRect.yMax;

        _rightButtonRect = new(inRect.xMax - WidgetWidth, yBottom - WidgetHeight, WidgetWidth, WidgetHeight);
        yBottom -= WidgetHeight + Gap;

        _bottomDivider = new(x, yBottom);

        _mainDetails.UpdateRect(new(x, y, width, yBottom - y));
    }

    public override void PostClose()
    {
        EndEdit();
        _mainDetails.PostClose();
    }

    public override void Pulse()
    {
        _context.Pulse();
        base.Pulse();
        _mainDetails.Pulse();
    }

    // 绘制详情/编辑区，有编辑态和只读态两种模式，且可**无缝切换**
    protected override void Draw()
    {
        // 绘制背景
        Widgets.DrawMenuSection(_rect);

        // 没有焦点时仅展示引导文本
        if (_tabContext.Focuse is not { } focuse)
        {
            Widgets.Label(_rect.ContractedBy(12f), (NameSpace + "Guide").Translate());
            return;
        }

        // 标题
        using (new TextBlock(GameFont.Medium))
            Widgets.Label(_titleRect, (NameSpace + "Details").Translate());

        // 记忆类型和层级
        Widgets.Label(_typeRect, $"{focuse.Layer.Translate()} · {focuse.Type.Translate()}");

        // 编辑态标记：当前是否正在编辑记忆
        bool editing = _context.EditingMemory is not null;

        // 编辑态持续强制暂停；玩家必须保存或取消后才能恢复游戏时间。
        if (editing) Find.TickManager?.Pause();

        // 记忆时间：编辑态显示可编辑时间输入行，只读态显示时间描述
        if (editing)
            DrawTimeEditor();
        else
            using (new TextBlock(TimeColor))
                Widgets.Label(_timeRect, focuse.AgeString);

        // 主要内容由 MemoryContent 绘制，IMGUI 只留空并绘制上下分隔线
        float width = _rect.ContractedBy(12f).width;
        Widgets.DrawLineHorizontal(_topDivider.x, _topDivider.y, width);
        Widgets.DrawLineHorizontal(_bottomDivider.x, _bottomDivider.y, width);

        // 底部按钮：编辑/保存/取消
        if (editing)
        {
            if (Widgets.ButtonText(_rightButtonRect, (NameSpace + "CancelEdit").Translate()))
                EndEdit();

            if (Widgets.ButtonText(
                new Rect(_rightButtonRect.x - Gap - WidgetWidth, _rightButtonRect.y, WidgetWidth, WidgetHeight),
                (NameSpace + "SaveEdit").Translate()
                ))
                SaveEdit();
        }
        else if (Widgets.ButtonText(_rightButtonRect, (NameSpace + "Edit").Translate()))
            BeginEdit(focuse);
    }

    // 时间编辑行：普通 xx年xx象xx日xx时；Archive xx年xx象xx日-xx年xx象xx日
    private void DrawTimeEditor()
    {
        using var _ = new TextBlock(TextAnchor.MiddleCenter);

        float x = _timeRect.x;
        float y = _timeRect.y;

        bool isArchive = _context.EditingMemory.Layer is MemoryLayer.Archive;

        if (isArchive)
        {
            // CLPA 起始点：年+象+日
            DrawTimeField(ref x, y, ref _startTimeYear, ref _startTimeYearBuf, TimeYearWidth, GenDate.DefaultStartingYear, MaxEditableYear, (NameSpace + "TimeYear").Translate());
            DrawTimeField(ref x, y, ref _startTimeQuadrum, ref _startTimeQuadrumBuf, TimeQuadrumWidth, 1, 4, (NameSpace + "TimeQuadrum").Translate());
            DrawTimeField(ref x, y, ref _startTimeDay, ref _startTimeDayBuf, TimeWidth, 1, 15, (NameSpace + "TimeDay").Translate());

            // 分隔符
            Widgets.Label(new(x, _timeRect.y, TimeStashWidth, _timeRect.height), "-");
            x += TimeStashWidth + TimeFieldGap;
        }

        // 年+象+日
        DrawTimeField(ref x, y, ref _timeYear, ref _timeYearBuf, TimeYearWidth, GenDate.DefaultStartingYear, MaxEditableYear, (NameSpace + "TimeYear").Translate());
        DrawTimeField(ref x, y, ref _timeQuadrum, ref _timeQuadrumBuf, TimeQuadrumWidth, 1, 4, (NameSpace + "TimeQuadrum").Translate());
        DrawTimeField(ref x, y, ref _timeDay, ref _timeDayBuf, TimeWidth, 1, 15, (NameSpace + "TimeDay").Translate());

        if (!isArchive)
            // 时
            DrawTimeField(ref x, y, ref _timeHour, ref _timeHourBuf, TimeWidth, 0, 23, (NameSpace + "TimeHour").Translate());
    }

    // 数字输入框 + 单位后缀
    private static void DrawTimeField(ref float x, float y, ref int value, ref string buffer, float width, int min, int max, string unitKey)
    {
        Widgets.TextFieldNumeric(new(x, y, width, TimeHeight), ref value, ref buffer, min, max);
        x += width + TimeFieldGap;

        Widgets.Label(new(x, y, TimeUnitWidth, TimeHeight), unitKey.Translate());
        x += TimeUnitWidth + TimeFieldGap;
    }

    // 编辑器操作独立草稿，保存前不修改业务对象。
    private void BeginEdit(MemoryEntry memory)
    {
        Find.TickManager?.Pause();

        _context.EditingMemory = memory;

        int startAbsTick = GenDate.TickGameToAbs(_context.EditingMemory.StartGameTick);
        _startTimeYear = GenDate.Year(startAbsTick, 0f);
        _startTimeQuadrum = (int)GenDate.Quadrum(startAbsTick, 0f) + 1;
        _startTimeDay = GenDate.DayOfQuadrum(startAbsTick, 0f) + 1;
        _startTimeHour = GenDate.HourOfDay(startAbsTick, 0f);
        _startTimeSubHourOffset = startAbsTick % GenDate.TicksPerHour;

        int endAbsTick = GenDate.TickGameToAbs(_context.EditingMemory.GameTick);
        _timeYear = GenDate.Year(endAbsTick, 0f);
        _timeQuadrum = (int)GenDate.Quadrum(endAbsTick, 0f) + 1;
        _timeDay = GenDate.DayOfQuadrum(endAbsTick, 0f) + 1;
        _timeHour = GenDate.HourOfDay(endAbsTick, 0f);
        _timeSubHourOffset = endAbsTick % GenDate.TicksPerHour;

        _context.InvokeBeginEdit();
    }

    // 保存：把编辑草稿应用回记忆本体
    private void SaveEdit()
    {
        var memory = _context.EditingMemory;
        memory.GameTick = GenDate.TickAbsToGame(
            (_timeYear - GenDate.DefaultStartingYear) * GenDate.TicksPerYear
            + (_timeQuadrum - 1) * GenDate.TicksPerQuadrum
            + (_timeDay - 1) * GenDate.TicksPerDay
            + _timeHour * GenDate.TicksPerHour
            + _timeSubHourOffset
            );
        if (memory.Layer is MemoryLayer.Archive)
            memory.StartGameTick = GenDate.TickAbsToGame(
                (_startTimeYear - GenDate.DefaultStartingYear) * GenDate.TicksPerYear
                + (_startTimeQuadrum - 1) * GenDate.TicksPerQuadrum
                + (_startTimeDay - 1) * GenDate.TicksPerDay
                + _startTimeHour * GenDate.TicksPerHour
                + _startTimeSubHourOffset
                );

        _context.InvokeSaveEdit();

        memory.IsUserEdited = true;
        EndEdit();
    }

    private void EndEdit()
    {
        _context.EditingMemory = null;
        _timeYearBuf = _timeQuadrumBuf = _timeDayBuf = _timeHourBuf
            = _startTimeYearBuf = _startTimeQuadrumBuf = _startTimeDayBuf
            = null;

        _context.InvokeEndEdit();
    }
}
