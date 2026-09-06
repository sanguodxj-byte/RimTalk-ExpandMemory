using UnityEngine;

namespace RimTalk.Memory.UI;

/// <summary>
/// 自制 UI 组件树中的基础元素。
/// 负责保存自身矩形，并提供统一的脉动、布局传播、绘制和关闭生命周期。
/// </summary>
public abstract class UIElement
{
    protected Rect _rect;

    /// <summary>
    /// 驱动当前元素的一次 UI 脉动。
    /// 默认在 Layout 阶段更新状态，随后执行绘制；派生类可按自身需要覆盖执行顺序。
    /// </summary>
    public virtual void Pulse()
    {
        if (Event.current.type is EventType.Layout) Update();
        Draw();
    }

    /// <summary>
    /// 设置当前元素的矩形，并重新计算自身及下属元素的布局矩形。
    /// </summary>
    /// <param name="rect">当前元素在父级坐标系中的矩形。</param>
    public virtual void UpdateRect(Rect rect)
    {
        _rect = rect;
        UpdateRects();
    }

    /// <summary>
    /// 通知当前元素执行关闭清理；子树的关闭通知由派生组件负责传播。
    /// </summary>
    public virtual void PostClose() { }

    /// <summary>
    /// 在 Layout 阶段更新当前元素的运行时状态。
    /// </summary>
    protected virtual void Update() { }

    /// <summary>
    /// 绘制当前元素并处理其区域内的即时交互。
    /// </summary>
    protected virtual void Draw() { }

    /// <summary>
    /// 根据当前 <see cref="_rect"/> 计算自身及下属元素的布局矩形。
    /// </summary>
    protected virtual void UpdateRects() { }
}
