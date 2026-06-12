using HarmonyLib;
using Verse;
using Verse.AI;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Patches.Capture
{

    /// <summary>
    /// Patch to capture job start as memories
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Pawn_JobTracker_StartJob_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Job newJob, Pawn ___pawn)
        {
            // 仅殖民者且启用工作记忆捕捉时才激活 capturer
            if (___pawn is null || !___pawn.IsColonist || !RimTalkMemoryPatchMod.Settings.enableActionMemory)
                return;

            ___pawn.GetComp<FourLayerMemoryComp>()?.JobCapturer?.BuildJobMemory(newJob);
        }
    }

}