using RimTalk.Memory.Utils;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow;

public abstract class MemoryCard : UIElement
{
    // 尺寸常量
    private const float Gap = MemoryTabWindow.Gap;
    private const float ShowPinWidthThreshold = 30f;
    private const float ShowContentWidthThreshold = 54f;
    private const float PinSize = 22f;
    private const float PinRingOuterRadius = 6f;
    private const float PinTexHeight = 27f;
    private const float TitleHeight = 22f;

    // 配色常量
    private const float ColorBGFactor = 0.16f;
    private const float HoverFactor = 0.35f;
    private const float PressFactor = 0.15f;
    private const float PulseFrequency = 1.5f;

    // 配色与纹理
    private static readonly Texture2D _pinTex = ContentFinder<Texture2D>.Get("RimTalk-ExpandMemory/UI/pin");
    private static Color RingColor => new(0.72f, 0.7f, 0.76f, 0.9f);

    // 实例成员
    public readonly MemoryEntry Memory;
    private readonly UIContext _context;
    protected readonly MemoryTabWindow.Context _tabContext; // 快捷访问

    /// <summary>
    /// 只读属性，获取记忆卡片的矩形区域
    /// </summary>
    public Rect Rect => _rect;

    /// <summary>
    /// 子类可覆写透明度逻辑
    /// </summary>
    protected virtual float BGAlpha => Memory.Activity;

    // 卡片颜色，随记忆层级变化
    private Color CardColor => Memory?.Layer switch
    {
        MemoryLayer.Active => new(0.28f, 0.68f, 0.84f),
        MemoryLayer.Situational => new(0.34f, 0.72f, 0.52f),
        MemoryLayer.EventLog => new(0.86f, 0.64f, 0.28f),
        MemoryLayer.Archive => new(0.64f, 0.42f, 0.78f),
        _ => Color.gray
    };
    private Color CardColorBG => CardColor * ColorBGFactor;

    // 构造函数
    protected MemoryCard(UIContext context, MemoryEntry memory)
    {
        _context = context;
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context) + nameof(memory));
        Memory = memory;
    }

    protected sealed override void Draw()
    {
        if (Memory is null) return;

        // 计算卡片底色，透明度随活跃度变化
        Color cardColorBG = new(CardColorBG.r, CardColorBG.g, CardColorBG.b, BGAlpha);
        if (Mouse.IsOver(_rect) && Input.GetMouseButton(0))
            cardColorBG *= PressFactor;

        // 卡片背景
        Widgets.DrawBoxSolid(_rect, cardColorBG);

        // 绘制边框
        Color cardColor = new(CardColor.r, CardColor.g, CardColor.b, BGAlpha);
        using (new TextBlock(cardColor))
            Widgets.DrawBox(_rect, 2);

        // 正在总结的记忆，绘制紫色脉冲边框
        if (_tabContext.MemoryComp?.Summarizer?.CheckSummarizing(Memory) ?? false)
            using (new TextBlock(new Color(0.5f, 0f, 1f, MathUtil.SinePulse(PulseFrequency, 0.1f, 0.8f))))
                Widgets.DrawBox(_rect.ExpandedBy(2f), 2);

        // 被选中的记忆，绘制白色脉冲边框
        if (_tabContext.Selection.Contains(Memory))
            using (new TextBlock(new Color(1f, 1f, 0.92f, MathUtil.SinePulse(PulseFrequency, 0.1f, 0.8f, phase: MathF.PI))))
                Widgets.DrawBox(_rect.ExpandedBy(2f), 2);

        // 绘制内容主体
        do
        {
            // 绘制 pin 区
            if (_rect.width < ShowPinWidthThreshold) break;

            // pin 响应区
            float rightX = _rect.xMax - Gap;

            Rect pinRect = new(rightX - PinSize, _rect.y, PinSize, PinSize);
            TooltipHandler.TipRegion(pinRect, (Memory.IsPinned ? "RimTalk.Memory.UI.TabWindow.Unpin" : "RimTalk.Memory.UI.TabWindow.Pin").Translate());
            if (Widgets.ButtonInvisible(pinRect))
                _tabContext.MemoryComp?.Interactor?.PinMemory(Memory, !Memory.IsPinned);
            rightX -= PinSize + Gap;

            // 计算 pin 圆环中心，在 pin 响应区右下角
            Vector2 ringCenter = new(pinRect.xMax - PinRingOuterRadius, pinRect.yMax - PinRingOuterRadius);

            bool hovered = Mouse.IsOver(pinRect);

            // 绘制圆环，悬停时颜色变亮
            WidgetsUtil.CircleImage(ringCenter, PinRingOuterRadius, hovered ? Color.Lerp(RingColor, Color.white, HoverFactor) : RingColor, WidgetsUtil.RingTex);

            // 绘制 pin 图标，图标右下角（针尖）对齐圆环中心
            if (Memory.IsPinned || hovered)
            {
                // 为图钉纹理分配 rect
                float pinTexWidth = PinTexHeight * _pinTex.width / _pinTex.height;
                Rect pinTexRect = new(ringCenter.x - pinTexWidth, ringCenter.y - PinTexHeight, pinTexWidth, PinTexHeight);

                // 被固定但未悬停时，图钉材质不变
                if (!hovered)
                    GUI.DrawTexture(pinTexRect, _pinTex, ScaleMode.ScaleToFit);
                // 被固定且悬停时，图钉材质变亮；未被固定但悬停时，绘制半透明图钉
                else
                    using (new TextBlock(Memory.IsPinned ? GenUI.MouseoverColor : new Color(1f, 1f, 1f, 0.38f)))
                        GUI.DrawTexture(pinTexRect, _pinTex, ScaleMode.ScaleToFit);
            }

            // 绘制内容区
            if (_rect.width < ShowContentWidthThreshold) break;

            float x = _rect.x + Gap;
            float y = _rect.y + Gap;

            Rect titleRect = new(x, y, rightX - x, TitleHeight);
            Widgets.Label(titleRect, GetTitle());
            y += TitleHeight + Gap * 0.5f;

            rightX = _rect.xMax - Gap;
            float bottomY = _rect.yMax - Gap;

            Rect contentRect = new(x, y, rightX - x, bottomY - y);
            Widgets.Label(contentRect, Memory.Content);
        } while (false);

        HandleSelect();
    }

    protected virtual void HandleSelect()
    {
        // 点击卡片时，设置为焦点并加入 selection，按住 control 键时可以多选
        if (Widgets.ButtonInvisible(_rect, false))
        {
            _tabContext.SetFocuse(Memory);

            if (!Event.current.control) _tabContext.Selection.Clear();
            _tabContext.Selection.Add(Memory);
        }
    }

    protected abstract string GetTitle();
}
