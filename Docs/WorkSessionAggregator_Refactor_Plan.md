# JobMemoryCapturer 重构纪要：流式追加(Append)与事件驱动架构

## 1. 核心思想与重构目标

在早期的设计中，动作记忆捕获采用了“定时心跳结算(Tick + Polling)”机制。经过架构审视，我们成功向更先进的**“流式追加 (Append / Event-Driven)”**模型演进，并完成了底层代码的全面现代化重构。

**核心变革：**
1. **彻底废弃心跳 (Zero-Tick)**：完全剥离了原先对于 `CompTick` 的依赖。所有的状态流转与记忆更新现在完全由 `BuildJobMemory` 这一事件驱动（Hook 在小人执行 `StartJob` 时），实现了真正的零后台轮询性能开销。
2. **免疫死亡/离场问题**：由于记忆是实时生成并流式更新的，小人意外死亡或离开地图时，系统内存中握持的已然是最新的记忆状态，不再需要依靠心跳来做兜底强行结算。
3. **架构降级与解耦**：将 `JobMemoryCapturer` 从沉重的 `ThingComp` 降级为纯净的 C# 对象（POCO），作为核心层 `FourLayerMemoryComp` 的内部专属模块存在，实现了零组件开销与零注入负担。

---

## 2. 架构设计与实现细节

### A. 架构降级 (ThingComp -> POCO 模块)
`JobMemoryCapturer` 已不再继承 `ThingComp`，也随之移除了 `CompProperties_JobMemoryCapturer` 以及繁琐的注入补丁。它现在直接被 `FourLayerMemoryComp` 所持有：
```csharp
public class FourLayerMemoryComp : ThingComp
{
    private readonly JobMemoryCapturer _jobCapturer;
    public JobMemoryCapturer JobCapturer => _jobCapturer;
    
    public FourLayerMemoryComp() {
        _jobCapturer = new JobMemoryCapturer(this);
    }
}
```
这种设计明确了生命周期的从属关系，并且使得 `JobMemoryCapturer` 可以直接调用其父级对象来存入生成的记忆。

### B. 数据驱动的字典聚合优化
为了消除在代码流中存在的大量 `if-else` 推导，我们引入了 `_dictAggregatedJobToDesc`：
- 在静态构造函数中，通过 C# 9.0+ 的 `switch` 模式匹配一次性初始化完毕（已包含如“搬运”、“采矿”、“调查(StudyInteract)”、“扑灭(BeatFire/Extinguish)”等聚合关键字）。
- 这是一张聚合工作与其对应中文描述的映射表。这不仅承担了**校验该工作是否能够聚合**的任务，也直接提供了**对应的描述文本**，实现了纯数据驱动，避免了代码膨胀。

### C. 大一统状态机 (Unified State Machine) 与防卡死设计
所有的动作开始时都会进入 `BuildJobMemory(job)`，并经过一套严密的状态机判定。
系统不再预先分流单次动作和聚合动作，而是统一进行“是否能接续上一状态”的判定。必须同时满足以下条件才会进入“流式追加（Append）”，否则全部视为“中断并创建新会话”：
1. **工作一致**：当前 `job.def` 必须等于 `_lastJobDef`。
2. **允许聚合**：`_dictAggregatedJobToDesc.ContainsKey(job.def)` 必须为 `true`。
3. **未超时**：距离上一次活动的间隔不能超过 `SessionTimeoutTicks`（2 小时）。修复了小人跨日干活导致的幽灵聚合 Bug。
4. **未达最大时长**：总持续时间不能超过 `SessionMaxDurationTicks`（12 小时）。这是防止某些特殊 Mod 导致死循环或动作时间过长而掩盖记忆细节的最后防线。

### D. 同帧绝对撤销机制 (Same-Tick Overwrite & Cancel)
为了解决在游戏暂停状态下玩家频繁下达、取消指令导致的**大量无意义瞬间 Job 被记录（幽灵指令）**的问题，在进入管线前加入了绝对拦截：
- 当捕获到新 Job，且其 `currentTick` 恰好等于当前持有记忆的 `_startGameTick`，说明上一个记忆**连一帧都没来得及执行**就被打断了。
- 系统会直接从底层记忆库中 `Remove` 掉该幽灵记忆。
- 如果新 Job 是垃圾工作（如重新征召），则相当于实现了完美的撤销功能；如果新 Job 是有效指令，则会重新走新建分支。这就使得最终只会有解除暂停前保留的**最后一个有效指令**被录入。

### E. 内存引用的就地覆写与增量权重
- **就地覆写**：当状态机进入“流式追加”时，系统利用自身持有的 `_lastJobMemory` 引用，直接修改其 `Content` 属性（调用 `GenerateAggregatedContent()` 生成），没有任何额外的组件查询或序列化成本。
- **权重安全封装**：`Importance` 属性的累加抛弃了原有的硬编码覆盖方式，转而使用 `ImportanceIncrement()` 计算时间增量进行累加。同时在底层的 `MemoryEntry` 实体类中，将 `Importance` 属性设置了 `Math.Clamp(value, 0f, 1f)`，从物理层面上隔绝了溢出越界的可能。

### F. 智能目标提取与文本清理
- 废弃了原本笨重的正则表达式（不再尝试匹配 `TargetA` 等无用字符）。直接依赖现代 C# 语法进行提取，并使用 `HashSet<string> _targetNames` 实现自动去重：
```csharp
if (targetThing is Blueprint or Frame) {
    targetName = targetThing.def?.entityDefToBuild?.label;
} else {
    targetName = targetThing.LabelShort ?? targetThing.def?.label;
}
```
- 新建记忆时，调用 `job.GetReport(pawn)` 后，如果文本以“正在”开头，则通过 `.Substring(2).TrimStart()` 将其切除（例如将“正在做礼拜”清洗为更整洁的“做礼拜”）。

---

## 3. 管线架构图 (ASCII Pipeline)

```text
========================================================================================
    四层记忆系统 (FourLayerMemoryComp) -> 子模块 [JobMemoryCapturer (POCO)] 
========================================================================================

   [外部系统/Patch: 小人开始工作 (JobStartMemoryPatch)]
                |
   ___pawn.GetComp<FourLayerMemoryComp>()?.JobCapturer?.BuildJobMemory(Job)
                |
                v
  [JobMemoryCapturer 内部逻辑开始]
                |
       (同帧撤销拦截) 是否 currentTick == _startGameTick ?
                |--> [True]: 直接从库中 Remove(_lastJobMemory)，将其置空。
                |
                v
       1. 全局拦截垃圾工作 (_jobsToIgnore)
                |
                v
       状态机判定：是否是同类工作 
       且 允许聚合(_dictAggregatedJobToDesc)
       且 未超时(2h) 且 未达到最大时长(12h) ?
                |
   +------------+-------------------------------------------------+
   | (True: 连续同类聚合工作)                                     | (False: 异类工作、已超时或属于单次重要工作)
   v                                                              v
 [流式追加分支 (Append Pipeline)]                         [全新建档分支 (New Session Pipeline)]
   |                                                              |
   |-- 1. 尝试提取并添加新的 TargetName 记录                      |-- 1. StartNewSession() (重置捕获器状态为当前新动作)
   |-- 2. UpdateSession() (状态累加)                              |
   |                                                              v
   v                                                              |-- 2. 使用 job.GetReport() 生成原汁原味的单次文本
   |-- 3. GenerateAggregatedContent() (生成如"几小时内搬运了N次") |-- 3. 截除开头的“正在”字样，提高文本整洁度
   |-- 4. ImportanceIncrement() (重要性增量计算)                  |-- 4. 实例化全新的 MemoryEntry 并移交维护层 ActiveMemories
   |                                                              `-- 5. 强引用锚定 _lastJobMemory = newMemory
   |-- 5. 基于手里握着的 _lastJobMemory 引用进行直接内存覆写：    
   |        - _lastJobMemory.Content = newText                 
   |        - _lastJobMemory.Importance += increment              v
   |        - _lastJobMemory.GameTick = Now                    (结束)
   |
   v
 (直接完成对底层全局记忆库的同步刷新，零引擎层面调用)
```

---

## 4. JobStartMemoryPatch 的最终形态
在重构完成后，原本臃肿的 `JobStartMemoryPatch` 彻底被解放，现在只负责纯粹的条件放行，将原始的 Job 一脚踢给专属捕获器：

```csharp
[HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
public static class Pawn_JobTracker_StartJob_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Job job, Pawn ___pawn)
    {
        // 仅殖民者且启用工作记忆捕捉时才激活 capturer
        if (___pawn is null || !___pawn.IsColonist || !RimTalkMemoryPatchMod.Settings.enableActionMemory)
            return;

        ___pawn.GetComp<FourLayerMemoryComp>()?.JobCapturer?.BuildJobMemory(job);
    }
}
```