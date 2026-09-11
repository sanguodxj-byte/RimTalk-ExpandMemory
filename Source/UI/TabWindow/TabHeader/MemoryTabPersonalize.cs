using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.TabHeader;

/// <summary>
/// “个性化”弹窗，
/// 用于开关记忆组件的子功能
/// </summary>
public class MemoryTabPersonalize : SideWindow
{
    // 尺寸常量
    private const float WidgetHeight = MemoryTabWindow.DefaultWidgetHeight;

    // 记忆组件
    private readonly FourLayerMemoryComp _memoryComp;

    public MemoryTabPersonalize(FourLayerMemoryComp memoryComp, Rect sidebarRect) : base(sidebarRect) => _memoryComp = memoryComp;

    public override void DoWindowContents(Rect inRect)
    {
        if (_memoryComp is null) return;

        float width = inRect.width;
        float x = inRect.x;
        float y = inRect.y;

        using var _ = new TextBlock(GameFont.Tiny);

        bool hasJobCapturer = _memoryComp.JobCapturer is not null;

        Widgets.CheckboxLabeled(new Rect(x, y, width, WidgetHeight), "工作记忆", ref hasJobCapturer);
        y += WidgetHeight;

        if (hasJobCapturer) _memoryComp.AddJobCapturer();
        else _memoryComp.RemoveJobCapturer();

        bool hasCombatCapturer = _memoryComp.CombatCapturer is not null;

        Widgets.CheckboxLabeled(new Rect(x, y, width, WidgetHeight), "战斗记忆", ref hasCombatCapturer);
        y += WidgetHeight;

        if (hasCombatCapturer) _memoryComp.AddCombatCapturer();
        else _memoryComp.RemoveCombatCapturer();

        bool hasSummarizer = _memoryComp.Summarizer is not null;

        Widgets.CheckboxLabeled(new Rect(x, y, width, WidgetHeight), "自动总结", ref hasSummarizer);

        if (hasSummarizer) _memoryComp.AddSummarizer();
        else _memoryComp.RemoveSummarizer();
    }
}
