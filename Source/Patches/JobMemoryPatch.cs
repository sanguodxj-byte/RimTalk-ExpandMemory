using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using RimTalk.Memory;
using System.Reflection;
using RimTalk.MemoryPatch;

namespace RimTalk.Patches
{
    /// <summary>
    /// Patch to capture job start as memories
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class JobStartMemoryPatch
    {
        private static readonly FieldInfo pawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
        
        [HarmonyPostfix]
        public static void Postfix(Pawn_JobTracker __instance, Job newJob)
        {
            Pawn pawn = pawnField?.GetValue(__instance) as Pawn;
            if (pawn == null || !pawn.IsColonist || newJob == null || newJob.def == null)
                return;

            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null)
                return;

            // Check if action memory is enabled
            if (!RimTalkMemoryPatchMod.Settings.enableActionMemory)
                return;

            // Skip insignificant jobs (Bug 4: ignore wandering and standing)
            if (!IsSignificantJob(newJob.def))
                return;

            // Build memory content
            string content = newJob.def.reportString;
            
            // Fix Bug 2: Only add target info if it's meaningful and not "TargetA"
            if (newJob.targetA.HasThing && newJob.targetA.Thing != pawn)
            {
                string targetName = newJob.targetA.Thing.LabelShort;
                if (!string.IsNullOrEmpty(targetName) && targetName != "TargetA")
                {
                    content = content + " - " + targetName;
                }
            }

            float importance = GetJobImportance(newJob.def);
            memoryComp.AddMemory(content, MemoryType.Action, importance);
        }

        private static bool IsSignificantJob(JobDef jobDef)
        {
            // Skip trivial jobs (Bug 4)
            if (jobDef == JobDefOf.Goto) return false;
            if (jobDef == JobDefOf.Wait) return false;
            if (jobDef == JobDefOf.Wait_Downed) return false;
            if (jobDef == JobDefOf.Wait_Combat) return false;
            if (jobDef == JobDefOf.GotoWander) return false;
            if (jobDef == JobDefOf.Wait_Wander) return false;
            
            // Only filter wandering jobs, not all jobs containing "Wander"
            if (jobDef.defName == "GotoWander") return false;
            if (jobDef.defName == "Wait_Wander") return false;
            
            // Only filter standing/waiting jobs, not working jobs
            if (jobDef.defName == "Wait_Stand") return false;
            if (jobDef.defName == "Wait_SafeTemperature") return false;
            if (jobDef.defName == "Wait_MaintainPosture") return false;

            return true;
        }

        private static float GetJobImportance(JobDef jobDef)
        {
            // Combat and social jobs are more important
            if (jobDef == JobDefOf.AttackMelee) return 0.9f;
            if (jobDef == JobDefOf.AttackStatic) return 0.9f;
            if (jobDef == JobDefOf.SocialFight) return 0.85f;
            if (jobDef == JobDefOf.MarryAdjacentPawn) return 1.0f;
            if (jobDef == JobDefOf.SpectateCeremony) return 0.7f;
            if (jobDef == JobDefOf.Lovin) return 0.95f;

            // Work jobs are moderate importance
            return 0.5f;
        }
    }
}
