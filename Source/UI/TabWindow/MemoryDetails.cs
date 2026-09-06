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
    // 常量配置
    private const float ListingStandardGap = 14f;
    private const float ButtonWidth = MemoryTabWindow.DefaultWidgetWidth;
    private const float ButtonHeight = MemoryTabWindow.DefaultWidgetHeight;
    private const float SliderLabelPct = 0.35f;     // 滑条行左侧标签宽度占比

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
        _editingMemory = null;
    }

    public override void PostClose()
    {
        _editingMemory = null;
        _content = null;
        _tags = null;
        _notes = null;
    }

    // 绘制详情/编辑区，有编辑态和只读态两种模式，且可**无缝切换**
    protected override void Draw()
    {
        // 绘制背景并取得工作区
        Widgets.DrawMenuSection(_rect);
        Rect inRect = _rect.ContractedBy(12f);

        // 没有焦点时仅展示引导文本
        if (_tabContext.Focuse is not { } focuse)
        {
            Widgets.Label(inRect, "RimTalk.Memory.UI.TabWindow.Guide".Translate());
            return;
        }

        // 启动 Listing_Standard
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // 标题
        using (new TextBlock(GameFont.Medium))
            listing.Label("RimTalk.Memory.UI.TabWindow.Details".Translate());
        listing.Gap();

        // 记忆类型和层级
        listing.Label($"{focuse.Layer.Translate()} · {focuse.Type.Translate()}");
        listing.Gap();

        // 记忆时间
        using (new TextBlock(TimeColor))
            listing.Label(focuse.AgeString);
        listing.GapLine();

        // 编辑态标记：当前是否正在编辑记忆
        bool editing = _editingMemory is not null;

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
                "RimTalk.Memory.UI.TabWindow.Importance".Translate(_importance.ToString("F2").Named("IMPORTANCE")),
                _importance, 0f, 1f, SliderLabelPct
                );
        else
            scrollListing.Label("RimTalk.Memory.UI.TabWindow.Importance".Translate(focuse.Importance.ToString("F2").Named("IMPORTANCE")));
        scrollListing.Gap();

        // 活跃度
        if (editing)
            _activity = scrollListing.SliderLabeled(
                "RimTalk.Memory.UI.TabWindow.Activity".Translate(_activity.ToString("F2").Named("ACTIVITY")),
                _activity, 0f, 1f, SliderLabelPct
                );
        else
            scrollListing.Label("RimTalk.Memory.UI.TabWindow.Activity".Translate(focuse.Activity.ToString("F2").Named("ACTIVITY")));
        scrollListing.Gap();

        // 标签
        if (editing)
            _tags = scrollListing.TextEntry(_tags);
        else
            scrollListing.Label("RimTalk.Memory.UI.TabWindow.Tags".Translate() + string.Join(", ", focuse.Tags ?? []));
        scrollListing.Gap();

        // 备注
        if (editing)
            scrollListing.TextEntry(_notes, 4);
        else
            using (new TextBlock(NoteColor))
                scrollListing.Label("RimTalk.Memory.UI.TabWindow.Notes".Translate() + focuse.Note);
        scrollListing.Gap();

        // 记忆状态：是否已固定、是否正在总结/已总结/未总结
        var summarizer = _tabContext.MemoryComp?.Summarizer;
        scrollListing.Label(
            $"{(focuse.IsPinned
            ? "RimTalk.Memory.UI.TabWindow.Pinned"
            : "RimTalk.Memory.UI.TabWindow.NotPinned")
            .Translate()} · " +
            $"{(summarizer?.CheckSummarizing(focuse) ?? false
            ? "RimTalk.Memory.UI.TabWindow.Summarizing"
            : summarizer?.CheckSummarized(focuse) ?? false
            ? "RimTalk.Memory.UI.TabWindow.Summarized"
            : "RimTalk.Memory.UI.TabWindow.NotSummarized")
            .Translate()}"
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
            if (Widgets.ButtonText(RightButton, "RimTalk.Memory.UI.TabWindow.CancelEdit".Translate()))
                _editingMemory = null;

            Rect LeftButton = new(RightButton.x - ButtonWidth - MemoryTabWindow.Gap, RightButton.y, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(LeftButton, "RimTalk.Memory.UI.TabWindow.SaveEdit".Translate()))
                SaveEdit();
        }
        else if (Widgets.ButtonText(RightButton, "RimTalk.Memory.UI.TabWindow.Edit".Translate()))
            BeginEdit(focuse);

        listing.End();
    }

    // 保存：把编辑草稿应用回记忆本体
    private void SaveEdit()
    {
        _editingMemory.Content = _content?.Trim();
        _editingMemory.Note = _notes?.Trim();
        _editingMemory.Tags = _tags
            .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToList();
        _editingMemory.Importance = _importance;
        _editingMemory.Activity = _activity;
        _editingMemory.IsUserEdited = true;

        _editingMemory = null;
    }

    // 编辑器操作独立草稿，保存前不修改业务对象。
    private void BeginEdit(MemoryEntry memory)
    {
        Find.TickManager?.Pause();
        _editingMemory = memory;
        _content = _editingMemory.Content;
        _notes = _editingMemory.Note;
        _tags = string.Join(", ", _editingMemory.Tags ?? []);
        _importance = _editingMemory.Importance;
        _activity = _editingMemory.Activity;
    }
}
