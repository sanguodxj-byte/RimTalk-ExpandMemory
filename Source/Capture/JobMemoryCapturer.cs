using RimTalk.MemoryPatch;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimTalk.Memory.Capture
{

    public class JobMemoryCapturer
    {
        // 全局预设的工作类型分类
        // 不参与记忆捕获的垃圾工作（如发呆、走路等）
        private static readonly HashSet<JobDef> _jobsToIgnore = new();
        // 可模糊聚合工作-描述字典
        // 命中字典的 job 允许在 target 等要素不同时仍然聚合
        private static readonly Dictionary<JobDef, string> _dictAggregatedJobToDesc = new();
        // 特定工作-重要性字典
        private static readonly Dictionary<JobDef, float> _dictJobToImportance = new();

        // 配置项
        // 超时时间（两小时）
        private const int SessionTimeoutTicks = 2 * GenDate.TicksPerHour;
        // 单次会话最长时间（12小时）
        private const int SessionMaxDurationTicks = 12 * GenDate.TicksPerHour;

        // 静态构造函数
        static JobMemoryCapturer()
        {
            // 忽略集初始化，精确匹配规则
            _jobsToIgnore.UnionWith([
                JobDefOf.Goto, JobDefOf.GotoWander,
                JobDefOf.Wait, JobDefOf.Wait_Wander, JobDefOf.Wait_Combat, JobDefOf.Wait_MaintainPosture,
                JobDefOf.Wait_Asleep, JobDefOf.Wait_AsleepDormancy, JobDefOf.Wait_WithSleeping,
                JobDefOf.LayDown, JobDefOf.LayDownAwake, JobDefOf.LayDownResting
                ]);
            _jobsToIgnore.Remove(null);

            // 特定工作-重要性字典初始化，精确匹配规则
            static void AddToDict(JobDef jobDef, float importance)
            {
                if (jobDef is null) return;
                _dictJobToImportance[jobDef] = importance;
            }

            AddToDict(JobDefOf.AttackMelee, 0.9f);
            AddToDict(JobDefOf.AttackStatic, 0.9f);
            AddToDict(JobDefOf.SocialFight, 0.85f);

            AddToDict(JobDefOf.MarryAdjacentPawn, 1.0f);
            AddToDict(JobDefOf.SpectateCeremony, 0.7f);

            AddToDict(JobDefOf.Lovin, 0.6f);

            // 可模糊聚合工作-描述字典初始化，模糊匹配规则
            foreach (var jobDef in DefDatabase<JobDef>.AllDefsListForReading)
            {
                if (jobDef is null) continue;

                string defName = jobDef.defName;
                string desc = string.Empty;

                desc = defName switch
                {
                    _ when defName.Contains("Haul") => "搬运",
                    _ when defName.Contains("Harvest") => "收获",
                    _ when defName.Contains("CutPlant") => "割除",
                    _ when defName.Contains("Mine") => "采矿",
                    _ when defName.Contains("Repair") => "修理",
                    _ when defName.Contains("Milk") => "挤奶",
                    _ when defName.Contains("Shear") => "剪毛",

                    _ when defName.Contains("Deconstruct")
                    || defName.Contains("RemoveFloor")
                    || defName.Contains("RemoveRoof")
                    || defName.Contains("Uninstall")
                    => "拆除",

                    _ when defName.Contains("Frame")
                    || defName.Contains("BuildRoof")
                    || defName.Contains("Smooth")
                    => "建造",

                    _ when defName.Contains("Sow")
                    || defName.Contains("Replant")
                    || defName.Contains("PlantSeed")
                    => "种植",

                    _ when defName.Contains("Clean")
                    || defName.Contains("Clear")
                    => "清洁",

                    _ => desc
                };

                if (string.IsNullOrEmpty(desc)) continue;

                _dictAggregatedJobToDesc[jobDef] = desc;
            }
        }

        // --- 外部调用接口 ---
        /// <summary>
        /// 信息提取入口
        /// </summary>
        public static void ExtractJobInfoEnter(Job job, Pawn pawn)
        {
            if (// job 不过关时提前返回，跳过 GetComp
                job?.def is not { } jobDef
                || _jobsToIgnore.Contains(jobDef)

                // 仅殖民者且启用工作记忆捕捉时才激活 capturer
                || pawn is null
                || !pawn.IsColonist
                || !RimTalkMemoryPatchMod.Settings.enableActionMemory)

                return;

            // 获取组件实例并传入新启动的工作
            // 这里不检定 job 是否真的“进行”（显而易见的，我们无法预知未来这个 job 是否会被执行），这会导致一定的性能浪费
            // 不过考虑到浪费只会出现在暂停时，此时无 tick 开销，浪费就浪费，蹬就完事了
            pawn.GetComp<FourLayerMemoryComp>()?.JobCapturer?.ExtractJobInfo(job);
        }

        /// <summary>
        /// 工作记忆捕获入口
        /// </summary>
        public static void BuildJobMemoryEnter(Job job, Pawn pawn)
        {
            if (// job 不过关时提前返回，跳过 GetComp
                job?.def is not { } jobDef
                || _jobsToIgnore.Contains(jobDef)

                // build 时额外判断 job 在时间尺度上是否真的“进行”了
                || Find.TickManager?.TicksGame is not { } currentTick 
                || currentTick == job.startTick

                // 仅殖民者且启用工作记忆捕捉时才激活 capturer
                || pawn is null
                || !pawn.IsColonist
                || !RimTalkMemoryPatchMod.Settings.enableActionMemory)

                return;

            // 获取组件实例并传入即将完成的工作
            pawn.GetComp<FourLayerMemoryComp>()?.JobCapturer?.BuildJobMemory(job);
        }


        // --- 实例成员 ---
        // 父组件（记忆组件）的引用
        // 不可能为空，若为空则放任后续逻辑崩溃，以暴露问题
        private readonly FourLayerMemoryComp _memoryComp;

        // 组件持有者
        // 同上，不可能为空
        private ThingWithComps Parent => _memoryComp.parent;

        // 状态数据
        private int _startGameTick;
        private int _lastActiveTick;
        private int _repeatCount;
        private HashSet<string> _targetNames; // 使用 HashSet，牺牲一定遍历性能，换取自动去重
        // 懒加载，未激活的 capture 不会有无效占用
        private HashSet<string> TargetNames
        {
            get
            {
                _targetNames ??= new();
                return _targetNames;
            }
        }

        // 最新一次工作记忆
        private MemoryEntry _lastJobMemory;
        private string _lastJobReport;
        private string _lastJobAggregateDesc;

        // StartJob 时提取的、CleanupCurrentJob 时可能丢失的信息
        // 由 job 对象“领导”，但双 hook 严格时序下，理论上多余，即使在大量 mod 的环境下，时序也几乎不可能被破坏
        // 不过考虑到一旦被破坏的严重后果（张冠李戴），此处暂时还是选择保留
        private System.WeakReference<Job> _curJobWeakRef;
        // 懒加载，未激活的 capture 不会有无效占用
        private Job CurJob
        {
            get
            {
                _curJobWeakRef ??= new(null);
                _curJobWeakRef.TryGetTarget(out var job);
                return job;
            }
            set
            {
                _curJobWeakRef ??= new(null);
                _curJobWeakRef.SetTarget(value);
            }
        }
        private string _curJobReport;
        private string _curJobTargetAName;

        // 实例构造函数
        public JobMemoryCapturer(FourLayerMemoryComp memoryComp)
        {
            _memoryComp = memoryComp;
        }

        /// <summary>
        /// 信息提取实例工作区
        /// </summary>
        private void ExtractJobInfo(Job job)
        {
            CurJob = job;
            _curJobReport = GetJobReport(job);
            // 仅针对可模糊聚合的工作进行目标提取，节约开销
            _curJobTargetAName = _dictAggregatedJobToDesc.ContainsKey(job.def) ? GetTargetAName(job) : null;
        }

        /// <summary>
        /// 记忆构建实例工作区
        /// </summary>
        private void BuildJobMemory(Job job)
        {
            // 公共条件：两次工作完成的间隔在容许范围内，且上一条记忆存在
            // 这些条件大部分时候为 true，故在多“且”判断中会往后放，以期直接短路跳过
            bool? sharedConditionCache = null;
            bool sharedCondition() => sharedConditionCache ??=
                Find.TickManager.TicksGame - _lastActiveTick <= SessionTimeoutTicks
                && Find.TickManager.TicksGame - _startGameTick <= SessionMaxDurationTicks
                && _lastJobMemory is not null;

            // 获取报告是一个潜在的重操作，仅在需要时才执行，并且总是只执行一次
            string report = null;
            string getReport() => report ??= GetJobReportCached(job);

            // DRY
            void updateMemoryBase()
            {
                _lastJobMemory.GameTick = Find.TickManager.TicksGame;
                _lastJobMemory.Importance += ImportanceIncrement();
            }

            // --- 管线分流 ---
            // 精确命中，优先进入精确合并管线
            // 精确合并管线无法处理复数目标，此时（尝试）下放给模糊聚合管线处理
            if (TargetNames.Count <= 1 && getReport() == _lastJobReport && sharedCondition())
            {
                // --- 工作记忆精确合并管线 ---
                updateMemoryBase();
                // 需先更新 Session 再生成文本，避免内容滞后
                UpdateSession(); // 精确命中时无需获取和传入 targetName
                _lastJobMemory.Content = BuildExactContent();

                return;
            }

            // 模糊命中，进入模糊聚合管线（白名单补充管线）
            var jobDef = job.def;
            var ableToAggregate = _dictAggregatedJobToDesc.TryGetValue(jobDef, out string jobAggregateDesc);

            if (ableToAggregate
                && jobAggregateDesc == _lastJobAggregateDesc
                && sharedCondition())
            {
                // --- 工作记忆模糊聚合管线 ---
                updateMemoryBase();
                // 需先更新 Session 再生成文本，避免内容滞后
                UpdateSession(GetTargetANameCached(job));
                _lastJobMemory.Content = BuildFuzzyContent();

                return;
            }

            // 未命中任何聚合情况，进入新建管线

            // --- 工作记忆新建管线 ---
            // 创建记忆条目并添加到记忆组件
            string newReport = getReport();

            var newMemory = new MemoryEntry(
                newReport,
                MemoryType.Action,
                MemoryLayer.Active,
                importance: _dictJobToImportance.TryGetValue(jobDef, out float value) ? value : 0.5f
                );

            _memoryComp.ActiveMemories.Add(newMemory);

            // 创建新会话
            StartNewSession(
                newMemory,
                newReport,
                jobAggregateDesc,
                ableToAggregate ? GetTargetANameCached(job) : null // 仅在传入 job 可模糊聚合时提取目标
                );
        }

        // 创建新会话
        // targetName 可选，若提供则添加到目标名称集合中
        private void StartNewSession(MemoryEntry newMemory, string report, string jobAggregateDesc, string targetName = null)
        {
            // 初始化会话数据
            _startGameTick = _lastActiveTick = Find.TickManager.TicksGame;
            _repeatCount = 1;
            TargetNames.Clear();

            // 尝试添加目标名称
            if (!string.IsNullOrEmpty(targetName))
                TargetNames.Add(targetName);

            // 更新最新工作记忆数据
            _lastJobMemory = newMemory;
            _lastJobReport = report;
            _lastJobAggregateDesc = jobAggregateDesc;

            // 考虑手动清空 curJob 系列数据？
        }

        // 更新会话
        // targetName 可选，若提供则添加到目标名称集合中
        private void UpdateSession(string targetName = null)
        {
            _lastActiveTick = Find.TickManager.TicksGame;
            _repeatCount++;

            if (!string.IsNullOrEmpty(targetName))
                TargetNames.Add(targetName);
        }

        // 获取工作报告文本
        private string GetJobReportCached(Job job)
        {
            // 先尝试获取提前提取好的可靠文本
            // 从双 hook 的时序规律出发，传入的 job 总会等于提前提取的 job，故理论上前半判断多余
            if (CurJob == job && !string.IsNullOrEmpty(_curJobReport))
                return _curJobReport;

            // 获取失败时，重新提取
            return GetJobReport(job);
        }

        // 提取并简单处理工作报告文本
        private string GetJobReport(Job job)
        {
            if (Parent is not Pawn parentPawn) return string.Empty;

            // 提取报告
            var jobReport = job.GetReport(parentPawn);

            // 文本无效时跳过后处理，提前返回
            if (string.IsNullOrEmpty(jobReport)) return string.Empty;

            // 后处理
            if (jobReport.StartsWith("正在"))
            {
                jobReport = jobReport.Substring(2);
            }

            return jobReport;
        }

        // 获取 job 的目标，只针对 targetA
        // 部分 job，如喂食，的语义目标可能为 targetB，需注意
        private string GetTargetANameCached(Job job)
        {
            // 先尝试获取提前提取好的可靠文本
            // 从双 hook 的时序规律出发，传入的 job 总会等于提前提取的 job，故理论上前半判断多余
            if (CurJob == job && !string.IsNullOrEmpty(_curJobTargetAName))
                return _curJobTargetAName;

            // 获取失败时，重新提取
            return GetTargetAName(job);
        }

        // 提取 job 的 targetA 的名称
        private string GetTargetAName(Job job)
        {
            if (!job.targetA.HasThing) return string.Empty;

            var targetThing = job.targetA.Thing;

            // 提取目标名称
            // 目标即自身时
            if (targetThing == Parent) return "自己";

            // 目标是蓝图或框架，则提取其正在建造的实体名称（如木材、钢铁等）
            if (targetThing is Blueprint or Frame)
            {
                return targetThing.def?.entityDefToBuild?.label ?? string.Empty;
            }

            return targetThing.LabelShort ?? targetThing.def?.label ?? string.Empty;
        }

        // 生成合并/聚合记忆更新文本
        private string BuildExactContent() => $"{GetDurationDesc()}{_repeatCount}次{_lastJobReport}";
        private string BuildFuzzyContent() =>
            $"{GetDurationDesc()}{_lastJobAggregateDesc}了{_repeatCount}次{string.Join("、", TargetNames.Take(3))}{(TargetNames.Count > 3 ? "等" : "")}。";

        // 获取耗时描述
        private string GetDurationDesc()
        {
            return (Find.TickManager.TicksGame - _startGameTick) switch
            {
                <= GenDate.TicksPerHour => "连续",
                <= 2 * GenDate.TicksPerHour => "两小时内",
                <= 4 * GenDate.TicksPerHour => "花费几小时",
                <= 8 * GenDate.TicksPerHour => "花费小半天",
                <= 12 * GenDate.TicksPerHour => "花费大半天",
                _ => "花费一整天" // 此分支理论上不触发
            };
        }

        // 计算重要性增量
        private float ImportanceIncrement()
        {
            // 每小时增长0.02
            float increment = (Find.TickManager.TicksGame - _lastActiveTick) * (0.02f / GenDate.TicksPerHour);

            // 前20次每次增长0.01，之后不再增长
            if (_repeatCount <= 20) increment += 0.01f;

            return increment;
        }
    }

}