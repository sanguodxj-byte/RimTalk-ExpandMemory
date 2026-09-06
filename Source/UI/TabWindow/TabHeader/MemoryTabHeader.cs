using RimTalk.Memory.Debug;
using RimTalk.Memory.Maintenance;
using RimTalk.Memory.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.TabHeader;

internal sealed class MemoryTabHeader : UIElement
{
    // 尺寸常量
    private const float Gap = MemoryTabWindow.Gap;
    private const float PawnWidgetWidth = 250f;
    private const float DefaultWidgetWidth = MemoryTabWindow.DefaultWidgetWidth;
    private const float PawnSelectorWidth = 300f;

    // 成员
    private readonly UIContext _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问

    // 预计算的枚举值列表，供下拉菜单使用
    private static readonly List<MemoryLayer?> _allLayers = EnumUtil.AllEnumValues<MemoryLayer>()?
        .Select(layer => (MemoryLayer?)layer).Prepend(null).ToList() ?? new();
    private static readonly List<MemoryType?> _allTypes = EnumUtil.AllEnumValues<MemoryType>()?
        .Select(type => (MemoryType?)type).Prepend(null).ToList() ?? new();

    public MemoryTabHeader(UIContext context)
    {
        _context = context;
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));
    }

    protected override void Draw()
    {
        // 绘制背景，控制缩进，初始化 xy 游标
        Widgets.DrawMenuSection(_rect);
        Rect inRect = _rect.ContractedBy(6f);
        float height = inRect.height;
        float x = inRect.x;
        float y = inRect.y;

        // 绘制角色选择控件
        Rect pawnRect = new(x, y, PawnWidgetWidth, height);
        if (Widgets.ButtonText(pawnRect, _tabContext.MemoryComp?.parent?.LabelShort ?? "RimTalk.Memory.UI.TabWindow.Need_SelectPawn".Translate()))
        {
            Rect currentWindowRect = Find.WindowStack.currentlyDrawnWindow.windowRect;
            // 是的，我知道这里 margin 是硬编码直接取默认值，但我懒得折腾了，就这样吧
            float margin = Window.StandardMargin;

            // 计算选择器的**绝对**矩形
            float selectorY = pawnRect.y + currentWindowRect.y + margin;
            Rect pawnSelectorRect = new(
                pawnRect.xMax + currentWindowRect.x + margin,
                selectorY,
                PawnSelectorWidth,
                Math.Max(currentWindowRect.yMax - selectorY, 0f) // 算下边缘的时候不需要 margin
                );
            Find.WindowStack.Add(new MemoryTabPawnSelector(pawnSelectorRect));
        }
        x += PawnWidgetWidth + Gap;

        // 绘制层级过滤控件
        Rect layerRect = new(x, y, DefaultWidgetWidth, height);
        if (Widgets.ButtonText(layerRect, _tabContext.LayerFilter.Translate()))
            OpenLayerFilterMenu();
        x += DefaultWidgetWidth + Gap;

        TooltipHandler.TipRegion(layerRect, "RimTalk.Memory.UI.TabWindow.LayerFilter".Translate());

        // 绘制类型过滤控件
        Rect typeRect = new(x, y, DefaultWidgetWidth, height);
        if (Widgets.ButtonText(typeRect, _tabContext.TypeFilter.Translate()))
            OpenTypeFilterMenu();
        x += DefaultWidgetWidth + Gap;

        TooltipHandler.TipRegion(typeRect, "RimTalk.Memory.UI.TabWindow.TypeFilter".Translate());

        // 绘制工具按钮
        float xRight = inRect.xMax;
        Rect toolsRect = new(xRight - DefaultWidgetWidth, y, DefaultWidgetWidth, height);
        if (Widgets.ButtonText(toolsRect, "RimTalk.Memory.UI.TabWindow.Tools".Translate()))
            OpenToolsMenu();
        xRight -= DefaultWidgetWidth + Gap;

        // 绘制记忆统计信息
        if (_tabContext.MemoryComp is { } memoryComp)
        {
            Rect statsRect = new(x, y, Math.Max(0f, xRight - x), height);
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft))
                Widgets.Label(
                    statsRect,
                    $"ABM {memoryComp.ActiveMemories?.Count ?? 0} · SCM {memoryComp.SituationalMemories?.Count ?? 0} · " +
                    $"ELS {memoryComp.EventLogMemories?.Count ?? 0} · CLPA {memoryComp.ArchiveMemories?.Count ?? 0}"
                    );
        }
    }

    private void OpenLayerFilterMenu() =>
        Find.WindowStack.Add(new FloatMenu(_allLayers
            .Select(layer => new FloatMenuOption(layer.Translate(), () => _tabContext.LayerFilter = layer))
            .ToList()));

    private void OpenTypeFilterMenu() =>
        Find.WindowStack.Add(new FloatMenu(_allTypes
            .Select(type => new FloatMenuOption(type.Translate(), () => _tabContext.TypeFilter = type))
            .ToList()));

    private void OpenToolsMenu()
    {
        var windowStack = Find.WindowStack;
        var memoryComp = _tabContext.MemoryComp;
        windowStack.Add(new FloatMenu([
            new("RimTalk.Memory.UI.TabWindow.Knowledge".Translate(), () => windowStack.Add(new Dialog_CommonKnowledge())),
            new("RimTalk.Memory.UI.TabWindow.CreateMemory".Translate(), () => windowStack.Add(new MemoryCreateDialog(memoryComp))),
            new("RimTalk.Memory.UI.TabWindow.Preview".Translate(), () => windowStack.Add(new Dialog_InjectionPreview())),
            new("RimTalk.Memory.UI.TabWindow.SummaryPrompt".Translate(), () => windowStack.Add(new Dialog_PromptEditor())),
            new("RimTalk.Memory.UI.TabWindow.Export".Translate(), () => CustomScribe.Export(memoryComp)),
            new("RimTalk.Memory.UI.TabWindow.Import".Translate(), OpenImportMenu),
            new("RimTalk.Memory.UI.TabWindow.SummarizeAll".Translate(), MemorySummarizer.SummarizeAll),
            new("RimTalk.Memory.UI.TabWindow.OperationGuide".Translate(), () => windowStack.Add(new Dialog_MessageBox("RimTalk.Memory.UI.TabWindow.Guide".Translate())))
        ]));
    }

    private void OpenImportMenu()
    {
        var targetComp = _tabContext.MemoryComp;
        var windowStack = Find.WindowStack;
        windowStack.Add(new FloatMenu((CustomScribe.GetImportFiles() ?? [])
                .Select(path => new FloatMenuOption(Path.GetFileName(path), () => windowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimTalk.Memory.UI.TabWindow.ConfirmImport".Translate(),
                    () => CustomScribe.Import(path, targetComp)
                    ))))
                .Prepend(new FloatMenuOption("RimTalk.Memory.UI.TabWindow.OpenImportFolder".Translate(), CustomScribe.OpenExportFolder))
                .ToList()));
    }
}
