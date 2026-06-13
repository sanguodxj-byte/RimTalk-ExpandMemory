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
        // 待聚合工作-描述字典
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
                JobDefOf.Wait_Asleep, JobDefOf.Wait_AsleepDormancy, JobDefOf.Wait_WithSleeping
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

            // 待聚合工作-描述字典初始化，模糊匹配规则
            foreach (var jobDef in DefDatabase<JobDef>.AllDefsListForReading)
            {
                if (jobDef is null) continue;

                string defName = jobDef.defName;
                string desc = string.Empty;

                desc = defName switch
                {
                    _ when defName.Contains("Haul") => "搬运",
                    _ when defName.Contains("Deconstruct") => "拆除",
                    _ when defName.Contains("Plant") => "种植",
                    _ when defName.Contains("Harvest") => "收获",
                    _ when defName.Contains("Mine") => "采矿",
                    _ when defName.Contains("Clean") => "清洁",
                    _ when defName.Contains("Repair") => "修理",
                    _ => desc
                };

                if (string.IsNullOrEmpty(desc)) continue;

                _dictAggregatedJobToDesc[jobDef] = desc;
            }
        }


        // 实例成员
        // --- 状态数据 ---
        private MemoryEntry _lastJobMemory;
        private JobDef _lastJobDef;
        private int _startGameTick;
        private int _lastActiveTick;
        private int _repeatCount;
        private readonly HashSet<string> _targetNames = new(); // 使用 HashSet，牺牲一定遍历性能，换取自动去重

        // 父组件（记忆组件）的引用
        // 不可能为空，若为空则放任后续逻辑崩溃，以暴露问题
        private FourLayerMemoryComp _memoryComp;

        // 组件持有者
        // 同上，不可能为空
        private ThingWithComps parent => _memoryComp.parent;

        // 实例构造函数
        public JobMemoryCapturer(FourLayerMemoryComp memoryComp)
        {
            _memoryComp = memoryComp;
        }


        // --- 外部调用接口 ---
        /// <summary>
        /// 工作记忆捕获入口
        /// </summary>
        public void BuildJobMemory(Job job)
        {
            if (job?.def is not { } jobDef || Find.TickManager is null) return;

            int currentTick = Find.TickManager.TicksGame;

            // 如果当前游戏刻正好是上一个会话的开始刻，说明上一个工作根本没来得及执行，此时将移除对应的无效工作记忆，并清空会话
            if (_startGameTick == currentTick && _lastJobMemory is not null)
            {
                // 此处会执行 O(N)，但考虑到本分支总出现在暂停时，故性能开销可以接受
                _memoryComp.ActiveMemories.Remove(_lastJobMemory);
                _lastJobMemory = null; // 置空后，不论下方是什么逻辑，都会被当作全新的状态处理
            }

            if (_jobsToIgnore.Contains(jobDef))
                return;

            // 管线分流
            if (jobDef != _lastJobDef
                || currentTick - _lastActiveTick > SessionTimeoutTicks
                || currentTick - _startGameTick > SessionMaxDurationTicks
                || !_dictAggregatedJobToDesc.ContainsKey(jobDef)
                || _lastJobMemory is null)
            {
                // --- 工作记忆新建管线 ---
                if (parent is not Pawn parentPawn) return; // 此判断在实际应用场景上为多余

                // 获取并简单处理工作报告文本
                string content = job.GetReport(parentPawn);
                if (content.StartsWith("正在"))
                {
                    content = content.Substring(2);
                }

                // 创建记忆条目并添加到记忆组件
                var newMemory = new MemoryEntry(
                    content,
                    MemoryType.Action,
                    MemoryLayer.Active,
                    importance: _dictJobToImportance.TryGetValue(jobDef, out float value) ? value : 0.5f
                    );

                _memoryComp.ActiveMemories.Add(newMemory);

                // 重置会话
                StartNewSession(newMemory, job);
            }
            else
            {
                // --- 工作记忆聚合管线 ---
                _lastJobMemory.GameTick = currentTick;
                _lastJobMemory.Importance += ImportanceIncrement();

                // 需先更新相关状态，再生成聚合文本，避免内容滞后
                UpdateSession(job);
                _lastJobMemory.Content = BuildAggregatedContent();
            }
        }

        // 重置并创建新聚合会话
        private void StartNewSession(MemoryEntry newMemory, Job job)
        {
            var jobDef = job.def;

            _lastJobMemory = newMemory;
            _lastJobDef = jobDef;
            _startGameTick = _lastActiveTick = Find.TickManager.TicksGame;
            _repeatCount = 1;

            _targetNames.Clear();
            // 考虑到字符串操作有额外的 GC 开销，此处额外进行一次 contain 判断，仅针对待聚合工作进行目标提取
            if (_dictAggregatedJobToDesc.ContainsKey(jobDef)) TryAddTargetName(job);
        }

        // 更新聚合会话状态
        private void UpdateSession(Job job)
        {
            _lastActiveTick = Find.TickManager.TicksGame;
            _repeatCount++;
            TryAddTargetName(job);
        }

        // 提取并添加 job 的目标，暂时只提取 targetA
        // 部分 job，如喂食，的语义目标可能为 targetB，需注意
        private bool TryAddTargetName(Job job)
        {
            if (!job.targetA.HasThing) return false;

            var targetThing = job.targetA.Thing;

            // 目标即自身时不提取
            // 暂定，或将视实际效果更改
            if (targetThing == parent) return false;

            // 提取目标名称
            // 特别的，如果目标是蓝图或框架，则提取其正在建造的实体名称（如木材、钢铁等）
            string targetName;
            if (targetThing is Blueprint or Frame)
            {
                targetName = targetThing.def?.entityDefToBuild?.label;
            }
            else
            {
                targetName = targetThing.LabelShort ?? targetThing.def?.label;
            }
            // 排空
            if (string.IsNullOrEmpty(targetName)) return false;

            // 添加到目标名称集合
            _targetNames.Add(targetName);
            return true;
        }

        // 生成聚合记忆更新文本
        private string BuildAggregatedContent()
        {
            // 耗时描述
            string durationDesc = (Find.TickManager.TicksGame - _startGameTick) switch
            {
                <= GenDate.TicksPerHour => "一小时内",
                <= 2 * GenDate.TicksPerHour => "两小时内",
                <= 4 * GenDate.TicksPerHour => "花费几小时",
                <= 8 * GenDate.TicksPerHour => "花费小半天",
                <= 12 * GenDate.TicksPerHour => "花费大半天",
                _ => "花费一整天" // 此分支理论上不触发
            };

            // 动作描述
            _dictAggregatedJobToDesc.TryGetValue(_lastJobDef, out string jobDesc);

            // 目标描述
            string targetDesc = $"{string.Join("、", _targetNames.Take(3))}{(_targetNames.Count > 3 ? "等" : "")}";

            // 组装并返回
            return $"{durationDesc}{jobDesc}了{_repeatCount}次{targetDesc}";
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