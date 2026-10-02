using RimTalk.Memory.Utils;
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow;

/// <summary>
/// 记忆详情/编辑区。用于展示和编辑单条记忆的内容、重要性、活跃度、标签和备注等信息。
/// </summary>
public class MemoryDetails : UIElement
{
    private const string NameSpace = "RimTalk.Memory.UI.TabWindow.";

    // 常量配置
    private const float ListingStandardGap = 14f;
    private const float ButtonWidth = MemoryTabWindow.DefaultWidgetWidth;
    private const float ButtonHeight = MemoryTabWindow.DefaultWidgetHeight;
    private const float SliderLabelPct = 0.35f; // 滑条行左侧标签宽度占比
    private const float TimeYearWidth = 38f; // 年份数字框宽度
    private const float TimeQuadrumWidth = 14f; // 象数字框宽度
    private const float TimeWidth = 22f; // 日/时数字框宽度
    private const float TimeUnitWidth = 14f; // 单位后缀（年/象/日/时）宽度
    private const float TimeStashWidth = 5f; // CLPA 时间编辑行的分隔符宽度
    private const float TimeFieldGap = 2f; // 数字编辑行各元素间距
    private const int MaxEditableYear = 6000; // 时间编辑年份上限（int tick 安全范围）

    private static readonly float TimeHeight = Text.LineHeightOf(GameFont.Small) + new Listing_Standard().verticalSpacing; // 时间编辑行高度，与 Listing_Standard.Label 行高一致

    // 颜色配置
    private static Color TimeColor => new(0.68f, 0.72f, 0.75f);
    private static Color NoteColor => new(0.78f, 0.78f, 0.72f);

    // 成员
    private readonly UIContext _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private Vector2 _scrollPosition;
    private float _scrollRectHeight = 760f;

    // 编辑态
    private MemoryEntry _editingMemory;
    private int _startTimeYear, _startTimeQuadrum, _startTimeDay, _startTimeHour, _startTimeSubHourOffset; // 起点草稿：年/象/日/时
    private int _timeYear, _timeQuadrum, _timeDay, _timeHour, _timeSubHourOffset; // 时间草稿：年/象/日/时
    private string _startTimeYearBuf, _startTimeQuadrumBuf, _startTimeDayBuf;
    private string _timeYearBuf, _timeQuadrumBuf, _timeDayBuf, _timeHourBuf;
    private string _content;
    private string _tags;
    private string _notes;
    private float _importance;
    private float _activity;

    // 构造函数
    public MemoryDetails(UIContext context)
    {
        _context = context;
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));

        _tabContext.FocuseChanged += OnFocuseChanged;
    }
    private void OnFocuseChanged()
    {
        _scrollPosition = Vector2.zero;
        EndEdit();
    }

    public override void PostClose() => EndEdit();

    // 绘制详情/编辑区，有编辑态和只读态两种模式，且可**无缝切换**
    protected override void Draw()
    {
        // 绘制背景并取得工作区
        Widgets.DrawMenuSection(_rect);
        Rect inRect = _rect.ContractedBy(12f);

        // 没有焦点时仅展示引导文本
        if (_tabContext.Focuse is not { } focuse)
        {
            Widgets.Label(inRect, (NameSpace + "Guide").Translate());
            return;
        }

        // 启动 Listing_Standard
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // 标题
        using (new TextBlock(GameFont.Medium))
            listing.Label((NameSpace + "Details").Translate());
        listing.Gap();

        // 记忆类型和层级
        listing.Label($"{focuse.Layer.Translate()} · {focuse.Type.Translate()}");
        listing.Gap();

        // 编辑态标记：当前是否正在编辑记忆
        bool editing = _editingMemory is not null;

        // 记忆时间：编辑态显示可编辑时间输入行，只读态显示时间描述
        if (editing)
            DrawTimeEditor(listing.GetRect(TimeHeight));
        else
            using (new TextBlock(TimeColor))
                listing.Label(focuse.AgeString);
        listing.GapLine();

        // 编辑态持续强制暂停；玩家必须保存或取消后才能恢复游戏时间。
        if (editing) Find.TickManager?.Pause();

        // 分配并启动滚动区
        Rect outRect = listing.GetRect(inRect.height - listing.CurHeight - ListingStandardGap - ButtonHeight);
        Rect scrollRect = new(0f, 0f, inRect.width - MemoryTabWindow.ScrollbarWidth, _scrollRectHeight);
        Widgets.BeginScrollView(outRect, ref _scrollPosition, scrollRect);
        var scrollListing = new Listing_Standard { maxOneColumn = true };
        scrollListing.Begin(scrollRect);

        // 记忆内容
        // 编辑态和只读态使用同一家族的 GUIStyle，保证字体、行距、边距一致，并统一计算和分配高度
        var style = editing ? Text.CurTextAreaStyle : Text.CurTextAreaReadOnlyStyle;
        Rect contentRect = scrollListing.GetRect(MathF.Max(Text.LineHeight, style.CalcHeight(
            new GUIContent(editing ? _content : focuse.Content),
            scrollRect.width
            )));
        if (editing)
            _content = GUI.TextArea(contentRect, _content, style);
        else
            GUI.Label(contentRect, focuse.Content, style);
        scrollListing.GapLine();

        // 重要性
        if (editing)
            _importance = scrollListing.SliderLabeled(
                (NameSpace + "Importance").Translate(_importance.ToString("F2").Named("IMPORTANCE")),
                _importance, 0f, 1f, SliderLabelPct
                );
        else
            scrollListing.Label((NameSpace + "Importance").Translate(focuse.Importance.ToString("F2").Named("IMPORTANCE")));
        scrollListing.Gap();

        // 活跃度
        if (editing)
            _activity = scrollListing.SliderLabeled(
                (NameSpace + "Activity").Translate(_activity.ToString("F2").Named("ACTIVITY")),
                _activity, 0f, 1f, SliderLabelPct
                );
        else
            scrollListing.Label((NameSpace + "Activity").Translate(focuse.Activity.ToString("F2").Named("ACTIVITY")));
        scrollListing.Gap();

        // 标签
        if (editing)
            _tags = scrollListing.TextEntry(_tags);
        else
            scrollListing.Label((NameSpace + "Tags").Translate() + string.Join(", ", focuse.Tags ?? []));
        scrollListing.Gap();

        // 备注
        if (editing)
            scrollListing.TextEntry(_notes, 4);
        else
            using (new TextBlock(NoteColor))
                scrollListing.Label((NameSpace + "Notes").Translate() + focuse.Note);
        scrollListing.Gap();

        // 记忆状态：是否已固定、是否正在总结/已总结/未总结
        var summarizer = _tabContext.MemoryComp?.Summarizer;
        scrollListing.Label(
            $"{(focuse.IsPinned
            ? (NameSpace + "Pinned").Translate()
            : (NameSpace + "NotPinned").Translate())} · " +
            $"{(summarizer?.CheckSummarizing(focuse) ?? false
            ? (NameSpace + "Summarizing").Translate()
            : summarizer?.CheckSummarized(focuse) ?? false
            ? (NameSpace + "Summarized").Translate()
            : (NameSpace + "NotSummarized").Translate())}"
            );
        scrollListing.Gap();

        // 结束滚动区前，回写滚动区内容高度，下次 OnGUI 生效
        _scrollRectHeight = scrollListing.CurHeight;

        // 结束滚动区
        scrollListing.End();
        Widgets.EndScrollView();
        listing.GapLine();

        // 底部按钮：编辑/保存/取消
        Rect RightButton = new(inRect.width - ButtonWidth, listing.CurHeight, ButtonWidth, ButtonHeight);
        if (editing)
        {
            if (Widgets.ButtonText(RightButton, (NameSpace + "CancelEdit").Translate()))
                EndEdit();

            Rect LeftButton = new(RightButton.x - ButtonWidth - MemoryTabWindow.Gap, RightButton.y, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(LeftButton, (NameSpace + "SaveEdit").Translate()))
                SaveEdit();
        }
        else if (Widgets.ButtonText(RightButton, (NameSpace + "Edit").Translate()))
            BeginEdit(focuse);

        listing.End();
    }

    // 时间编辑行：普通 xx年xx象xx日xx时；Archive xx年xx象xx日-xx年xx象xx日
    private void DrawTimeEditor(Rect timeRect)
    {
        using var _ = new TextBlock(TextAnchor.MiddleCenter);

        float x = timeRect.x;
        float y = timeRect.y;

        bool isArchive = _editingMemory.Layer is MemoryLayer.Archive;

        if (isArchive)
        {
            // CLPA 起始点：年+象+日
            DrawTimeField(ref x, y, ref _startTimeYear, ref _startTimeYearBuf, TimeYearWidth, GenDate.DefaultStartingYear, MaxEditableYear, (NameSpace + "TimeYear").Translate());
            DrawTimeField(ref x, y, ref _startTimeQuadrum, ref _startTimeQuadrumBuf, TimeQuadrumWidth, 1, 4, (NameSpace + "TimeQuadrum").Translate());
            DrawTimeField(ref x, y, ref _startTimeDay, ref _startTimeDayBuf, TimeWidth, 1, 15, (NameSpace + "TimeDay").Translate());

            // 分隔符
            Widgets.Label(new(x, timeRect.y, TimeStashWidth, timeRect.height), "-");
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

        _editingMemory = memory;

        int startAbsTick = GenDate.TickGameToAbs(_editingMemory.StartGameTick);
        _startTimeYear = GenDate.Year(startAbsTick, 0f);
        _startTimeQuadrum = (int)GenDate.Quadrum(startAbsTick, 0f) + 1;
        _startTimeDay = GenDate.DayOfQuadrum(startAbsTick, 0f) + 1;
        _startTimeHour = GenDate.HourOfDay(startAbsTick, 0f);
        _startTimeSubHourOffset = startAbsTick % GenDate.TicksPerHour;

        int endAbsTick = GenDate.TickGameToAbs(_editingMemory.GameTick);
        _timeYear = GenDate.Year(endAbsTick, 0f);
        _timeQuadrum = (int)GenDate.Quadrum(endAbsTick, 0f) + 1;
        _timeDay = GenDate.DayOfQuadrum(endAbsTick, 0f) + 1;
        _timeHour = GenDate.HourOfDay(endAbsTick, 0f);
        _timeSubHourOffset = endAbsTick % GenDate.TicksPerHour;

        _content = _editingMemory.Content;
        _notes = _editingMemory.Note;
        _tags = string.Join(", ", _editingMemory.Tags ?? []);
        _importance = _editingMemory.Importance;
        _activity = _editingMemory.Activity;
    }

    // 保存：把编辑草稿应用回记忆本体
    private void SaveEdit()
    {
        _editingMemory.GameTick = GenDate.TickAbsToGame(
            (_timeYear - GenDate.DefaultStartingYear) * GenDate.TicksPerYear
            + (_timeQuadrum - 1) * GenDate.TicksPerQuadrum
            + (_timeDay - 1) * GenDate.TicksPerDay
            + _timeHour * GenDate.TicksPerHour
            + _timeSubHourOffset
            );
        if (_editingMemory.Layer is MemoryLayer.Archive)
            _editingMemory.StartGameTick = GenDate.TickAbsToGame(
                (_startTimeYear - GenDate.DefaultStartingYear) * GenDate.TicksPerYear
                + (_startTimeQuadrum - 1) * GenDate.TicksPerQuadrum
                + (_startTimeDay - 1) * GenDate.TicksPerDay
                + _startTimeHour * GenDate.TicksPerHour
                + _startTimeSubHourOffset
                );
        _editingMemory.Content = _content?.Trim();
        _editingMemory.Note = _notes?.Trim();
        _editingMemory.Tags = _tags
            .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToList();
        _editingMemory.Importance = _importance;
        _editingMemory.Activity = _activity;
        _editingMemory.IsUserEdited = true;

        EndEdit();
    }

    private void EndEdit()
    {
        _editingMemory = null;
        _timeYearBuf = _timeQuadrumBuf = _timeDayBuf = _timeHourBuf
            = _startTimeYearBuf = _startTimeQuadrumBuf = _startTimeDayBuf
            = _content = _tags = _notes
            = null;
    }
}
