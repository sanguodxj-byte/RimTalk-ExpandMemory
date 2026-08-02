using HarmonyLib;
using RimTalk.Memory.Capture;
using Verse;

namespace RimTalk.Memory.Patches.Capture;

// Verb.TryCastShot 成功后统一进入 Pawn.Notify_UsedVerb，避免记录仅开始瞄准的攻击意图
[HarmonyPatch(typeof(Pawn), "Notify_UsedVerb")]
public static class Pawn_Notify_UsedVerb_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, Verb verb)
    {
        CombatMemoryCapturer.CaptureAttackEnter(__instance, verb);
    }
}
