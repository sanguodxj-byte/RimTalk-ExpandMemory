using HarmonyLib;
using RimTalk.Memory.Capture;
using Verse;
using Verse.AI;

namespace RimTalk.Memory.Patches.Capture
{

    // 通过 Postfix 捕获 StartJob 后的 curJob
    // 将一些 CleanupCurrentJob 时会丢失的信息提前提取出来
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Pawn_JobTracker_StartJob_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Job ___curJob, Pawn ___pawn)
        {
            // 相关检定全部交由下游
            JobMemoryCapturer.ExtractJobInfoEnter(___curJob, ___pawn);
        }
    }

    // 通过 Prefix 捕获 CleanupCurrentJob 时的 curJob
    // 逻辑上即 hook 工作的**完成/结束**
    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    public static class Pawn_JobTracker_CleanupCurrentJob_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Job ___curJob, Pawn ___pawn) // 未来此处还可传入 JobCondition 进行更精密的上游判断或下游操作
        {
            // 相关检定全部交由下游
            JobMemoryCapturer.BuildJobMemoryEnter(___curJob, ___pawn);
        }
    }

}