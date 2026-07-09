# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.8.0] - 2026-07-09

### Added
- 轮次记忆现默认启用，无需手动开启。 (`7a074b1`)

### Changed
- 工作记忆捕获架构彻底重构为双 Hook 快照式事件驱动：捕获时机从工作开始改为工作结束，记录"已执行的工作"而非"已下达的意图"；零时长工作静默跳过，不再产生幽灵记忆。 (`ae1d0a4`)
- 旧版对话逐条捕获管线重构：直接 Patch `TalkService.CreateInteraction` 替代运行时反射扫描，移除旧去重机制与整条废弃桥接管线。 (`0bfeb62`)
- 规范部分 Patch 文件名。 (`e705a5a`)
- 去除轮次记忆捕获管线中多余的文本处理，提升性能。 (`881c10d`)

## [1.7.0] - 2026-06-14

### Added
- 引入预设内容嗅探机制 (Content Heuristics Sniffing)：注入默认记忆提示词前自动扫描 RimTalk 激活预设，检测到自定义关键词时自动中止注入，实现零配置智能适配，避免 Token 浪费与 AI 幻觉。 (`774dd3d`)

### Changed
- 整理 Patches 文件结构。 (`13678f9`)
- 整理和清理 `FourLayerMemoryComp` 代码结构，不影响现有逻辑。 (`9c8e39a`)
- 优化记忆组件注入逻辑：使用 Harmony `ref ___comps` 直接注入，移除 `InitializeComps` 阶段的反射开销；引入全局单例共享属性，减少 GC 压力；移除 `HasVocalLink` 等死代码。 (`1e6d460`)
- 清理 `PawnMemoryComp` 废弃 API，合并 `CompProperties_PawnMemory` 至同一文件，全局调用链同步迁移至 `FourLayerMemoryComp` 原生方法。 (`5771039`)
- 封装 `Importance` 属性并增加 `Math.Clamp(0, 1)` 收束，默认值由 1f 调整为 0.5f，正确使用后备字段序列化。 (`46806f2`)

## [1.6.0] - 2026-05-16

### Added
- RoundMemory 捕获管线替换为流式捕获管线：每句话实时追加，新增 `AppendLine()` 方法和 `StreamingBuildRoundMemory<T>` 基于 `ConditionalWeakTable` 的 session 隔离。 (`8021cf9`)

### Fixed
- 修复 colonist 代发时玩家发言未被录入轮次记忆的问题。 (`cdda298`)

### Changed
- 更新 `项目管线.md` 文档。 (`1da7b7e`)

## [1.5.0] - 2026-05-12

### Changed
- 优化 `MemoryEntry` 基类数据结构：移除 `CalculateRetrievalScore` 中的 LINQ 操作改用零分配循环，数学运算迁移至 `System.Math`，使用 C# 8 Switch 表达式重构，消除魔法数字统一使用 `GenDate` 常量。 (`ad0562d`)
- 规范化公有字段为 PascalCase 命名，`timestamp` 重命名为 `GameTick` 明确语义，`ExposeData` 序列化键值保持原样以兼容旧存档。 (`7f48f11`)
- 合并 `Age`、`TimeAgoString`、`GameDateString` 为统一的 `AgeString` 属性，使用 C# 9 模式匹配简化时间判定，改用 RimWorld 原生 `GenDate` API。 (`6ae7a7d`)

### Fixed
- 修正用户编辑的记忆无法自然衰减的问题。 (`3acbd6f`)

## [1.4.0] - 2026-04-30

### Fixed
- 修复每日总结中轮次记忆总结不完全的问题：同一个 `RoundMemory` 对象被多个 pawn 总结时只能被第一个正确总结，新增 `CanBeSummarized` 虚属性实现状态隔离。 (`aa2f556`)

### Changed
- `MemoryTypes.cs` 改名为 `MemoryEntry.cs`，部分内容转移至 `MemoryCategory.cs`。 (`b9989b4`)

## [1.3.0] - 2026-04-06

### Changed
- 整理项目根目录：部署脚本归集至 `deploy` 文件夹，文档归集至 `doc` 文件夹，Defs 置于 1.6/1.5 目录以符合当前 RimWorld mod 开发规范。 (`1c70b73`)
- 整理 `.cproj` 项目文件，移除多余内容。 (`78ba4f9`)
- 为 RimChat 相关代码增加显式注解。 (`a090ffd`)
- 更新 CHANGELOG.md 文档。 (`b5f1f97`)

### Fixed
- 移除一处总会在存档加载时刷屏的日志输出。 (`7cd68db`)

### Added
- 添加用于 Visual Studio 开发的 `.props` 文件。 (`50f90ad`)

## [1.2.0] - 2026-03-26

### Added
- 新增泛型高性能环形缓冲区类：强制尺寸为二的幂，位运算寻址，O(1) 读写。 (`75c9497`)

### Changed
- 轮次记忆全局列表应用环形缓冲区：从 RemoveAt(0) 的 O(N) 算法升级为自动覆盖最旧记忆的 O(1) 算法，通过临时列表读写存档完全向后兼容，上限降低至合理的 256。 (`d91a7e7`)
- 规范 `RoundMemoryManager` 字段命名。 (`b211d8a`)
- 增强 `RoundMemoryManager` 健壮性：存档外访问 Instance 时通过 GC 销毁旧实例并返回 null。 (`5cf015a`)

## [1.1.0] - 2026-03-22

### Added
- 优化记忆编辑窗口：扩大窗口尺寸以改善轮次记忆（通常为多行）的查看和编辑体验，为记忆内容窗口增加滚动条。 (`cd27e51`)
- 新增对 RimChat 的轮次记忆捕获支持（软依赖），可捕获 RPG 对话和外交对话内容。 (`a1d1b47`)

### Fixed
- 修复总结记忆时偶现"非法字符"报错的问题，改为显式使用更宽容的转码方式。 (`b744771`)
- 修复对话过于频繁时和预览界面下轮次记忆去重缓存污染的问题：将基于 tick 的缓存重置改为基于 RimTalk 逻辑监听，通过 Patch 在构建 prompt 和渲染预览时重置缓存。 (`9180d7a`)

### Changed
- 记忆总结方法 `InjectMemoriesWithDetails` 可选层级参数拓展为两个，优化内部判断代码。 (`c748a40`)
- 轮次记忆去重逻辑转移至 `ABMCollector`。 (`f08c624`)
- 优化轮次记忆捕获管线架构：在 Patch 中将 RimTalk 相关数据处理为原版数据再传给 `RoundMemoryManager`，由 Manager 全权负责构建和插入，优化文本格式化和清洗性能。 (`ea84173`)
- 优化轮次记忆捕获/创建模块代码：进一步与 RimTalk 解耦，硬依赖集中至 Patch，核心逻辑内聚至核心类。 (`40fdf5f`)

## [1.0.0] - 2026-03-20

接手项目，基于前作代码开始后续维护与迭代。

[1.8.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.7.0...v1.8.0
[1.7.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.6.0...v1.7.0
[1.6.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Anomaly-Works/RimTalk-ExpandMemory/releases/tag/v1.0.0