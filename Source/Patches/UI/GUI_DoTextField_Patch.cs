using HarmonyLib;
using RimTalk.Memory.UI;
using UnityEngine;

namespace RimTalk.Memory.Patches;

// MemoryInputField 持有焦点时，失焦 IMGUI
[HarmonyPatch(typeof(GUI), "DoTextField", [
    typeof(Rect), typeof(int), typeof(GUIContent), typeof(bool),
    typeof(int), typeof(GUIStyle), typeof(string), typeof(char)
])]
public static class GUI_DoTextField_Patch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (MemoryInputField.AnyFocused()) GUIUtility.keyboardControl = 0;
    }
}
