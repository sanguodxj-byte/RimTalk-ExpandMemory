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

            // === 工作会话聚合 ===
            // 先让聚合器处理这个Job
            WorkSessionAggregator.OnJobStarted(pawn, newJob.def, newJob.targetA.Thing);
            
            // 如果这个Job正在被聚合器追踪，则跳过单次记录
            // 避免生成重复的"搬运 - 木材"记忆
            if (WorkSessionAggregator.IsJobBeingAggregated(newJob.def))
            {
                return; // 跳过，让聚合器处理
            }
            
            // Skip insignificant jobs (Bug 4: ignore wandering and standing)
            if (!IsSignificantJob(newJob.def))
                return;

            // ⭐ 修复：使用GetReport而不是reportString，自动替换占位符
            string content = newJob.GetReport(pawn);
            
            // 如果GetReport返回的内容仍然包含无意义的占位符，则尝试手动构建
            if (content.Contains("TargetA") || content.Contains("TargetB") || content.Contains("TargetC"))
            {
                // 回退到手动构建
                content = newJob.def.reportString;
                
                // 尝试替换占位符
                if (newJob.targetA.HasThing && newJob.targetA.Thing != pawn)
                {
                    Thing targetThing = newJob.targetA.Thing;
                    string targetName = "";
                    
                    // 尝试获取有意义的名称
                    if (targetThing is Blueprint blueprint)
                    {
                        var entityDef = blueprint.def.entityDefToBuild;
                        if (entityDef != null && !string.IsNullOrEmpty(entityDef.label))
                        {
                            targetName = entityDef.label;
                        }
                    }
                    else if (targetThing is Frame frame)
                    {
                        var entityDef = frame.def.entityDefToBuild;
                        if (entityDef != null && !string.IsNullOrEmpty(entityDef.label))
                        {
                            targetName = entityDef.label;
                        }
                    }
                    else
                    {
                        // 其他类型的目标，使用LabelShort
                        targetName = targetThing.LabelShort ?? targetThing.def?.label ?? "";
                    }
                    
                    // 替换占位符
                    if (!string.IsNullOrEmpty(targetName) && IsValidTargetName(targetName))
                    {
                        // 替换常见的占位符格式
                        content = content.Replace("TargetA", targetName);
                        content = content.Replace("{0}", targetName);
                        content = content.Replace("{TargetA}", targetName);
                    }
                }
            }

            float importance = GetJobImportance(newJob.def);
            memoryComp.AddMemory(content, MemoryType.Action, importance);
        }
        
        /// <summary>
        /// 检查目标名称是否有效（过滤TargetA等无意义名称）
        /// </summary>
        private static bool IsValidTargetName(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
                return false;
            
            // 使用正则表达式过滤：
            // 1. Target[A-Z] 格式 (TargetA, TargetB, TargetC)
            if (System.Text.RegularExpressions.Regex.IsMatch(targetName, @"^Target[A-Z]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return false;
            
            // 2. Target开头的任何内容 (Target, TargetInfo, TargetCustom等)
            if (System.Text.RegularExpressions.Regex.IsMatch(targetName, @"^Target\w*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return false;
            
            // 3. 纯数字名称
            if (System.Text.RegularExpressions.Regex.IsMatch(targetName, @"^\d+$"))
                return false;
            
            // 4. 只包含空格或特殊字符
            if (System.Text.RegularExpressions.Regex.IsMatch(targetName, @"^[\s\W]+$"))
                return false;
            
            return true;
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
