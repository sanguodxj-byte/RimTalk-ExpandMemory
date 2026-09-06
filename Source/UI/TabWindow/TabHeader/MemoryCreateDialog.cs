using RimTalk.Memory.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.TabHeader;

/// <summary>
/// 新建记忆表单。层级在窗口内选择，调用方直接 Find.WindowStack.Add(new MemoryCreateDialog(memoryComp))。
/// </summary>
public sealed class MemoryCreateDialog : Window
{
    // 尺寸常量
    private const float TitleHeight = 34f;
    private const float Gap = 8f;
    private const float WidgetsHeight = 30f;
    private const float LabelHeight = 24f;
    private const float ContentHeight = 150f;
    private const float ButtonWidth = 120f;

    // 所有层级和类型的枚举值列表，供下拉菜单使用
    private static readonly List<MemoryLayer> _allLayers = EnumUtil.AllEnumValues<MemoryLayer>()?.ToList() ?? new();
    private static readonly List<MemoryType> _allTypes = [MemoryType.Action, MemoryType.Conversation]; // 暂时只开放 Action 和 Conversation

    // 创建记忆的目标组件
    private readonly FourLayerMemoryComp _memoryComp;

    // 记忆内容
    private MemoryLayer _layer = MemoryLayer.Active;
    private MemoryType _type = MemoryType.Conversation;
    private string _content = string.Empty;
    private string _tags = string.Empty;
    private string _note = string.Empty;
    private float _importance = MemoryEntry.DefaultImportance;
    private bool _pinned = false;

    public override Vector2 InitialSize => new(620f, 560f);

    public MemoryCreateDialog(FourLayerMemoryComp memoryComp)
    {
        _memoryComp = memoryComp;

        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (_memoryComp is null) return;

        float width = inRect.width;
        const float x = 0f;
        float y = 0f;

        // 绘制标题
        Rect titleRect = new(x, y, width, TitleHeight);
        using (new TextBlock(GameFont.Medium))
            Widgets.Label(titleRect, "RimTalk.Memory.UI.TabWindow.CreateTitle".Translate((_memoryComp.parent?.LabelShort).Named("NAME")));
        y += TitleHeight + 2 * Gap;

        // 层级选择：在窗口内决定新建记忆的层级
        Rect layerRect = new(x, y, width, WidgetsHeight);
        if (Widgets.ButtonText(layerRect, _layer.Translate()))
            OpenLayerMenu();
        y += WidgetsHeight + Gap;

        // Active / Situational 层允许选择行动或对话；EventLog / Archive 层强制使用 Summarization
        if (_layer is MemoryLayer.Active or MemoryLayer.Situational)
        {
            Rect typeRect = new(x, y, width, WidgetsHeight);
            if (Widgets.ButtonText(typeRect, _type.Translate()))
                OpenTypeMenu();
            y += WidgetsHeight + Gap;
        }

        // 绘制 content 控件
        Rect contentLabelRect = new(x, y, width, LabelHeight);
        Widgets.Label(contentLabelRect, "RimTalk.Memory.UI.TabWindow.ContentLabel".Translate());
        y += LabelHeight;

        Rect contentRect = new(x, y, width, ContentHeight);
        _content = Widgets.TextArea(contentRect, _content);
        y += ContentHeight + Gap;

        // 绘制 tags 控件
        Rect tagsLabelRect = new(x, y, width, LabelHeight);
        Widgets.Label(tagsLabelRect, "RimTalk.Memory.UI.TabWindow.TagsLabel".Translate());
        y += LabelHeight;

        Rect tagsRect = new(x, y, width, WidgetsHeight);
        _tags = Widgets.TextField(tagsRect, _tags);
        y += WidgetsHeight + Gap;

        // 绘制 notes 控件
        Rect notesLabelRect = new(x, y, width, LabelHeight);
        Widgets.Label(notesLabelRect, "RimTalk.Memory.UI.TabWindow.NotesLabel".Translate());
        y += LabelHeight;

        Rect notesRect = new(x, y, width, WidgetsHeight);
        _note = Widgets.TextField(notesRect, _note);
        y += WidgetsHeight + Gap;

        // 绘制 importance 控件
        Rect importanceLabelRect = new(x, y, width, LabelHeight);
        Widgets.Label(importanceLabelRect, "RimTalk.Memory.UI.TabWindow.CurrentImportance".Translate() + _importance);
        y += LabelHeight;

        Rect importanceRect = new(x, y, width, WidgetsHeight);
        _importance = Widgets.HorizontalSlider(importanceRect, _importance, 0f, 1f, middleAlignment: true);
        y += WidgetsHeight + Gap;

        // 绘制 pin 控件
        Rect pinRect = new(x, y, width, WidgetsHeight);
        Widgets.CheckboxLabeled(pinRect, "RimTalk.Memory.UI.TabWindow.PinLabel".Translate(), ref _pinned);
        y += WidgetsHeight + Gap;

        // 右下角按钮
        float yBottom = inRect.yMax;
        float xRight = inRect.xMax - WidgetsHeight;

        Rect cancelRect = new(xRight - ButtonWidth, yBottom - WidgetsHeight, ButtonWidth, WidgetsHeight);
        if (Widgets.ButtonText(cancelRect, "RimTalk.Memory.UI.TabWindow.Cancel".Translate())) Close();
        xRight -= ButtonWidth + Gap;

        Rect saveRect = new(xRight - ButtonWidth, yBottom - WidgetsHeight, ButtonWidth, WidgetsHeight);
        if (Widgets.ButtonText(saveRect, "RimTalk.Memory.UI.TabWindow.Save".Translate())) Save();
    }

    private void OpenLayerMenu() =>
        Find.WindowStack.Add(new FloatMenu(_allLayers
            .Select(layer => new FloatMenuOption(layer.Translate(), () => _layer = layer))
            .ToList()));

    private void OpenTypeMenu() =>
        Find.WindowStack.Add(new FloatMenu(_allTypes
            .Select(type => new FloatMenuOption(type.Translate(), () => _type = type))
            .ToList()));

    private void Save()
    {
        // 总结层只允许创建 Summarization；具体经历层允许玩家选择行动或对话。
        if (_layer is MemoryLayer.EventLog or MemoryLayer.Archive)
            _type = MemoryType.Summarization;

        var memory = new MemoryEntry(_content, _type, _layer, _importance)
        {
            Note = _note,
            IsPinned = _pinned
        };
        memory.Tags ??= new();
        memory.Tags.AddRange(_tags.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries).Select(tag => tag.Trim()));

        _memoryComp.Interactor.AddMemory(memory);

        Messages.Message("RimTalk.Memory.UI.TabWindow.Created".Translate(), MessageTypeDefOf.TaskCompletion, false);
        Close();
    }
}
