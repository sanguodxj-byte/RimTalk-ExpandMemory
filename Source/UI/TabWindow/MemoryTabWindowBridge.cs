using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow;

/// <summary>
/// 桥接至自制 UI 框架
/// </summary>
public class MemoryTabWindowBridge : MainTabWindow
{
    private readonly MemoryTabWindow _memoryTabWindow = new(null);

    // 期望的标签页尺寸，实际绘制区会有缩进
    public override Vector2 RequestedTabSize => new(1280f, 760f);

    public Rect InRect
    {
        get
        {
            var inRect = windowRect.AtZero().ContractedBy(Margin);
            if (!optionalTitle.NullOrEmpty())
                inRect.yMin += Margin + 25f;
            return inRect.AtZero();
        }
    }

    public MemoryTabWindowBridge() => doCloseX = true;

    // （潜在）改变窗口尺寸和位置时，通知 MemoryTabWindow 更新绘制区
    protected override void SetInitialSizeAndPosition()
    {
        base.SetInitialSizeAndPosition();
        _memoryTabWindow.UpdateRect(InRect);
    }

    public override void DoWindowContents(Rect _)
    {
        // 因为傻福泰南的字体泄露，这里不得不在最根部定义一个基底字体
        using (new TextBlock(GameFont.Small))
            _memoryTabWindow.Pulse();
    }

    public override void PostClose()
    {
        base.PostClose();
        _memoryTabWindow.PostClose();
    }
}
