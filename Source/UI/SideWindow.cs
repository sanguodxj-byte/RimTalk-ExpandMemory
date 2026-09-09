using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI;

/// <summary>
/// 窗口位置/尺寸由调用方一次性算好传入（紧贴触发按钮右侧、收缩在主标签内容区内），呈现“侧边栏”观感。
/// </summary>
public abstract class SideWindow : Window
{
    // 外部算好传入的矩形
    private readonly Rect _sidebarRect;

    public SideWindow(Rect sidebarRect)
    {
        _sidebarRect = sidebarRect;

        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
    }

    // 直接采用调用方算好的矩形，跳过默认的居中布局。
    protected override void SetInitialSizeAndPosition() => windowRect = _sidebarRect.Rounded();
}
