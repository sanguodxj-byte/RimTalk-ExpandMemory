# MemoryMaintainer 重构执行方案

## 1. 文档目的

本文定义第一阶段维护层重构的可执行方案：在 `FourLayerMemoryComp` 下新增与 `JobMemoryCapturer` 同级的 `MemoryMaintainer`，集中承接记忆的机械性维护与原子状态变更。

本阶段优先解决以下问题：

- 衰减、清理、容量治理和直接迁移散落在 `FourLayerMemoryComp` 中。
- `MemoryManager`、UI 和其他调用方可以形成不同的维护路径。
- 四层列表被直接修改，维护约束容易被绕过。
- 后续拆分记忆聚合器时，缺少统一的底层迁移接口。

本方案要求小步迁移，每一步都保持项目可编译，并尽量维持当前存档格式和运行行为。

## 2. 目标架构

```text
MemoryManager (WorldComponent)
  - 全局时间判断
  - 跨 Pawn 任务排队与限流
  - 第一阶段继续触发小时维护
  - 调用 FourLayerMemoryComp 的稳定入口

FourLayerMemoryComp (ThingComp)
  - 持有 ABM / SCM / ELS / CLPA 数据
  - 负责存档读写
  - 持有 Pawn 生命周期
  - 对外提供兼容入口
  - 持有 JobMemoryCapturer 与 MemoryMaintainer

MemoryMaintainer (POCO，每个 MemoryComp 一个实例)
  - Activity 衰减
  - 低活跃记忆清理
  - 容量治理
  - Pin 状态及其直接层级迁移
  - ELS 超限时的直接迁移
  - 提供受控的插入、移动、删除原语

后续 MemoryConsolidator
  - ABM / SCM -> ELS 的语义聚合
  - ELS -> CLPA 的摘要式归档
  - 简单摘要构造与 LLM 请求编排
```

核心约束：

> `FourLayerMemoryComp` 持有状态，`MemoryMaintainer` 执行机械维护规则；调度器和 UI 只触发，不自行实现维护算法。

## 3. 命名与文件位置

新增文件：

```text
Source/Memory/Maintenance/MemoryMaintainer.cs
```

建议命名空间：

```csharp
namespace RimTalk.Memory.Maintenance
```

选择 `MemoryMaintainer` 而不是 `MemoryMaintenanceService`，因为它是由单个 `FourLayerMemoryComp` 持有、绑定单个 Pawn 状态的协作对象，不是全局无状态服务。

## 4. 本阶段职责边界

### 4.1 移入 MemoryMaintainer

从 `FourLayerMemoryComp` 迁移以下职责：

| 当前方法或逻辑 | 目标方法 | 说明 |
|---|---|---|
| `DecayActivity()` | `RunDecay()` | 按层衰减后执行清理和容量治理 |
| `CleanupLowActivityMemories()` | `CleanupLowActivityMemories()` | 保持 `Activity < 0.01f` 和 Pin 保护规则 |
| `EnforceMemoryLimits()` | `EnforceMemoryLimits()` | 第一阶段保持 SCM/ELS 当前限制语义 |
| `TrimEventLog()` | `ArchiveExcessEventLogs()` | 保持“最旧非固定 ELS 直接改层进入 CLPA”的行为 |
| `PinMemory()` 中普通条目逻辑 | `SetPinned()` | Pin ABM 时迁移到 SCM |
| `PinRoundMemory()` 的转换和迁移 | `PinRoundMemory()` | 将 `RoundMemory` 实体化为 SCM `MemoryEntry` |
| 多处列表插入/移动逻辑 | `InsertByTimestamp()` / `Move()` | 形成后续聚合器可复用的原子操作 |

以下方法可以先设为 `internal` 或 `private`，只暴露实际需要的入口，不建立大而全的公共 API。

### 4.2 暂留 FourLayerMemoryComp

- 四层列表及其存档字段。
- `PostExposeData()`。
- 查询、编辑和兼容 API。
- `DailySummarization()`、`ManualSummarization()`、`CreateSimpleSummary()`。
- `RetrieveMemories()` 和检索辅助逻辑。
- 对外兼容方法，如 `DecayActivity()`、`PinMemory()`。

第一阶段的兼容方法只做委托：

```csharp
public void DecayActivity() => Maintainer.RunDecay();

public void PinMemory(string memoryId, bool pinned)
    => Maintainer.SetPinned(memoryId, pinned);
```

这样现有 `MemoryManager` 和 UI 调用点无需同时修改，重构可以分步验证。

### 4.3 明确不移入 MemoryMaintainer

- 每日总结和手动总结的分组、摘要生成。
- LLM 提示词选择、请求、缓存和回调队列。
- 自动归档中的摘要文本生成。
- 检索、评分和注入逻辑。
- UI 缓存刷新、消息提示和确认框。
- 世界级 Pawn 遍历、时间判断和任务限流。

这些内容分别属于后续 `MemoryConsolidator`、AI 基础设施、UI 或 `MemoryManager`。

## 5. 所有权与访问规则

### 5.1 实例所有权

采用与 `JobMemoryCapturer` 相同的所有权模式：

```csharp
private readonly MemoryMaintainer _maintainer;

public MemoryMaintainer Maintainer => _maintainer;

public FourLayerMemoryComp()
{
    _jobCapturer = new JobMemoryCapturer(this);
    _maintainer = new MemoryMaintainer(this);
}
```

`MemoryMaintainer` 第一阶段不持有需要序列化的数据，因此不实现 `IExposable`。它只持有所属 `FourLayerMemoryComp` 的只读引用。

### 5.2 列表访问

为控制改动规模，第一阶段允许 Maintainer 通过 Comp 的 `internal` 列表访问器操作四层数据。不要在 Maintainer 中使用反射，也不要复制四层列表。

推荐逐步形成以下内部原语：

```csharp
internal List<MemoryEntry> GetMutableMemories(MemoryLayer layer);
internal MemoryEntry FindMemoryById(string id);
```

长期目标是将公开的可变 `List<MemoryEntry>` 收敛为只读视图和受控写入口，但这不属于本阶段，避免一次性修改全部捕获、UI 和注入调用方。

### 5.3 状态变更原则

- 同一条记忆在任意时刻只能属于一个层级列表。
- `MemoryEntry.Layer` 必须与所在列表一致。
- 移动操作先验证源和目标，再执行删除与插入。
- Pin 保护规则由 Maintainer 统一实施。
- UI 刷新和用户消息在状态变更完成后由调用方处理。

## 6. 建议接口

第一阶段保持接口精简：

```csharp
public sealed class MemoryMaintainer
{
    public MemoryMaintainer(FourLayerMemoryComp memoryComp);

    public void RunDecay();
    public void EnforcePolicies();
    public bool SetPinned(string memoryId, bool pinned);

    internal void ArchiveExcessEventLogs();
    internal bool Move(MemoryEntry memory, MemoryLayer targetLayer);
    internal bool Remove(string memoryId);
    internal void InsertByTimestamp(MemoryEntry memory, MemoryLayer targetLayer);
}
```

接口语义：

- `RunDecay()`：执行当前小时维护的完整等价流程。
- `EnforcePolicies()`：执行低活跃清理和容量约束，不额外衰减。
- `SetPinned()`：修改 Pin 状态，并应用 ABM -> SCM 规则。
- `ArchiveExcessEventLogs()`：供现有总结流程调用，保持当前 `TrimEventLog()` 行为。
- `Move()`：只做原条目的直接层级迁移，不生成摘要。
- `Remove()`：从所属层删除指定条目。
- `InsertByTimestamp()`：设置目标层并按时间插入。

`MemoryMaintainer` 不提供 `Summarize()` 或 `ArchiveSelected()`。涉及多条源记忆生成新条目的操作留给后续 `MemoryConsolidator`。

## 7. Tick 策略

Tick 迁移分成两个阶段，避免“代码抽取”和“运行时调度变化”同时发生。

### 7.1 第一阶段：保持现有全局调度

继续使用：

```text
MemoryManager.WorldComponentTick
  -> 每 2500 tick 遍历当前地图 Pawn
  -> FourLayerMemoryComp.DecayActivity
  -> MemoryMaintainer.RunDecay
```

这一阶段只改变代码所有权，不改变执行时间、Pawn 筛选范围和每小时集中执行的行为。

### 7.2 第二阶段：迁移到 Pawn 级低频 Tick

第一阶段稳定后，再评估由 `FourLayerMemoryComp` 触发本地维护：

```csharp
public override void CompTickRare()
{
    base.CompTickRare();

    if (parent is Pawn pawn && pawn.IsHashIntervalTick(GenDate.TicksPerHour))
        Maintainer.RunDecay();
}
```

正式实施前必须验证：

- 动物、机械体和非人类 Pawn 是否实际挂载该 Comp。
- 离图 Pawn、世界 Pawn 和未生成 Pawn 是否需要继续衰减。
- `CompTickRare()` 与 `IsHashIntervalTick()` 组合是否会重复或漏触发。
- 从全局集中执行改成分散执行后，读档和暂停恢复是否保持可接受语义。

如果离图 Pawn 也必须维护，则保留 `MemoryManager` 的低频补偿扫描，或者明确维护层只保证生成中 Pawn 的衰减。第二阶段不得同时保留两个无去重机制的小时衰减入口。

## 8. 分阶段实施步骤

### Phase 0：记录行为基线

- [ ] 记录四层初始数量和各层代表条目的 `Activity`。
- [ ] 验证 Pin 条目不会衰减或被清理。
- [ ] 验证 ELS 超过上限后的当前迁移行为。
- [ ] 验证 Pin ABM 和 Pin `RoundMemory` 的当前结果。
- [ ] 记录一次每日总结后 `TrimEventLog()` 的结果。

### Phase 1：建立 Maintainer 骨架

- [ ] 新建 `Source/Memory/Maintenance/MemoryMaintainer.cs`。
- [ ] 构造函数接收并保存 `FourLayerMemoryComp`。
- [ ] 在 `FourLayerMemoryComp` 构造函数中初始化 Maintainer。
- [ ] 添加只读 `Maintainer` 属性。
- [ ] 不改变任何现有调用路径。
- [ ] 编译验证。

### Phase 2：迁移衰减和容量治理

- [ ] 将 `DecayActivity()` 的实现迁入 `RunDecay()`。
- [ ] 将 `CleanupLowActivityMemories()` 迁入 Maintainer。
- [ ] 将 `EnforceMemoryLimits()` 迁入 Maintainer。
- [ ] 保留 `FourLayerMemoryComp.DecayActivity()` 作为委托入口。
- [ ] 保持当前 SCM、ELS、CLPA 衰减率和 ABM 不衰减的行为。
- [ ] 保持当前仅保护 Pin、不保护 `IsUserEdited` 的行为。
- [ ] 编译并执行衰减基线验证。

### Phase 3：建立原子迁移操作

- [ ] 实现按层获取可变列表的内部方法。
- [ ] 实现 `Move()`，统一更新列表和 `MemoryEntry.Layer`。
- [ ] 实现 `InsertByTimestamp()`。
- [ ] 实现 `Remove()`。
- [ ] 为“条目不存在”“源层不一致”“目标层相同”定义确定行为。
- [ ] 不在这些原语中触发 UI 消息或 LLM 请求。

建议行为：

| 情况 | 结果 |
|---|---|
| 条目不存在 | 返回 `false`，不抛异常 |
| 已在目标层 | 校正 `Layer` 后返回 `true`，不重复添加 |
| 同时出现在多个列表 | DevMode 记录错误，并清理重复引用 |
| 目标层无效 | 抛出 `ArgumentOutOfRangeException` |

### Phase 4：迁移 Pin 与直接层级迁移

- [ ] 将普通 `PinMemory()` 状态变更迁入 `SetPinned()`。
- [ ] 保持 Pin ABM 自动进入 SCM 的规则。
- [ ] 将 `RoundMemory` -> SCM `MemoryEntry` 转换迁入 Maintainer。
- [ ] 保持原内容、时间戳、关联 Pawn、地点、标签、关键词和备注。
- [ ] 保持新条目的 `IsPinned = true` 和当前 `IsUserEdited = true` 行为。
- [ ] UI 缓存刷新继续留在 `FourLayerMemoryComp` 或 UI 调用层。
- [ ] 编译并验证普通记忆与 `RoundMemory` 两条 Pin 路径。

### Phase 5：迁移 ELS 超限直接归档

- [ ] 将 `TrimEventLog()` 实现迁为 `ArchiveExcessEventLogs()`。
- [ ] `DailySummarization()` 和 `ManualSummarization()` 暂时通过兼容委托调用。
- [ ] 保持选择最旧非固定 ELS 的当前规则。
- [ ] 保持原条目直接换层，不创建摘要。
- [ ] 保持 Pin ELS 不计入可裁剪数量。
- [ ] 编译并验证两条总结流程。

### Phase 6：收口调用点

- [ ] `MemoryManager` 继续调用 `FourLayerMemoryComp.DecayActivity()`，暂不直接依赖 Maintainer。
- [ ] UI 继续调用 `FourLayerMemoryComp.PinMemory()`，暂不直接依赖 Maintainer。
- [ ] 检查是否仍有机械维护算法残留在 UI 或 `MemoryManager`。
- [ ] 删除已经完全迁移且无调用的私有实现。
- [ ] 更新 `维护层.md` 的组件图和职责说明。

稳定入口暂时保留在 Comp 上，可以减少调用方对内部协作对象的耦合。后续是否公开 `Maintainer` 命令入口，应结合 `MemoryConsolidator` 的设计统一决定。

### Phase 7：独立实施 Tick 下沉

- [ ] 确认 Comp 的实际挂载种族和 Pawn 生命周期范围。
- [ ] 为生成中 Pawn 接入分散的低频维护触发。
- [ ] 移除或调整 `MemoryManager.DecayAllMemories()`，确保不重复衰减。
- [ ] 验证读档冷启动、跨地图、商队和世界 Pawn 行为。
- [ ] 对比集中维护和分散维护的耗时与日志。

## 9. 行为等价要求

第一阶段重构不得有意改变以下行为：

1. ABM 不参与小时 Activity 衰减。
2. SCM、ELS、CLPA 使用各自设置中的衰减率。
3. Pin 条目不衰减、不因低活跃或容量超限被删除。
4. `Activity < 0.01f` 的非固定 SCM、ELS、CLPA 被清理。
5. SCM 和 ELS 超限时按当前最低 Activity、再按最旧时间淘汰。
6. 总结后的 ELS 超限裁剪仍将最旧非固定 ELS 直接迁入 CLPA。
7. Pin ABM 后进入 SCM。
8. Pin `RoundMemory` 后生成普通 SCM 条目并删除原条目。
9. 存档字段名和四层列表序列化格式不变。
10. 每日总结、手动总结和 AI 回调行为不因本次抽取改变。

当前规则之间存在已知差异，例如小时容量治理会删除超限 ELS，而总结后的 `TrimEventLog()` 会把超限 ELS 迁入 CLPA。本阶段保留差异，不在结构重构中顺带改变产品策略。

## 10. 验证方案

### 10.1 编译验证

避免构建后自动部署：

```powershell
dotnet build .\RimTalk-ExpandMemory.csproj -p:BuildingWithScript=true
```

要求：

- 无编译错误。
- 不新增与 Maintainer 相关的可空引用或未使用成员警告。
- 新文件由 SDK 默认编译项自动包含，无需修改 `.csproj`。

### 10.2 存档兼容验证

- [ ] 读取重构前存档，四层数量一致。
- [ ] 所有 `MemoryEntry.Id`、`Layer`、`Activity` 和 Pin 状态保持不变。
- [ ] 保存并重新读取后，Maintainer 可正常工作。
- [ ] Maintainer 本身不产生新的 Scribe 节点。

### 10.3 运行行为验证

- [ ] 普通 SCM/ELS/CLPA 在一次维护后按对应比例衰减。
- [ ] ABM Activity 不变。
- [ ] Pin 条目 Activity 不变。
- [ ] 低于阈值的非固定条目被清理。
- [ ] SCM/ELS 超限时只淘汰非固定条目。
- [ ] ELS 直接归档后只存在于 CLPA，且 `Layer == Archive`。
- [ ] Pin ABM 后只存在于 SCM，且 `Layer == Situational`。
- [ ] Pin `RoundMemory` 后数据字段完整且原条目被删除。
- [ ] 每日总结和手动总结仍能触发 ELS 超限处理。

### 10.4 回归范围

重点回归：

- `MemoryManager` 小时衰减流程。
- `DailySummarization` 和 `ManualSummarization`。
- 记忆窗口 Pin/Unpin。
- `RoundMemory` 固定实体化。
- 存档读写。
- ELS 和 CLPA 注入读取，确认迁移后条目仍可被检索。

## 11. 完成标准

满足以下条件后，第一阶段 Maintainer 抽取才算完成：

- `MemoryMaintainer` 是衰减、低活跃清理、容量治理和直接迁移的唯一实现位置。
- `FourLayerMemoryComp` 中对应公开方法仅作为薄委托或必要的 UI 适配层存在。
- `MemoryManager` 不包含单 Pawn 的维护算法。
- UI 不自行实现 Pin 导致的层级迁移。
- 存档格式没有变化。
- 编译通过，运行验证覆盖衰减、Pin、ELS 超限和读档。
- `维护层.md` 已更新为新职责结构。

## 12. 暂不处理项

以下问题已确认存在，但应在后续独立变更中处理：

- 抽取 `MemoryConsolidator`，统一每日、批量和选中总结。
- UI `AggregateMemories()` 可能删除源层全部未固定条目的问题。
- 自动归档受 `enableDailySummarization` 间接控制的问题。
- ABM-only Pawn 无法进入手动批量总结队列的问题。
- 统一“ELS 超限删除”和“ELS 超限直接归档”的产品策略。
- 让 `maxArchiveMemories` 成为持续约束。
- 收敛四层公开可变列表，禁止外部直接 `Add`/`Remove`。
- 将 AI 回调改成按目标记忆 ID 和请求 ID 回写。

这些问题不应混入 Maintainer 的首次抽取，否则结构迁移与行为修复会相互干扰，增加回归定位难度。

## 13. 建议提交拆分

建议至少拆成三个独立提交：

1. `refactor: add pawn memory maintainer`
   新增骨架、所有权和兼容入口，不迁移行为。
2. `refactor: move memory decay and migration rules`
   迁移衰减、清理、容量治理、Pin 和直接迁移。
3. `refactor: move memory maintenance to pawn ticks`
   在独立验证后改变调度方式，不与逻辑抽取合并。

如果 Phase 4 或 Phase 5 的回归范围较大，可继续拆分 Pin 迁移和 ELS 超限迁移，确保每个提交都能单独编译和验证。
