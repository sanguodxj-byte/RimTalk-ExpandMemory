using RimTalk.Memory.Utils;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow;

/// <summary>
/// 选中记忆后的批量动作栏
/// </summary>
internal sealed class MemorySelectionBar : UIElement
{
    // 尺寸常量
    private const float Gap = MemoryTabWindow.Gap;
    private const float ButtonWidth = MemoryTabWindow.DefaultWidgetWidth;

    // 成员
    private readonly UIContext _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问

    // 构造函数
    public MemorySelectionBar(UIContext context)
    {
        _context = context;
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));
    }

    public override void Pulse()
    {
        if (Event.current.type is EventType.Layout) Update();
        if (_rect != Rect.zero) Draw();
    }

    protected override void Draw()
    {
        // 这个守卫条件理论上不可能触发，此处仅作为防御性编程
        if (_tabContext.Selection is not { } selection || selection.Count == 0) return;

        // 绘制背景，控制缩进，初始化 xy 游标
        Widgets.DrawMenuSection(_rect);
        Rect inRect = _rect.ContractedBy(6f);
        float height = inRect.height;
        float xRight = inRect.xMax;
        float y = inRect.y;

        // 绘制清空按钮
        if (Widgets.ButtonText(
            new Rect(xRight - ButtonWidth, y, ButtonWidth, height),
            "RimTalk.Memory.UI.TabWindow.ClearSelection".Translate()
            ))
        {
            selection.Clear();
            _tabContext.SetFocuse(null);
        }
        xRight -= ButtonWidth + Gap;

        // 绘制删除按钮
        using (new TextBlock(new Color(0.92f, 0.55f, 0.52f)))
            if (Widgets.ButtonText(
                new Rect(xRight - ButtonWidth, y, ButtonWidth, height),
                "RimTalk.Memory.UI.TabWindow.Delete".Translate()
                ))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimTalk.Memory.UI.TabWindow.ConfirmDelete".Translate(selection.Count.Named("COUNT")),
                    RemoveSelectedMemories
                    ));
        xRight -= ButtonWidth + Gap;

        // 绘制归档按钮，仅在选中含 ELS/CLPA 记忆时可用
        if (Widgets.ButtonText(
            new Rect(xRight - ButtonWidth, y, ButtonWidth, height),
            "RimTalk.Memory.UI.TabWindow.Archive".Translate(),
            active: selection.Any(memory => memory?.Layer is MemoryLayer.EventLog or MemoryLayer.Archive)
            ))
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk.Memory.UI.TabWindow.ConfirmArchive".Translate(),
                () => _tabContext.MemoryComp?.Summarizer?.Archive(selection)
                ));
        xRight -= ButtonWidth + Gap;

        // 绘制总结按钮，仅在选中含 ABM/SCM 记忆时可用
        if (Widgets.ButtonText(
            new Rect(xRight - ButtonWidth, y, ButtonWidth, height),
            "RimTalk.Memory.UI.TabWindow.Summarize".Translate(),
            active: selection.Any(memory => memory?.Layer is MemoryLayer.Active or MemoryLayer.Situational)
            ))
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_Archive_ConfirmSummarize".Translate(),
                () => _tabContext.MemoryComp?.Summarizer?.ManualSummarize(selection)
                ));
        xRight -= ButtonWidth + Gap;

        // 绘制选中计数
        float x = inRect.x;
        Widgets.Label(new Rect(x, y, xRight - x, height), "RimTalk.Memory.UI.TabWindow.Selected".Translate(_tabContext.Selection.Count));
    }

    private void RemoveSelectedMemories()
    {
        if (_tabContext.MemoryComp?.Interactor is not { } interactor) return;

        var selection = _tabContext.Selection;
        foreach (var memory in selection)
            interactor.RemoveMemory(memory);

        selection.Clear();
    }
}
