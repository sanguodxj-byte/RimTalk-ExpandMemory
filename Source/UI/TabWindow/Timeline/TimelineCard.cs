using RimTalk.Memory.Utils;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Timeline;

/// <summary>
/// 时间轴记忆卡片，纵向时间流中的可视单元。
/// </summary>
public class TimelineCard : MemoryCard
{
    /// <summary>
    /// 按记忆类型推算卡片高度
    /// </summary>
    public static float HeightOf(MemoryEntry memory) => memory.Type is MemoryType.Action ? 68f : 96f;

    // 构造函数
    public TimelineCard(UIContext context, MemoryEntry memory) : base(context, memory) { }

    protected override string GetTitle() =>
        $"{Memory?.Type.Translate()} · {Memory?.AgeString}";

    protected override void HandleSelect()
    {
        // 点击卡片时，设置为焦点并加入 selection，按住 control 键时可以多选
        if (Widgets.ButtonInvisible(_rect, false))
        {
            var current = Event.current;

            if (!current.control) _tabContext.Selection.Clear();

            // 如果按住 shift 键，则尝试选中焦点记忆和当前记忆之间的所有记忆
            if (current.shift
                && _tabContext.Focuse is { } focus
                && _tabContext.TreatedTimelineMemories is { } timelineMemories
                && timelineMemories.Contains(focus))
            {
                int dir = Memory.GameTick > focus.GameTick ? -1 : 1;
                for (int i = timelineMemories.IndexOf(focus); 0 <= i && i < timelineMemories.Count; i += dir)
                {
                    var memory = timelineMemories[i];
                    if (memory == Memory) break;
                    _tabContext.Selection.Add(memory);
                }
            }
            _tabContext.Selection.Add(Memory);

            _tabContext.SetFocuse(Memory);
        }
    }
}
