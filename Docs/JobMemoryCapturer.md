# JobMemoryCapturer 架构纪要：双 Hook 快照式事件驱动

## 1. 语义修正：记忆记录"已发生"而非"即将发生"

本次重构的核心是修正捕获语义本身：**记忆应记录已经发生的事，而非即将发生的事**。

为此，捕获时机从 `StartJob`（工作开始）改为 `CleanupCurrentJob`（工作结束）——只在一份工作真正执行完毕后才将其录入记忆。`StartJob`(Postfix) 退化为纯粹的**信息预提取**时机：在工作开始、目标 Thing 必定存活时提取 report/targetName 入缓存，供 `CleanupCurrentJob`(Prefix) 消费，顺带规避了目标 Discarded 时 `GetReport` 降级的旧症。

旧架构在 `StartJob` 即生成记忆，语义上记录的是"已下达的意图"，由此衍生出一系列弊端：暂停微操/同刻撤销产生的幽灵记忆、被立即打断工作的瞬时记录、目标 Thing 后续被 Discard 导致报告退化等。这些问题在旧架构下曾各自被单独修补（如"先记录再 Remove 幽灵"），但本次重构并非针对其中任何一项——**修正语义之后，上述弊端自然消解**。同刻起止的工作因"未真正发生"而不被录入（见 §7），只是这一语义的自然推论，而非专门的清理机制。

---

## 2. 核心架构

- **双 Hook**：`StartJob`(Postfix) 预提取信息 → `CleanupCurrentJob`(Prefix) 消费缓存生成记忆。
- **POCO 化**：`JobMemoryCapturer` 不继承 `ThingComp`，由 `FourLayerMemoryComp` 直接持有；删除旧 `WorkSessionAggregator`（-500 行），消除组件注入开销。
- **零心跳**：完全剥离 `CompTick` / `WorldComponentTick` 依赖，状态流转由事件驱动，零后台轮询。
- **数据驱动**：`_jobsToIgnore` / `_dictAggregatedJobToDesc` / `_dictJobToImportance` 三个静态集合在静态构造函数中通过 `switch` 模式匹配一次性初始化，废弃 Regex 与 if-else 地狱，工作识别与目标提取 O(1)。
- **三分支管线**：精确合并 / 模糊聚合 / 新建，聚合分支就地覆写 `_lastJobMemory`，零分配。

---

## 3. Hook 时序与快照机制

### 双 Hook 工作流（新工作替换旧工作时）

```
Pawn_JobTracker.StartJob(newJob)
  ├─ CleanupCurrentJob(oldJob)            ← 清理旧工作
  │   └─ [Prefix] BuildJobMemoryEnter(oldJob)  → 消费缓存生成记忆
  ├─ curJob = newJob                      ← 切换
  └─ [Postfix] ExtractJobInfoEnter(newJob)     → 预提取信息入缓存
```

关键时序保证：旧工作的 `BuildJobMemory` 先于新工作的 `ExtractJobInfo` 执行——因为 `CleanupCurrentJob` 在 `curJob = newJob` 之前被调用。这保证了 `BuildJobMemory(old)` 读到的缓存仍是 old 的提取结果，不会被 new 覆写。

> 工作自然结束（非替换）时，`CleanupCurrentJob` 同样被调用，Prefix 照样触发；此时无新工作紧跟，缓存被 `BuildJobMemory` 消费后保持旧值，直至下一个 `StartJob` 的 Postfix 覆写。

### 快照缓存模式

```
ExtractJobInfo(job)                     BuildJobMemory(job)
  ├─ CurJob = job (WeakRef)             ├─ GetJobReportCached(job)
  ├─ _curJobReport    = GetReport(job)  │   → CurJob==job ? _curJobReport    : 降级提取
  └─ _curJobTargetAName = GetName(job)  └─ GetTargetANameCached(job)
                                          → CurJob==job ? _curJobTargetAName : 降级提取
```

`CurJob` 以 `WeakReference<Job>` 存储，工作周期内通过引用比对校验缓存有效性。降级路径在 Job 对象变更（或存档加载后首个工作）时触发，重新通过 `job.GetReport(parentPawn)` 直接提取——此时目标可能已 Discarded，报告文本会退化但不崩溃。

---

## 4. 闸门下沉

Patch 层归零为单行委托，所有检定集中到 `JobMemoryCapturer` 的静态 Enter 方法：

```csharp
// StartJob Postfix —— 信息预提取
[HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
public static class Pawn_JobTracker_StartJob_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Job ___curJob, Pawn ___pawn)
        => JobMemoryCapturer.ExtractJobInfoEnter(___curJob, ___pawn);
}

// CleanupCurrentJob Prefix —— 记忆生成
[HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
public static class Pawn_JobTracker_CleanupCurrentJob_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Job ___curJob, Pawn ___pawn)
        => JobMemoryCapturer.BuildJobMemoryEnter(___curJob, ___pawn);
}
```

闸门统一处理：job 有效性、忽略集匹配、colonist 身份、Settings 开关；`BuildJobMemoryEnter` 额外做同帧拦截（见 §7）。`ExtractJobInfoEnter` 不做同帧拦截——提取本身无害，零时长工作的缓存会被后续 `StartJob` 自然覆写，且 `BuildJobMemoryEnter` 自有同刻跳过兜底。

---

## 5. 三分支分流管线

```
BuildJobMemory(job)
  ├─ 【精确合并】  条件: TargetNames.Count ≤ 1 且 report 逐字相等 且 时间窗口内
  │   行为: 就地覆写 _lastJobMemory，不创建新对象
  │   例:  连续对同一块矿脉采矿 → "连续5次采矿"
  │
  ├─ 【模糊聚合】  条件: 白名单命中 且 聚合描述匹配 且 时间窗口内
  │   行为: 就地覆写 _lastJobMemory，追加新目标名到 TargetNames
  │   例:  搬运钢铁、大米、木头 → "两小时内搬运了3次钢铁、大米、木头等"
  │
  └─ 【新建】      条件: 前两条均不命中 (不同类工作/超时/首次记录)
      行为: 创建新 MemoryEntry，Add 到 ActiveMemories，重置会话
```

### 判定矩阵

| 条件 | 精确合并 | 模糊聚合 | 新建 |
|------|:---:|:---:|:---:|
| `TargetNames.Count ≤ 1` | **特有** | — | — |
| `report` 文本相等 | **特有** | — | — |
| 白名单命中 `ableToAggregate` | — | **特有** | — |
| `jobAggregateDesc == _lastJobAggregateDesc` | — | **特有** | — |
| `sharedCondition()` (时间窗口+记忆存在) | 末尾求值 | 末尾求值 | — |

### 亮点：无重复判断、无重复重操作

分流机制确保**整个 `BuildJobMemory` 调用中，任何重操作至多执行一次，任何共享判断至多求值一次**：

- `sharedCondition()`：`bool?` 缓存 + `??=` 单次求值，精确/模糊管线共享同一结果。
- `getReport()`：`string ??=` 单次求值，`GetReport`（潜在重操作）至多触发一次。
- `&&` 链中廉价检查前置：`TargetNames.Count` / `ableToAggregate` / 描述比对 等轻量判断在前，`getReport()` / `sharedCondition()` 等重操作仅在前面通过后才求值。
- 目标提取以白名单命中为前提（`ExtractJobInfo` 与 `StartNewSession` 中均如此），非聚合工作零目标提取开销。
- 新建管线以 `newReport = getReport()` 显式求值后复用于 `MemoryEntry` 构造与 `StartNewSession`，无二次提取。

---

## 6. 就地覆写与增量权重

聚合分支不创建新 `MemoryEntry`，直接修改 `_lastJobMemory`：

```csharp
void updateMemoryBase() {
    _lastJobMemory.GameTick = Find.TickManager.TicksGame;
    _lastJobMemory.Importance += ImportanceIncrement();
}
// 精确合并: _lastJobMemory.Content = BuildExactContent();
// 模糊聚合: _lastJobMemory.Content = BuildFuzzyContent();
```

零组件查询、零序列化、零内存分配。`MemoryEntry.Importance` 在 setter 中 `Math.Clamp(0, 1)` 物理层面防止溢出。

权重增量：每小时 +0.02，前 20 次每次额外 +0.01。

---

## 7. 同帧撤销（零时长跳过）

`BuildJobMemoryEnter` 中：若 `currentTick == job.startTick`，说明该 job 同刻起止、未真正执行，直接 `return` 跳过记录。

这是 §1 语义的自然推论——"未发生的事不记录"——而非专门的幽灵清理机制。旧架构因在 `StartJob` 即记录，零时长工作会先入库再被 `Remove` 清除；新架构下它们根本不入库，旧版的 Remove 逻辑随之废弃。

---

## 8. 智能目标提取

```csharp
if (targetThing == Parent) return "自己";                              // 自目标
if (targetThing is Blueprint or Frame)                                 // 蓝图/框架
    return targetThing.def?.entityDefToBuild?.label ?? string.Empty;
return targetThing.LabelShort ?? targetThing.def?.label ?? string.Empty;  // 普通
```

在 `StartJob` 时调用，目标 Thing 必定存活，无需 Discard 状态 hack。仅对白名单聚合工作提取，非聚合工作跳过以节约开销。`HashSet<string> TargetNames` 自动去重。

---

## 9. 管线架构图

```
======================== FourLayerMemoryComp → [JobMemoryCapturer (POCO)] ========================

  [Patch: StartJob Postfix]              [Patch: CleanupCurrentJob Prefix]
        │                                         │
        v                                         v
  ExtractJobInfoEnter(job, pawn)          BuildJobMemoryEnter(job, pawn)
        │ 闸门: job/def/ignore/                 │ 闸门: job/def/ignore/
        │       colonist/settings                │       startTick≠currentTick/
        │                                         │       colonist/settings
        v                                         v
  ExtractJobInfo(job)                     BuildJobMemory(job)
  (缓存: CurJob / report / targetAName)          │
                                         ┌───────┴───────┐
                                         v 精确合并       v 模糊聚合
                                   report== 且 单目标   白名单== 且 同desc
                                         │               │
                                         │ return        │ return
                                         └───────┬───────┘
                                                 v (都不命中)
                                            ┌──────────┐
                                            │ 新建     │
                                            │ new Mem  │
                                            │ Add+Sess │
                                            └──────────┘
```
