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
