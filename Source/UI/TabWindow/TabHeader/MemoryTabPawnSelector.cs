using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.TabHeader;

/// <summary>
/// 轻量级 Pawn 搜索选择窗口。
/// 自行从当前所有地图中检索候选 Pawn，并在选中时直接同步 RimWorld 当前选择，调用方无需提供 Pawn 列表或回调。
/// 窗口位置/尺寸由调用方一次性算好传入（紧贴触发按钮右侧、收缩在主标签内容区内），呈现“侧边栏”观感。
/// </summary>
public sealed class MemoryTabPawnSelector : Window
{
    // 尺寸常量
    private const float Gap = MemoryTabWindow.Gap;
    private const float QueryHeight = 32f;
    private const float ScrollbarWidth = MemoryTabWindow.ScrollbarWidth;
    private const float BottonHeight = 34f;
    private const float BottonGap = 4f;

    // 外部算好传入的矩形
    private readonly Rect _sidebarRect;

    // 抓取到的 Pawn 列表，按殖民者优先、名字排序
    private readonly List<Pawn> _pawns = new();

    // query
    private string _query = string.Empty;

    // 滚动位置
    private Vector2 _scroll;

    public MemoryTabPawnSelector(Rect sidebarRect)
    {
        _sidebarRect = sidebarRect;

        // 获取当前地图上所有 pawn
        _pawns = Find.CurrentMap?.mapPawns?.AllPawns?
            .Where(pawn => pawn?.TryGetComp<FourLayerMemoryComp>() is not null)
            .OrderByDescending(pawn => pawn.IsColonist)
            .ThenBy(pawn => pawn.Faction?.Name)
            .ThenBy(pawn => pawn.playerSettings?.displayOrder ?? 999999)
            .ThenBy(pawn => pawn.LabelShort)
            .ToList()
            ?? new();

        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
    }

    // 直接采用调用方算好的矩形，跳过默认的居中布局。
    protected override void SetInitialSizeAndPosition() => windowRect = _sidebarRect.Rounded();

    public override void DoWindowContents(Rect inRect)
    {
        float width = inRect.width;
        const float x = 0f;
        float y = 0f;

        // 搜索框：按 Pawn 名字做不区分大小写的子串匹配，空查询时展示全部。
        Rect queryRect = new(x, y, width, QueryHeight);
        _query = Widgets.TextField(queryRect, _query);
        y += QueryHeight + Gap;

        // 以 query 过滤 Pawn 列表
        var filtered = _pawns;
        if (!string.IsNullOrWhiteSpace(_query))
        {
            filtered = filtered
                .Where(pawn => pawn.LabelShort?.Contains(_query, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        // 列表滚动视图：底部留出滚动条宽度，纵向高度随侧边栏自适应。
        Rect outRect = new(x, y, width, inRect.height - y);

        float viewHeight = filtered.Count * (BottonHeight + BottonGap);
        Rect viewRect = new(
            0f, 0f,
            viewHeight > outRect.height ? outRect.width - ScrollbarWidth : outRect.width,
            viewHeight
            );

        Widgets.BeginScrollView(outRect, ref _scroll, viewRect);
        float viewWidth = viewRect.width;
        float viewY = 0f;
        foreach (Pawn pawn in filtered)
        {
            if (Widgets.ButtonText(
                new Rect(0f, viewY, viewWidth, BottonHeight),
                // 非殖民者追加所属派系后缀，便于在搜索结果中区分阵营。
                pawn.LabelShort + (pawn.IsColonist ? string.Empty : $" · {pawn.Faction?.Name ?? "RimTalk.Memory.UI.TabWindow.NoFaction".Translate()}")
                ))
            {
                // 选中后直接同步 RimWorld 全局选择并关闭窗口。
                Find.Selector.ClearSelection();
                Find.Selector.Select(pawn);
                Close();
            }
            viewY += BottonHeight + BottonGap;
        }
        Widgets.EndScrollView();
    }
}
