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

            // ⭐ 方案1：智能占位符检测与替换
            string content = BuildJobDescription(newJob, pawn);

            float importance = GetJobImportance(newJob.def);
            memoryComp.AddMemory(content, MemoryType.Action, importance);
        }
        
        /// <summary>
        /// ⭐ 构建工作描述（智能占位符替换）
        /// </summary>
        private static string BuildJobDescription(Job job, Pawn pawn)
        {
            // 1. 先尝试使用 GetReport()（处理标准占位符）
            string content = job.GetReport(pawn);
            
            // 2. 检测是否包含任何形式的占位符
            if (ContainsPlaceholder(content))
            {
                // 3. 获取目标物体的真实名称
                string targetName = GetTargetName(job, pawn);
                
                // 4. 如果获取到有效的目标名称，替换所有占位符
                if (!string.IsNullOrEmpty(targetName))
                {
                    content = ReplaceAllPlaceholders(content, targetName);
                }
            }
            
            return content;
        }
        
        /// <summary>
        /// ⭐ 检测字符串是否包含占位符
        /// </summary>
        private static bool ContainsPlaceholder(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            // 检测各种占位符格式
            var placeholderPatterns = new[]
            {
                @"\{0\}",                    // {0}
                @"\{1\}",                    // {1}
                @"\{2\}",                    // {2}
                @"TargetA",                  // TargetA
                @"TargetB",                  // TargetB
                @"TargetC",                  // TargetC
                @"\{TargetA\}",              // {TargetA}
                @"\{TargetB\}",              // {TargetB}
                @"\{TARGETLABEL\}",          // {TARGETLABEL}
                @"\{TARGET_LABEL\}",         // {TARGET_LABEL}
            };
            
            foreach (var pattern in placeholderPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// ⭐ 替换所有可能的占位符格式
        /// </summary>
        private static string ReplaceAllPlaceholders(string text, string replacement)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(replacement))
                return text;
            
            // 按优先级替换（从最具体到最通用）
            var replacements = new[]
            {
                ("{TargetA}", replacement),
                ("{TargetB}", replacement),
                ("{TargetC}", replacement),
                ("{TARGETLABEL}", replacement),
                ("{TARGET_LABEL}", replacement),
                ("TargetA", replacement),
                ("TargetB", replacement),
                ("TargetC", replacement),
                ("{0}", replacement),
                ("{1}", replacement),
                ("{2}", replacement),
            };
            
            string result = text;
            foreach (var (placeholder, value) in replacements)
            {
                result = result.Replace(placeholder, value);
            }
            
            return result;
        }
        
        /// <summary>
        /// ⭐ 统一获取目标名称的逻辑
        /// </summary>
        private static string GetTargetName(Job job, Pawn pawn)
        {
            // 检查 targetA
            if (job.targetA.HasThing && job.targetA.Thing != pawn)
            {
                Thing targetThing = job.targetA.Thing;
                string targetName = "";
                
                // 特殊处理：蓝图
                if (targetThing is Blueprint blueprint)
                {
                    var entityDef = blueprint.def.entityDefToBuild;
                    if (entityDef != null && !string.IsNullOrEmpty(entityDef.label))
                    {
                        targetName = entityDef.label;
                    }
                }
                // 特殊处理：框架
                else if (targetThing is Frame frame)
                {
                    var entityDef = frame.def.entityDefToBuild;
                    if (entityDef != null && !string.IsNullOrEmpty(entityDef.label))
                    {
                        targetName = entityDef.label;
                    }
                }
                // 通用处理：使用 LabelShort
                else
                {
                    targetName = targetThing.LabelShort ?? targetThing.def?.label ?? "";
                }
                
                // 验证名称有效性
                if (IsValidTargetName(targetName))
                {
                    return targetName;
                }
            }
            
            // 如果 targetA 无效，尝试 targetB
            if (job.targetB.HasThing && job.targetB.Thing != pawn)
            {
                Thing targetThing = job.targetB.Thing;
                string targetName = targetThing.LabelShort ?? targetThing.def?.label ?? "";
                
                if (IsValidTargetName(targetName))
                {
                    return targetName;
                }
            }
            
            return "";
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
