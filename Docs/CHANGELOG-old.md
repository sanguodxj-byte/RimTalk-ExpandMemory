# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.5.1] - 2026-01-20

### Changed
- 将默认的常识匹配源选择策略回滚为更贴近旧版逻辑的保守设定。

## [3.5.0] - 2026-01-19

### Added
- 全面适配 RimTalk 最新版的 Scriban 模板引擎与相关的 API 架构系统。

## [3.4.10] - 2025-12-30

### Changed
- 提升系统性能与稳定性，引入反射属性缓存机制，并实现线程安全的数据快照机制。

## [3.4.7] - 2025-12-29

### Fixed
- 修复 `WorldComponent` 加载时的报错，移除无效的定义，并简化针对旧存档兼容的修复逻辑。

## [3.4.4] - 2025-12-29

### Added
- 添加 RimWorld 1.5 版本的兼容依赖，全面切换至 .NET SDK 9 进行编译。

### Changed
- 优化 `PlayLog` 事件扫描逻辑，并增强 Patch 拦截层的底层稳定性。

### Fixed
- 修复与 RimTalk 主体更新相关的兼容性问题，适配了最新的异步对话生成 Patch 签名。

## [3.4.0] - 2025-12-26

### Changed
- 拆分重构了 `MainTabWindow_Memory` 组件，大幅优化底层的 UI 代码结构。

### Fixed
- 统一使用 Prompt 进行常识匹配，修复了 `injectToContext` 模式下的文本匹配错误。
- 修复对话类记忆在每日总结后意外消失丢失的严重问题。
- 补充了缺失的 UI 面板文件，并强制统一所有 UI 源码为 UTF-8 编码以防乱码。

## [3.3.38] - 2025-12-26

### Changed
- 重大重构：将记忆和常识的注入位置由系统的 `prompts` 级别改为用户消息的 `context` 级别。在保持系统提示词简洁的同时，提高了 AI 对全局规则的关注度和 Token 的利用效率。

## [3.3.2.37] - 2025-12-26

### Changed
- 引入提示词规范化系统，支持正则表达式规范化过滤并实时生效。

### Fixed
- 修复用户自定义提示词中含有花括号（如 JSON 格式）时导致 `string.Format` 解析崩溃的报错问题。

## [3.3.2.36] - 2025-12-25

### Added
- 新增常识库公共 API (`CommonKnowledgeAPI`)，支持通过代码层面对其进行添加、更新、查询和导入导出操作。

### Changed
- 优化常识分类规则，支持部分匹配模式（如：规则-世界观）。
- 重写常识库 UI 帮助文档与 API 使用说明，推荐使用逗号作为标签分隔符。

### Fixed
- 修复总结生成的 `ELS` 时间戳与列表位置不符的问题，确保总结时间戳继承自组内最新的记忆，并按时间戳降序正确插入列表。
- 验证并修复了固定记忆（`isPinned`）在总结和归档阶段未被正确保护的问题。
- 移除 `isUserEdited` 标记对归档行为的限制，用户编辑过的记忆现在也可以正常参与每日的自动归档与清理。

## [3.3.19] - 2025-12-25

### Added
- 引入 Mind Stream UI (全局记忆时间流)：基于卡片式布局的直观记忆查看器。
- 支持框选、拖拽多选，并可按层级与类型进行精准筛选操作。
- 增加各层级容量的实时统计显示与颜色分级指示器。

### Changed
- 优化图形界面的高度渲染逻辑，按记忆类别（ABM/SCM/ELS/CLPA）动态计算并呈现卡片高度。
- 整合并移除了过期的 `MindStream` 与 `Library` 旧界面文件代码。

## [3.3.14] - 2025-12-25

### Added
- 引入记忆自动清理系统：增加记忆活跃度 (`Activity`) 的自动衰减阈值机制，每天自动回收并归档低活跃记忆。

## [3.3.13] - 2025-12-25

### Changed
- 优化知识库相似度打分：调整并提高了基于标签命中率的加权得分统计算法。

## [3.3.0] - 2025-12-24

### Added
- 项目初始核心版本发布。
- 构建了完整的四层记忆系统（SCM, ELS, CLPA, FMS）。
- 增加基于标签匹配与向量增强的双重常识库支持。
- 集成 AI 驱动的离线异步记忆深度总结。
- 新增基于 Pawn 状态与事件记录的自动常识生成。

### Fixed
- 修复殖民者加入日期产生漂移的底层数据问题。
- 完善长期记忆 (CLPA) 的衰减和安全删除机制逻辑。
- 适配并修复 `System.Net.Http` 与其他大型模组（如 SOS2）之间的版本冲突问题。

<!-- GitHub Links -->
[Unreleased]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.5.1...HEAD
[3.5.1]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.5.0...v3.5.1
[3.5.0]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.4.10...v3.5.0
[3.4.10]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.4.7...v3.4.10
[3.4.7]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.4.4...v3.4.7
[3.4.4]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.4.0...v3.4.4
[3.4.0]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.38...v3.4.0
[3.3.38]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.2.37...v3.3.38
[3.3.2.37]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.2.36...v3.3.2.37
[3.3.2.36]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.19...v3.3.2.36
[3.3.19]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.14...v3.3.19
[3.3.14]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.13...v3.3.14
[3.3.13]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/compare/v3.3.0...v3.3.13
[3.3.0]: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/releases/tag/v3.3.0