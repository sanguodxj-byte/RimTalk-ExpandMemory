using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI;

public abstract class ScrollUIElement : UIElement
{
    protected Vector2 _scrollPosition;
    protected Rect _totalRect;

    public virtual void ScrollPulse()
    {
        Widgets.BeginScrollView(_rect, ref _scrollPosition, _totalRect);
        Pulse();
        Widgets.EndScrollView();
    }
}
