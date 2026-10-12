using HarmonyLib;
using RimTalk.Memory.UI;
using Verse;

namespace RimTalk.Memory.Patches;

// 存在任意 MemoryInputField 获得焦点时，屏蔽游戏内快捷键等
[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.AnySearchWidgetFocused), MethodType.Getter)]
public static class WindowStack_AnySearchWidgetFocused_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (!MemoryInputField.AnyFocused()) return true;

        __result = true;
        return false;
    }
}
