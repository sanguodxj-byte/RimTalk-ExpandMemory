# Known Issues

本文记录当前已确认、暂不修复的问题。

## 1. 在途 RoundMemory 私有化后可能重复提交

`MemorySummarizer` 使用非持久的 `SummarizingMemories`，按 `MemoryEntry` 对象引用记录正在总结或归档的条目；持久完成状态则由每个 Pawn 的 `FourLayerMemoryComp.SummarizedIds` 按 `OriginId` 记录。

当一个 `RoundMemory` 已提交 AI 总结但尚未完成时，ABM 到期转换或 Pin 操作可能调用 `Privatize()`，产生一个具有相同 `OriginId` 的新 `MemoryEntry` 对象。新对象不在 `SummarizingMemories` 中，而该来源在 AI 成功前也尚未写入 `SummarizedIds`。此时若从手动总结等其他入口再次提交新对象，可能产生重复请求、重复 ELS 总结及额外 API 消耗。

当前状态：已知并接受。后续可将正在处理的状态改为按 Pawn 私有的 `OriginId` 跟踪，并在构建候选集合时按 `OriginId` 去重。

## 2. 导入导出不保留总结状态

当前记忆导出格式只序列化四层 `MemoryEntry` 列表，不包含所属 Pawn 的 `FourLayerMemoryComp.SummarizedIds`；导入逻辑也只按 `Layer` 将条目加入目标列表。

因此，导出前已经总结的 ABM/SCM 或已经归档的 ELS，在导入后可能被视为尚未处理，再次进入自动总结或自动归档流程。这可能产生重复总结、重复归档及额外 API 消耗。

当前状态：已知并接受。导入导出功能将在未来完善，届时应增加格式版本、总结状态持久化以及目标 Pawn 中 ID 冲突的处理规则。

## 3. 部分特殊 Action 无法进入当前捕获管线

当前行动记忆由两条管线组成：`JobMemoryCapturer` 通过 `CleanupCurrentJob` 捕获所有 Job 结束事件（`Succeeded` 或 `_jobsAlwaysCapture` 白名单命中时放行非 Succeeded），`CombatMemoryCapturer` 则通过 `Pawn.Notify_UsedVerb` 补充捕获征召 `Wait_Combat` 中由 `JobDriver_Wait.CheckForAutoAttack` 直接触发、不创建攻击 Job 的自动攻击。部分 Action 虽然已经成功生效，但会绕过 Job，或在效果执行过程中主动中断施法者的 Job 且未被 `_jobsAlwaysCapture` 覆盖，因此无法被当前管线记录。

### 稳定绕过 Job 的 Action

- `nonInterruptingSelfCast` 自施法：烟雾包、灭火泡沫包、低盾包、毒气包，以及 `GhoulFrenzy`、`MetalbloodInjection`、`RevenantInvisibility` 等。此类动作会直接启动 Verb，不创建 Job；这些 Verb 也不属于普通武器攻击，不进入 `CombatMemoryCapturer` 的 `Wait_Combat` 过滤。
- 商队中的世界目标能力：例如商队内施放 `Farskip`。该路径直接调用 `Ability.Activate(GlobalTargetInfo)`，既没有 Job，也不经过 `Pawn.Notify_UsedVerb`。

### 成功生效但未被 `_jobsAlwaysCapture` 覆盖的 Action

- 自我传送：`Skip`、`ChaosSkip`、`EntitySkip`，以及 `MassChaosSkip` 波及施法者自身时。传送效果调用施法者的 `Notify_Teleported()`，以 `InterruptForced` 结束当前施法 Job。由于 `CastAbilityOnThing` 已在 `_jobsAlwaysCapture` 中，此类能力**已被捕获**；但若能力使用的 JobDef 不是 `CastAbilityOnThing`/`CastAbilityOnWorldTile`（Mod 自定义 JobDef），则不在覆盖范围内。
- 对施法者自身施加精神状态：例如对自己使用 `Berserk`、`BerserkPulse` 波及自己，以及 `BerserkTrance`。精神状态通常设置 `stopsJobs = true`，生效后会停止当前施法 Job。同理，若走标准 `CastAbilityOnThing` 则已被捕获。
- 使施法者立即倒地或失去意识：最明确的原版例子是 `Neuroquake`，成功后向施法者施加 `PsychicComa`；倒地会以 `InterruptForced` 结束当前 Job。具有同类效果的 Mod 能力若走标准能力 JobDef 则已被捕获。
- 一次性消耗武器的最后一次射击：例如 `Verb_ShootOneUse` / `Verb_LaunchProjectileStaticOneUse`。射击成功过程中会销毁装备，装备丢失可能先结束或替换攻击 Job。`AttackStatic` 已在 `_jobsAlwaysCapture` 中，Job 层面会被捕获；但 `CombatMemoryCapturer` 不再负责此类攻击，因此若 Job 在装备销毁瞬间被替换为非攻击 Job，存在丢失该次射击的风险。

### 攻击 Job 的语义放宽

`AttackMelee` / `AttackStatic` 现由 `JobMemoryCapturer` 在 Job 结束时捕获，不再依赖 `Notify_UsedVerb` 的"实际执行"语义。一个以 `InterruptForced` 结束的 AttackMelee 可能从未真正挥击（例如殖民者在走向目标途中被其他事件打断），但仍会被记录为一次"近身攻击"。这是从 Verb 管线回到 Job 管线的固有 tradeoff：以精度换取与能力/工作记忆统一的聚合管线。`CombatMemoryCapturer` 保留的 `Notify_UsedVerb` 路径仅用于 `Wait_Combat` 自动攻击，该场景下 Verb 成功执行是已确认的事实。

当前状态：已知并接受。潜在解决方向是为能力单开一个 capturer，以 `Ability.Activate(...)` 为成功语义，覆盖无 Job 和自中断能力；普通攻击的 Job 管线精度问题可在未来通过检查 Job 实际执行时长或 Verb 调用记录来收紧。
