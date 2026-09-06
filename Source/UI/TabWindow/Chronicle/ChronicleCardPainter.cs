using RimTalk.Memory.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Chronicle;

public class ChronicleCardPainter : UIElement
{
    // 常量配置
    private const float ClusterWidgetWidth = 8f;
    private const int DepthGap = 10;
    private const float ClusterWidgetPulseFrequency = 1f;
    private const float DragThreshold = 5f;

    // 成员
    private readonly UIContext _context;
    private readonly MemoryChronicle.Context _chronicleContext; // 快捷访问
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private readonly Dictionary<MemoryEntry, ChronicleCard> _cardMap = new();

    // 理论上，_visableCards 每次 pulse 都只需要在 Update 中被构建并同时遍历一次
    // 但此处为了解耦和语义干净，额外遍历了若干次
    private readonly List<ChronicleCard> _visableCards = new();

    // 卡片工厂缓存，避免布局热路径上每张卡分配闭包
    private readonly Func<MemoryEntry, ChronicleCard> _cardFactory;

    // 把聚类列表设置为成员变量，避免每帧都创建新的 List 对象，减少 GC 压力
    private readonly List<ChronicleCard> _clusteringCards = new();

    // 框选
    private Vector2? _dragAnchor;
    private Rect? _dragRect;
    private readonly HashSet<MemoryEntry> _selectionSnapshot = new();

    // 构造函数
    public ChronicleCardPainter(UIContext context)
    {
        _context = context;
        _chronicleContext = _context.GetContext<MemoryChronicle.Context>()
            ?? throw new ArgumentException(nameof(context));
        _tabContext = _context.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));

        _tabContext.MemoryCompReseted += MemoryCompReseted;

        _cardFactory = memory => new ChronicleCard(_context, memory);
    }

    private void MemoryCompReseted() => _cardMap.Clear();

    public override void Pulse()
    {
        base.Pulse();

        // 逻辑 pass 中按层级从浅到深遍历，确保最浅的卡片控件先响应
        // 绘制 pass 中则从深到浅遍历，确保最浅的卡片盖在上面
        if (Event.current.type is not EventType.Repaint)
            foreach (var card in _visableCards.OrderBy(c => c.Depth)) card.Pulse();
        else
            foreach (var card in _visableCards.OrderByDescending(c => c.Depth)) card.Pulse();

        // 特别的，一些框框之类的控件需要在所有卡片绘制完后再绘制，避免被卡片遮挡
        AfterDraw();
    }

    public override void PostClose()
    {
        _cardMap.Clear();
        _visableCards.Clear();
    }

    protected override void UpdateRects()
    {
        float width = _rect.width;
        float height = _rect.height;
        float x = _rect.x;
        float y = _rect.y;

        int chronicleStartTick = _chronicleContext.ChronicleStartTick;
        int chronicleEndTick = _chronicleContext.ChronicleEndTick;
        int tickRange = chronicleEndTick - chronicleStartTick;

        foreach (var card in _visableCards)
        {
            var memory = card.Memory;

            float cardLeftX = (Math.Max(memory.GameTick, chronicleStartTick) - chronicleStartTick) / (float)tickRange * width + x;
            float cardRightX = (Math.Min(memory.EndGameTick, chronicleEndTick) - chronicleStartTick) / (float)tickRange * width + x;

            Rect cardRect = new(cardLeftX, y, cardRightX - cardLeftX, height);

            card.UpdateRect(cardRect);
        }
    }

    protected override void Update()
    {
        // 因为数据源记忆列表具有每帧可变性，故每帧都重新拉取可见篇章列表
        UpdateCardList();
    }

    // 每帧从 MemoryComp 的 CLPA 档案重建篇章列表，只应用标签过滤。
    private void UpdateCardList()
    {
        _visableCards.Clear();

        if (_tabContext.MemoryComp?.ArchiveMemories is not { } archiveMemories) return;

        // 因为是热点路径，故放弃了 LINQ，直接使用 foreach 遍历
        foreach (var memory in archiveMemories)
        {
            if (memory is null
                // 在窗口内
                || memory.GameTick >= _chronicleContext.ChronicleEndTick
                || memory.EndGameTick <= _chronicleContext.ChronicleStartTick)
                continue;

            if (_cardMap.GetOrAdd(memory, _cardFactory) is not { } card
                || card.Memory is null)
                continue;
            _visableCards.Add(card);
        }
        // 按时间顺序排序，便于后续处理重叠篇章
        _visableCards.SortBy(card => card.Memory.GameTick);

        // 处理重叠篇章的深度
        ChronicleCard formerCard = null;
        foreach (var card in _visableCards)
        {
            if (card.Memory.GameTick < formerCard?.Memory.EndGameTick)
            {
                card.Depth = formerCard.Depth - DepthGap;

            }
            formerCard = card;
        }

        UpdateRects();
    }

    protected override void Draw()
    {
        // 篇章栏与时间轴的底色。
        Widgets.DrawBoxSolid(_rect, new Color(0.07f, 0.08f, 0.09f, 0.82f));

        var current = Event.current;

        // 框选逻辑
        do
        {
            bool dragSelecting = _dragRect is not null;

            // 纯逻辑部分只处理左键事件
            if (current.button != 0) break;

            // 鼠标抬起时重置起点
            if (current.rawType is EventType.MouseUp)
            {
                _dragAnchor = null;

                // 若当前正在框选，则结束框选
                if (dragSelecting)
                {
                    _dragRect = null;
                    _selectionSnapshot.Clear();
                }
                break;
            }

            var type = current.type;
            Vector2 mouse = current.mousePosition;

            // 左键在区域内按下时，记录框选起点，但不一定启动框选
            if (type is EventType.MouseDown && _rect.Contains(mouse))
            {
                _dragAnchor = mouse;
                break;
            }

            // _dragAnchor 未激活则不执行后续拖拽逻辑
            if (_dragAnchor is not Vector2 dragAnchor
                || type is not EventType.MouseDrag
                || _tabContext.Selection is not { } selection)
                break;

            // 框选对角长度超过阈值时，才**单向**启动框选
            if ((mouse - dragAnchor).magnitude >= DragThreshold)
            {
                // 如果当前是启动框选的瞬间，且按住 ctrl，则记录当前 selection 的快照
                if (!dragSelecting && current.control)
                    _selectionSnapshot.UnionWith(selection);

                dragSelecting = true;
            }

            // 执行框选
            if (!dragSelecting) break;

            float anchorX = dragAnchor.x;
            float anchorY = dragAnchor.y;
            float mouseX = mouse.x;
            float mouseY = mouse.y;
            _dragRect = Rect.MinMaxRect(
                MathF.Min(anchorX, mouseX),
                MathF.Min(anchorY, mouseY),
                MathF.Max(anchorX, mouseX),
                MathF.Max(anchorY, mouseY)
            );

            selection.Clear();
            foreach (var card in _visableCards)
                if (((Rect)_dragRect).Overlaps(card.Rect)) selection.Add(card.Memory);
            selection.UnionWith(_selectionSnapshot);

            current.Use();
        } while (false);

        // 滚轮滚动
        if (current.type is EventType.ScrollWheel && _rect.Contains(current.mousePosition))
        {
            // 基于 tick 跨度来计算 tick 移动，从而实现所有跨度下视觉移动速度相同
            int span = _chronicleContext.ChronicleEndTick - _chronicleContext.ChronicleStartTick;
            int step = (int)(current.delta.y * span * MemoryTabWindow.ScrollWheelSpeed);

            // 基于滚动方向，先锚定 end/start tick，再调整另一端，保持 span 不变
            // step 几乎不可能为 0，为 0 也不会造成什么危害
            if (step > 0)
            {
                _chronicleContext.ChronicleEndTick += step;
                if (_chronicleContext.ChronicleEndTick == _tabContext.LifeCurrentTick)
                    _chronicleContext.ChronicleStartTick = _chronicleContext.ChronicleEndTick - span;
                else _chronicleContext.ChronicleStartTick += step;
            }
            else
            {
                _chronicleContext.ChronicleStartTick += step;
                if (_chronicleContext.ChronicleStartTick == _tabContext.LifeStartTick)
                    _chronicleContext.ChronicleEndTick = _chronicleContext.ChronicleStartTick + span;
                else _chronicleContext.ChronicleEndTick += step;
            }

            current.Use();
        }
    }

    private void AfterDraw()
    {
        // 绘制重叠处理控件
        if (_visableCards.Count > 0)
        {
            _clusteringCards.Clear();
            _clusteringCards.Add(_visableCards[0]);

            for (int i = 1; i < _visableCards.Count; i++)
            {
                var card = _visableCards[i];

                if (card.Rect.xMin < _clusteringCards[^1]?.Rect.xMax)
                {
                    // 检查到重叠，入列
                    _clusteringCards.Add(card);
                    continue;
                }

                if (_clusteringCards.Count == 1)
                {
                    _clusteringCards[0] = card;
                    continue;
                }

                // 当前未重叠，但列表 count 大于 1，说明一段聚类结束了
                // 执行控件绘制
                DrawClusterWidget(_clusteringCards);
                _clusteringCards.Clear();
                _clusteringCards.Add(card);
            }

            // 循环结束后再执行一次检查
            if (_clusteringCards.Count > 1) DrawClusterWidget(_clusteringCards);
        }

        // 绘制选框
        if (_dragRect is not null)
            Widgets.DrawBox((Rect)_dragRect);
    }
    // 绘制重叠控件
    private static void DrawClusterWidget(List<ChronicleCard> clusteringCards)
    {
        var lastRect = clusteringCards[^1].Rect;
        const float radius = ClusterWidgetWidth * 0.5f;
        Vector2 center = new(lastRect.xMax + radius, lastRect.yMin - radius);

        if (WidgetsUtil.ButtonCircle(center, radius, new Color32(255, 59, 48, 255)))
        {
            // 将最深的篇章提升到最浅篇章的上方
            (var shallowestCard, var deepestCard) = GetCardsWithExtremeDepth(clusteringCards);
            deepestCard.SetDepth(shallowestCard.Depth - DepthGap);
        }
        // 悬停重叠控件时，在层级最深的卡片周围、绘制正弦规律明暗波动的指示框
        else if (WidgetsUtil.CircleContains(center, radius, Event.current.mousePosition))
        {
            using (new TextBlock(new Color(1f, 1f, 0.92f, MathUtil.SinePulse(ClusterWidgetPulseFrequency, 0.1f, 0.8f))))
                Widgets.DrawBox(clusteringCards.MaxBy(c => c.Depth).Rect.ExpandedBy(2f), 2);
        }
    }
    private static (ChronicleCard Shallowest, ChronicleCard Deepest) GetCardsWithExtremeDepth(List<ChronicleCard> cards)
    {
        if (cards is null || cards.Count == 0) return (null, null);

        ChronicleCard shallowest, deepest;
        shallowest = deepest = cards[0];

        for (int i = 1; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card.Depth < shallowest.Depth)
                shallowest = card;
            else if (card.Depth > deepest.Depth)
                deepest = card;
        }

        return (shallowest, deepest);
    }
}
