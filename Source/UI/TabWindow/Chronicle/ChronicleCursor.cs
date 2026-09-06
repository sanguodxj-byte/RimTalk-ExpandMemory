using RimTalk.Memory.Utils;
using System;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Chronicle;

public class ChronicleCursor : UIElement
{
    // 常量配置
    private const float CursorHeight = 14f;
    private const float CursorWidth = CursorHeight * 1.1547005383792515290182975610039f; // 等边三角形的宽高比
    private const float HighlightFactor = 0.35f;
    private static Color CursorColor => new(0.93f, 0.72f, 0.34f, 0.95f);

    // 成员
    private readonly UIContext _context;
    private readonly MemoryChronicle.Context _chronicleContext; // 快捷访问
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private bool _dragging = false;

    public ChronicleCursor(UIContext context)
    {
        _context = context;
        _chronicleContext = _context?.GetContext<MemoryChronicle.Context>() ?? throw new ArgumentNullException(nameof(context));
        _tabContext = _context?.GetContext<MemoryTabWindow.Context>() ?? throw new ArgumentNullException(nameof(context));

        // 时间轴会触发光标重定位检测，拉动视窗将光标移入视野内
        _tabContext.RePositionCursor += RePositionCursor;
    }

    private void RePositionCursor()
    {
        // 变量拉取和校验
        int startTick = _chronicleContext.ChronicleStartTick;
        int endTick = _chronicleContext.ChronicleEndTick;

        if (startTick >= endTick) return;

        int cursorTick = _tabContext.CursorTick;

        // 拖动区间
        if (cursorTick < startTick)
        {
            _chronicleContext.ChronicleStartTick = cursorTick;
            _chronicleContext.ChronicleEndTick = _chronicleContext.ChronicleStartTick + (endTick - startTick);
            return;
        }
        if (cursorTick > endTick)
        {
            _chronicleContext.ChronicleEndTick = cursorTick;
            _chronicleContext.ChronicleStartTick = _chronicleContext.ChronicleEndTick - (endTick - startTick);
            return;
        }
    }

    protected override void Draw()
    {
        int startTick = _chronicleContext.ChronicleStartTick;
        int endTick = _chronicleContext.ChronicleEndTick;

        if (startTick >= endTick) return;

        int cursorTick = _tabContext.CursorTick;

        // 仅当 curortick 在区间内时才绘制
        if (startTick <= cursorTick && cursorTick <= endTick)
            // 截断超出的三角形部分
            // 注意裁剪作用域内为局部坐标系
            using (new CropBlock(_rect))
            // 正在拖拽时高亮
            using (new TextBlock(_dragging ? Color.Lerp(CursorColor, Color.white, HighlightFactor) : CursorColor))
                GUI.DrawTexture(
                    new Rect(
                        0f + (float)(cursorTick - startTick) / (endTick - startTick) * _rect.width - CursorWidth / 2f,
                        0f, CursorWidth, CursorHeight
                        ),
                    TexButton.ReorderUp
                    );

        // 点击和拖拽
        do
        {
            var current = Event.current;

            // 只响应左键事件
            if (current.button != 0) break;

            Vector2 mouse = current.mousePosition;

            // 在区域内按下时更新 cursortick，激活拖拽，并触发时间轴重定位
            if (current.type is EventType.MouseDown)
            {
                if (_rect.Contains(mouse))
                {
                    MoveCursor(mouse.x);
                    _dragging = true;
                }
                break;
            }

            // 其他逻辑只在拖拽状态下响应
            if (!_dragging) break;

            // 拖拽时实时更新 cursortick 并重定位时间轴
            if (current.type is EventType.MouseDrag)
            {
                MoveCursor(Math.Clamp(mouse.x, _rect.x, _rect.xMax));
                break;
            }

            // 鼠标抬起，结束拖拽
            if (current.rawType is EventType.MouseUp)
            {
                _dragging = false;
                break;
            }

            // DRY
            void MoveCursor(float mouseX)
            {
                _tabContext.CursorTick = (int)((mouseX - _rect.x) / _rect.width * (endTick - startTick)) + startTick;
                _tabContext.RaiseRePositionTimeline();
                current.Use();
            }
        } while (false);
    }
}
