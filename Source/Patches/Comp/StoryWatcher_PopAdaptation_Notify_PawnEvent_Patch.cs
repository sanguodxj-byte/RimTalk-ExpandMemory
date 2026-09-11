using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalk.Memory.Patches.Comp;

// 新殖民者自动激活所有子组件
[HarmonyPatch(typeof(StoryWatcher_PopAdaptation), "Notify_PawnEvent")]
public static class StoryWatcher_PopAdaptation_Notify_PawnEvent_Patch
{
    [HarmonyPostfix]
    private static void Postfix(Pawn p, PopAdaptationEvent ev)
    {
        // ev 门控**完全多余**，仅用作防御性编程，以及预见性适配
        if (ev is not PopAdaptationEvent.GainedColonist
            || !p.IsColonist
            || p.TryGetComp<FourLayerMemoryComp>() is not { } memoryComp)
            return;

        memoryComp.AddJobCapturer();
        memoryComp.AddCombatCapturer();
        memoryComp.AddSummarizer();
    }
}
