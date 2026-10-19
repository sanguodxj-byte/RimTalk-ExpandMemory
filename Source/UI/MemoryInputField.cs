using HarmonyLib;
using TMPro;
using UnityEngine.EventSystems;

namespace RimTalk.Memory.UI;

public class MemoryInputField : TMP_InputField
{
    public static bool AnyFocused()
    {
        var currentSelectedGameObject = EventSystem.current?.currentSelectedGameObject;
        return currentSelectedGameObject != null // 此处的“!= null”为 unity 重载，不能用 is not 代替
            && currentSelectedGameObject.TryGetComponent<MemoryInputField>(out var input)
            && input.isFocused;
    }

    public override float preferredHeight => textComponent?.preferredHeight ?? 0f;

    // 重写 OnScroll 方法，强制把所有输入框滚轮交给详情区外层 ScrollRect，避免多行输入框在自身内部滚动
    private static readonly AccessTools.FieldRef<TMP_InputField, IScrollHandler> ParentScrollRectRef =
        AccessTools.FieldRefAccess<TMP_InputField, IScrollHandler>("m_IScrollHandlerParent");
    public override void OnScroll(PointerEventData eventData) => ParentScrollRectRef(this)?.OnScroll(eventData);
}
