using UnityEngine;

namespace RimTalk.Memory.UI;

/// <summary>
/// UI 组件树中的级联上下文，类似嵌套作用域。
/// 上下文沿父级链向下传播并逐层累积，子作用域可以查找祖先上下文，
/// 但祖先和其他分支无法反向看到当前作用域的数据。
/// </summary>
public abstract class UIContext
{
    private readonly UIContext _parentContext;

    /// <summary>
    /// 创建一个位于指定父级作用域下的上下文。
    /// </summary>
    /// <param name="parentContext">父级上下文，可以为 null。</param>
    protected UIContext(UIContext parentContext) => _parentContext = parentContext;

    /// <summary>
    /// 从当前作用域开始，沿父级链查找指定类型的上下文。
    /// </summary>
    /// <typeparam name="T">要查找的上下文类型。</typeparam>
    /// <returns>找到的上下文；不存在时返回 null。</returns>
    public T GetContext<T>() where T : UIContext => this is T context ? context : _parentContext?.GetContext<T>();

    /// <summary>
    /// 尝试从当前作用域开始，沿父级链查找指定类型的上下文。
    /// 不鼓励使用本方法，更建议使用 <see cref="GetContext{T}"/> 。
    /// </summary>
    /// <typeparam name="T">要查找的上下文类型。</typeparam>
    /// <param name="parentContext">找到的上下文；未找到时为 null。</param>
    /// <returns>找到上下文时返回 true，否则返回 false。</returns>
    public bool TryGetContext<T>(out T parentContext) where T : UIContext
    {
        parentContext = GetContext<T>();
        return parentContext is not null;
    }

    /// <summary>
    /// 驱动当前上下文的状态更新。
    /// 默认仅在 Unity 的 Layout 事件阶段调用 <see cref="Update"/>；
    /// 是否接入脉动以及调用时机由拥有该上下文的组件决定。
    /// </summary>
    public virtual void Pulse()
    {
        if (Event.current.type is EventType.Layout) Update();
    }

    /// <summary>
    /// 通知当前上下文执行关闭清理，以便释放局部状态。
    /// </summary>
    public virtual void PostClose() { }

    /// <summary>
    /// 在 Layout 阶段更新当前作用域维护的状态和派生不变量。
    /// </summary>
    protected virtual void Update() { }
}
