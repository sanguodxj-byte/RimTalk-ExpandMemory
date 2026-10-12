using RimTalk.Memory.Utils;
using RimWorld;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Verse;

namespace RimTalk.Memory.UI.TabWindow.Details;

/// <summary>
/// 基于 UGUI 的记忆详情区，负责显示和编辑当前选中记忆的内容和元数据。
/// </summary>
public class MemoryDetailsMain : UGUIElement
{
    private const string NameSpace = "RimTalk.Memory.UI.TabWindow.";

    // 常量配置
    private const float ContentLineSpacing = 15f; // 行距百分数
    private const float ContentParagraphSpacing = 50f; // 段距百分数

    // UGUI 使用物理像素；由逻辑尺寸和游戏 UI 缩放派生。
    private static float ScrollbarWidth => 14f * Prefs.UIScale;
    private static int FieldPadX => (int)(6f * Prefs.UIScale);
    private static int FieldPadY => (int)(6f * Prefs.UIScale);
    private static float TextFontSize => 16f * Prefs.UIScale;
    private static float DividerHeight => 2f * Prefs.UIScale;
    private static float ScrollbarInset => 2f * Prefs.UIScale;
    private static float ScrollbarEndInset => 4f * Prefs.UIScale;
    private static float LabelGap => 2f * Prefs.UIScale;
    private static float Gap => MemoryTabWindow.Gap * Prefs.UIScale;
    private static float ScrollSensitivity => 48f * Prefs.UIScale;

    // 颜色配置
    private static readonly Color TextColor = new(0.9f, 0.9f, 0.9f);
    private static readonly Color DividerColor = new(1f, 1f, 1f, 0.4f);
    private static readonly Color ScrollbarBGColor = new(1f, 1f, 1f, 0.1f);
    private static readonly Color ScrollbarHandleColor = new(0.62f, 0.64f, 0.66f, 0.8f);

    // 成员
    private readonly UIContext _context;
    private readonly MemoryTabWindow.Context _tabContext; // 快捷访问
    private readonly MemoryDetails.Context _detailsContext; // 快捷访问
    private TMP_InputField _contentField;
    private TMP_InputField _importanceField;
    private TMP_InputField _activityField;
    private TMP_InputField _tagsField;
    private TMP_InputField _notesField;
    private TextMeshProUGUI _statusText;

    public MemoryDetailsMain(UIContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tabContext = context.GetContext<MemoryTabWindow.Context>()
            ?? throw new ArgumentException(nameof(context));
        _detailsContext = context.GetContext<MemoryDetails.Context>()
            ?? throw new ArgumentException(nameof(context));

        _tabContext.FocuseChanged += CheckFocus;

        _detailsContext.BeginEdit += () => SwitchEditing(true);
        _detailsContext.SaveEdit += SaveEdit;
        _detailsContext.EndEdit += () => SwitchEditing(false);
    }
    private void CheckFocus()
    {
        if (_tabContext.Focuse is not null) EnableCanvas();
        else DisableCanvas();
    }
    private void SwitchEditing(bool editing)
    {
        // 所有字段统一切换 readOnly，一键切换编辑态/只读态。
        _contentField?.readOnly = !editing;
        _importanceField?.readOnly = !editing;
        _activityField?.readOnly = !editing;
        _tagsField?.readOnly = !editing;
        _notesField?.readOnly = !editing;
    }
    private void SaveEdit()
    {
        var memory = _detailsContext.EditingMemory;
        memory.Content = _contentField?.text?.Trim();
        memory.Note = _notesField?.text?.Trim();
        memory.Tags = _tagsField?.text?
            .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToList();
        if (float.TryParse(_importanceField?.text, out float importance))
            memory.Importance = importance;
        if (float.TryParse(_activityField?.text, out float activity))
            memory.Activity = activity;
    }

    protected override void Draw()
    {
        base.Draw();

        // 避免滚轮事件泄露
        if (Event.current is { type: EventType.ScrollWheel } current
            && _rect.Contains(current.mousePosition))
            current.Use();
    }

    protected override void Update() => UpdateUGUI();

    // 初始化 UGUI 组件树，结构为 Canvas → ScrollRect → Viewport → Content → fields
    protected override (GameObject, RectTransform) InitiateUGUI()
    {
        // 构建 Canvas
        var canvasObject = NewGameObject("Canvas");
        canvasObject.SetActive(false); // 先不激活，避免在构建过程中触发 OnEnable
        canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera; // 画布直接和屏幕对齐
        canvasObject.AddComponent<GraphicRaycaster>();

        // 构建 Scroll：即详情区根
        var scroll = NewGameObject("ScrollRect", canvasObject);
        var rootRect = scroll.AddComponent<RectTransform>();
        // 不继承缩放、pivot 为 rect 左下角
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = Vector2.zero;

        // 构建 Viewport
        var viewport = NewGameObject("Viewport", scroll);
        var viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new(-ScrollbarWidth - ScrollbarInset, 0f);
        viewport.AddComponent<RectMask2D>();
        // 让整个 Viewport，包括分隔线和字段间距也能命中父级 ScrollRect。
        viewport.AddComponent<Image>().color = Color.clear;

        // 构建 Content
        var content = NewGameObject("Content", viewport);
        var contentRect = content.AddComponent<RectTransform>();
        // 上边缘对齐
        contentRect.anchorMin = new(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new(0.5f, 1f);
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
        // 自动纵向布局
        BuildVerticalLayoutGroup(content, Gap);

        // 构建 ScrollRect 组件
        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = ScrollSensitivity; // 每个滚轮单位移动约三行文本
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        // 构建关联关系
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        // 构建滑条
        scrollRect.verticalScrollbar = CreateScrollbar(scroll);

        // 构建各个末端文本对象
        BuildFields(content);

        return (canvasObject, rootRect);
    }

    // 按显示顺序创建详情字段
    private void BuildFields(GameObject parent)
    {
        _contentField = CreateField("Content", parent);
        _contentField.textComponent.lineSpacing = ContentLineSpacing;
        _contentField.textComponent.paragraphSpacing = ContentParagraphSpacing;

        // 在内容字段和其他字段之间添加一个分隔线，视觉上区分内容和元数据。
        var divider = NewGameObject("Divider", parent);
        divider.AddComponent<LayoutElement>().preferredHeight = DividerHeight;
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = DividerColor;
        dividerImage.raycastTarget = false;

        _importanceField = CreateFieldWithLabel("Importance", parent);
        _activityField = CreateFieldWithLabel("Activity", parent);
        _tagsField = CreateFieldWithLabel("Tags", parent);
        _notesField = CreateFieldWithLabel("Notes", parent);

        _statusText = CreateText("Status", parent);
    }

    private void UpdateUGUI()
    {
        // 只读状态下每帧拉取显示值
        if (_detailsContext.EditingMemory is not null
            || _tabContext.Focuse is not { } focuse) return;

        _contentField?.SetTextWithoutNotify(focuse.Content);
        _importanceField?.SetTextWithoutNotify(focuse.Importance.ToString("F2"));
        _activityField?.SetTextWithoutNotify(focuse.Activity.ToString("F2"));
        _tagsField?.SetTextWithoutNotify(string.Join(", ", focuse.Tags ?? []));
        _notesField?.SetTextWithoutNotify(focuse.Note);

        var summarizer = _tabContext.MemoryComp?.Summarizer;
        _statusText.text =
            $"{(focuse.IsPinned
            ? (NameSpace + "Pinned").Translate()
            : (NameSpace + "NotPinned").Translate())} · " +
            $"{(summarizer?.CheckSummarizing(focuse) ?? false
            ? (NameSpace + "Summarizing").Translate()
            : summarizer?.CheckSummarized(focuse) ?? false
            ? (NameSpace + "Summarized").Translate()
            : (NameSpace + "NotSummarized").Translate())}";
    }

    // 创建滑条
    private static Scrollbar CreateScrollbar(GameObject parent)
    {
        // 滑条根对象
        var scrollbarObject = NewGameObject("Scrollbar", parent);
        var rect = scrollbarObject.AddComponent<RectTransform>();
        rect.anchorMin = new(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new(1f, 0.5f);
        rect.offsetMin = new(-ScrollbarWidth - ScrollbarInset, ScrollbarEndInset);
        rect.offsetMax = new(-ScrollbarInset, -ScrollbarEndInset);
        // 滑条背景
        var background = scrollbarObject.AddComponent<Image>();
        background.color = ScrollbarBGColor;
        background.raycastTarget = true;

        // 滑块对象
        var handleObject = NewGameObject("Handle", scrollbarObject);
        // 滑块 Image
        var handleImage = handleObject.AddComponent<Image>();
        handleImage.color = ScrollbarHandleColor;
        handleImage.raycastTarget = true;
        // add Image 组件时已自动添加 RectTransform
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;

        // 滑条组件
        var scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = background;
        scrollbar.handleRect = handleRect;

        return scrollbar;
    }

    // 创建一个带标签的输入框，标签和输入框垂直排列，返回输入框组件
    private static TMP_InputField CreateFieldWithLabel(string name, GameObject parent)
    {
        // 构建嵌套组
        var group = NewGameObject(name + ".Group", parent);
        // 自动布局
        BuildVerticalLayoutGroup(group, LabelGap);

        // 构建标签
        var label = CreateText(name, group);
        label.text = (NameSpace + name).Translate();

        // 构建并返回输入框
        return CreateField(name, group);
    }

    // 构建输入框
    // 字段自身 rect 由外层布局控制；文本 rect 用锚点偏移表达内边距
    private static TMP_InputField CreateField(string name, GameObject parent)
    {
        // 构建末端的输入框对象
        var fieldObject = NewGameObject(name, parent);
        // 根据内容自动调整高度，宽度由外层布局控制
        var fitter = fieldObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 输入框背景
        var background = fieldObject.AddComponent<Image>();
        background.color = Widgets.WindowBGFillColor;
        background.raycastTarget = true;

        // 输入框组件
        var field = fieldObject.AddComponent<MemoryInputField>();
        field.lineType = TMP_InputField.LineType.MultiLineNewline;
        field.interactable = true; // 始终可交互
        field.readOnly = true; // 初始为只读
        field.textComponent = CreateText(name, fieldObject);
        field.targetGraphic = background;
        field.textViewport = fieldObject.GetComponent<RectTransform>();
        return field;
    }

    private static GameObject NewGameObject(string name, GameObject parent = null)
    {
        var go = new GameObject(NameSpace + name);
        if (parent is not null)
            go.transform.SetParent(parent.transform, false);
        return go;
    }

    // 纵向自动布局
    private static void BuildVerticalLayoutGroup(GameObject go, float spacing = 0f, RectOffset padding = null)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        if (padding is not null)
            layout.padding = padding;
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 使用 ContentSizeFitter 根据子元素尺寸自动调整高度
        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // 构建文本
    private static TextMeshProUGUI CreateText(string name, GameObject parent)
    {
        var textObject = NewGameObject(name + ".Text", parent);

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TextUtil.InternalFont;
        text.fontSize = TextFontSize;
        text.color = TextColor;
        text.enableWordWrapping = true;
        text.margin = new Vector4(FieldPadX, FieldPadY, FieldPadX, FieldPadY);

        var textRect = textObject.GetComponent<RectTransform>(); // add TextMeshProUGUI 时已自动添加 RectTransform
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        return text;
    }
}
