using HarmonyLib;
using RimWorld;
using RimTalk.Memory.UI.TabWindow;
using UnityEngine;
using Verse;

namespace RimTalk.MemoryPatch;

/// <summary>
/// 原版会在地图鼠标按下时关闭所有非 Inspect 主标签并清空选择。
/// 记忆档案需要保留窗口，让 Selector 完成 Pawn 选择后由窗口切换所有者。
/// </summary>
[HarmonyPatch(typeof(MainTabsRoot), "HandleLowPriorityShortcuts")]
public static class MainTabsRoot_HandleLowPriorityShortcuts_Patch
{
    [HarmonyPrefix]
    // 只会在当前打开的标签页是 MemoryTabWindowBridge，且事件为鼠标左键按下时，短路目标方法
    private static bool Prefix() =>
        !(
        Find.MainTabsRoot?.OpenTab?.TabWindow is MemoryTabWindowBridge
        && Event.current.type is EventType.MouseDown && Event.current.button == 0
        );
}
