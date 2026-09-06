using RimTalk.Memory.UI.TabWindow.Chronicle;
using RimTalk.Memory.UI.TabWindow.TabHeader;
using RimTalk.Memory.UI.TabWindow.Timeline;
using RimTalk.Memory.Utils;
using RimTalk.MemoryPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow;

/// <summary>
/// “角色人生档案工作台”主标签窗口，负责生命周期、布局和区域派发
/// </summary>
public class MemoryTabWindow : UIElement
{
    // 尺寸常量
    public const float Gap = 8f;
    public const float DefaultWidgetWidth = 114f;
    public const float DefaultWidgetHeight = 32f;
    public const float ScrollbarWidth = 16f;

    // 视觉尺寸固定的窗口中的滚轮系数，3 是每格滚动产生的 delta.y
    public const float ScrollWheelSpeed = 0.06f / 3f; // 滚轮每格滚动距离占窗口总尺寸的百分比
    public const float ScrollZoomFactor = 1.0322801154563671592135852250097f; // 即 1.1^(1/3)，滚轮每格滚动的缩放倍数

    private const float HeaderHeight = 46f;
    private const float ChronicleHeight = 166f;
    private const float SplitterWidth = 6f;
    private const float SelectionBarHeight = 44f;
    private const float MinDetailsWidth = 300f;
    private const float MinTimelineWidth = 520f;

    /// <summary>
    /// MemoryTabWindow 的局部共享上下文，供各个子区域使用
    /// </summary>
    public class Context : UIContext
    {
        // 公开数据，供外部消费
        /// 核心记忆组件
        public FourLayerMemoryComp MemoryComp;

        // 全量记忆总时间跨度缓存，每帧刷新
        public int LifeStartTick = -1;
        public int LifeCurrentTick = MathUtil.SafeMaxInt;

        // 核心锚点指针，每帧收束
        private int _cursorTick = int.MaxValue;
        public int CursorTick
        {
            get => _cursorTick;
            set => _cursorTick = Math.Clamp(value, LifeStartTick, LifeCurrentTick);
        }

        // 主 UI 窗口 rect
        public Rect InRect;

        // 过滤器
        public MemoryLayer? LayerFilter;
        public MemoryType? TypeFilter;

        // 关注点，会展示在记忆详情区
        private MemoryEntry _focuse;
        public MemoryEntry Focuse => _focuse;

        // 选中记忆集合
        public readonly HashSet<MemoryEntry> Selection = new();

        // 事件
        public event Action MemoryCompReseted;
        public event Action RePositionTimeline;
        public event Action RePositionCursor;
        public event Action FocuseChanged;

        /// <summary>
        /// 过滤和按新到旧排序后的时间轴记忆缓存，因为后续在篇章栏中也会用到，所以放在这里
        /// </summary>
        public readonly List<MemoryEntry> TreatedTimelineMemories = new();

        // 全量记忆缓存，每帧刷新；供内部校验记忆条目有效性
        private readonly HashSet<MemoryEntry> _allMemories = new();

        // 构造函数
        public Context(UIContext parentContext) : base(parentContext) { }

        /// <summary>
        /// 设置关注点，如果关注点发生变化，则广播事件
        /// </summary>
        public void SetFocuse(MemoryEntry memory)
        {
            if (memory != Focuse) FocuseChanged?.Invoke();
            _focuse = memory;
        }

        public override void PostClose()
        {
            SetFocuse(null);
            Selection.Clear();
            TreatedTimelineMemories.Clear();
            _allMemories.Clear();
        }

        // 每帧刷新
        protected override void Update()
        {
            CheckCompChange();
            UpdateAll();
        }

        private void CheckCompChange()
        {
            // 当前 context 目标出现有效变化时，更新 MemoryComp 并广播事件
            if (Find.Selector.SingleSelectedThing is Pawn pawn
                && pawn != MemoryComp?.parent
                && pawn.TryGetComp<FourLayerMemoryComp>() is { } memoryComp)
            {
                MemoryComp = memoryComp;

                // 订阅者或将需要最新数据来执行更新，故需要先全量 update 一次
                UpdateAll();
                MemoryCompReseted?.Invoke();
            }
        }

        private void UpdateAll()
        {
            // 没有有效的记忆组件时，没有多余操作的必要
            if (MemoryComp is null) return;

            var aBMs = MemoryComp.ActiveMemories;
            var sCMs = MemoryComp.SituationalMemories;
            var eLSs = MemoryComp.EventLogMemories;

            // 刷新全量记忆缓存
            _allMemories.Clear();
            _allMemories.UnionWith(aBMs);
            _allMemories.UnionWith(sCMs);
            _allMemories.UnionWith(eLSs);
            _allMemories.UnionWith(MemoryComp.ArchiveMemories);

            // 刷新全量记忆时间跨度
            if (_allMemories.Count > 0)
            {
                LifeStartTick = _allMemories.Min(memory => memory.GameTick);
                LifeCurrentTick = _allMemories.Max(memory => memory.EndGameTick);
            }

            // 每帧收束 cursor
            CursorTick = CursorTick;

            // 刷新关注点和选中集合
            if (!_allMemories.Contains(Focuse))
                SetFocuse(null);
            Selection.RemoveWhere(memory => !_allMemories.Contains(memory));

            // 刷新时间轴记忆列表
            TreatedTimelineMemories.Clear();
            bool Filter(MemoryEntry memory) =>
                memory is not null
                && (LayerFilter is null || memory.Layer == LayerFilter.Value)
                && (TypeFilter is null || memory.Type == TypeFilter.Value);

            TreatedTimelineMemories.AddRange(aBMs.Where(Filter));
            TreatedTimelineMemories.AddRange(sCMs.Where(Filter));
            TreatedTimelineMemories.AddRange(eLSs.Where(Filter));
            // 按 gametick 降序排列，便于时间轴从上到下展示
            TreatedTimelineMemories.SortBy(memory => -memory.GameTick);
        }

        public void RaiseRePositionTimeline() => RePositionTimeline?.Invoke();
        public void RaiseRePositionCursor() => RePositionCursor?.Invoke();
    }

    // 实例成员
    private readonly Context _context;
    private bool _showSelectionBar = false; // 是否显示动作栏
    private float _timelineResizeAnchor = -1; // 时间轴-记忆详情区宽度调整锚点
    private Rect _timelineRect; // 时间轴区域矩形，用于背景绘制与滚动视口
    private Rect _splitterRect;

    // 下属
    private readonly MemoryTabHeader _header; // 窗口头
    private readonly MemoryChronicle _chronicle; // 篇章区
    private readonly MemoryTimeline _timeline; // 时间轴
    private readonly MemoryDetails _details; // 记忆详情
    private readonly MemorySelectionBar _selectionBar; // 动作栏（选中记忆后展示）

    // 构造函数，会同时以 context 注册各个子实例
    public MemoryTabWindow(UIContext context)
    {
        _context = new Context(context);

        _header = new MemoryTabHeader(_context);
        _chronicle = new MemoryChronicle(_context);
        _timeline = new MemoryTimeline(_context);
        _details = new MemoryDetails(_context);
        _selectionBar = new MemorySelectionBar(_context);
    }

    public override void Pulse()
    {
        _context.Pulse();

        base.Pulse();

        _header.Pulse();
        _chronicle.Pulse();
        _timeline.ScrollPulse();
        _details.Pulse();
        _selectionBar.Pulse();
    }

    public override void PostClose()
    {
        _context.PostClose();

        _header.PostClose();
        _chronicle.PostClose();
        _timeline.PostClose();
        _details.PostClose();
        _selectionBar.PostClose();
    }

    protected override void Update()
    {
        // 检测是否需要显示动作栏，若状态发生变化则重新布局
        bool showSelectionBar = _context.Selection?.Count > 0;
        if (_showSelectionBar != showSelectionBar)
        {
            _showSelectionBar = showSelectionBar;
            // 这条更新信号传递的帧内时点相当早，仅晚于 _context 的 pulse
            // 此时下游子控件都尚未 pulse，故具有一定危险性，需要注意
            UpdateRects();
        }
    }

    // 这里采取了直接布局而非游标布局
    protected override void UpdateRects()
    {
        float totalWidth = _rect.width;
        float totalHeight = _rect.height;

        // 派发 header 绘制
        Rect headerRect = new(0f, 0f, totalWidth, HeaderHeight);

        // 准备派发内容区绘制
        float contentTop = headerRect.yMax + Gap;
        float contentHeight = totalHeight - contentTop;

        if (_showSelectionBar) contentHeight -= SelectionBarHeight;

        // 派发篇章区和时间轴区绘制
        float timelineWidth = RimTalkMemoryPatchMod.Settings.MemoryTabTimeLineWidth;

        Rect chronicleRect = new(0f, contentTop, timelineWidth, ChronicleHeight);

        Rect timelineRect = new(0f, chronicleRect.yMax + Gap, timelineWidth, contentHeight - ChronicleHeight - Gap);

        // 派发时间轴-记忆详情区分割条绘制和拖拽调整
        Rect splitterRect = new(chronicleRect.xMax + Gap, contentTop, SplitterWidth, contentHeight);

        // 派发记忆详情区绘制
        Rect detailsRect = new(splitterRect.xMax, contentTop, totalWidth - splitterRect.xMax, contentHeight);

        // 派发动作栏绘制，若不显示则传入 Rect.zero
        Rect selectionBarRect = _showSelectionBar ? new(0f, totalHeight - SelectionBarHeight, totalWidth, SelectionBarHeight) : Rect.zero;

        _context.InRect = _rect;

        _header.UpdateRect(headerRect);
        _chronicle.UpdateRect(chronicleRect);
        _timelineRect = timelineRect;
        _timeline.UpdateRect(_timelineRect.ContractedBy(Gap));
        _splitterRect = splitterRect;

        _details.UpdateRect(detailsRect);
        _selectionBar.UpdateRect(selectionBarRect);
    }

    protected override void Draw()
    {
        // 时间轴区背景
        Widgets.DrawMenuSection(_timelineRect);

        // 分发任务后，当前窗口的 draw 几乎仅需绘制时间轴-记忆详情区分割条和处理拖拽调整
        // 故不使用 do - while(false) + break，而是直接 return
        Widgets.DrawBoxSolid(_splitterRect, Mouse.IsOver(_splitterRect)

            ? new Color(0.55f, 0.6f, 0.66f, 0.8f)
            : new Color(0.25f, 0.28f, 0.32f, 0.8f));

        var current = Event.current;

        // 只响应左键事件
        if (current.button != 0) return;

        // 在区域内按下左键，记录锚点位置并激活拖拽状态
        if (current.type is EventType.MouseDown && _splitterRect.Contains(current.mousePosition))
        {
            _timelineResizeAnchor = current.mousePosition.x;
            current.Use();
            return;
        }

        // 其他逻辑只在拖拽激活时响应
        if (_timelineResizeAnchor == -1) return;

        // 在拖拽状态下，计算时间轴区宽度并更新设置
        if (current.type is EventType.MouseDrag)
        {
            var settings = RimTalkMemoryPatchMod.Settings;
            float mouseX = current.mousePosition.x;

            settings.MemoryTabTimeLineWidth = Math.Clamp(
                settings.MemoryTabTimeLineWidth + (mouseX - _timelineResizeAnchor),
                MinTimelineWidth,
                Math.Max(MinTimelineWidth, _context.InRect.width - MinDetailsWidth - Gap - SplitterWidth));
            _timelineResizeAnchor = mouseX;
            UpdateRects();
            current.Use();
            return;
        }

        // 鼠标抬起，结束拖拽状态并保存设置
        if (current.rawType is EventType.MouseUp)
        {
            _timelineResizeAnchor = -1;
            RimTalkMemoryPatchMod.Settings.Write();
        }
    }
}
