# UI 层文档

本文档记录当前 UI 层的实际结构和功能。`MainTabWindow_Memory_UI重写概念设计.md` 是产品与交互设计总纲。

## 一、当前结构

记忆主界面位于 `Source/UI/`。主窗口负责生命周期和布局，各区域通过同一个 `MemoryTabContext` 共享当前会话数据，不使用 `partial` 拆分，也不持有主窗口引用。

| 文件 | 职责 |
|---|---|
| `MemoryTabWindow.cs` | 主标签生命周期、五区布局、Pawn 跟随和分割条 |
| `MemoryTabContext.cs` | 各区域共享的会话上下文及跨状态协调操作 |
| `MemoryTabState.cs` | 全局单一的滚动、筛选、光标、焦点和选择状态 |
| `MemoryTabRuntimeModel.cs` | 当前 `FourLayerMemoryComp`、记忆全集、篇章筛选和生命时间边界 |
| `MemoryTabHeader.cs` | Pawn 选择、筛选、统计与工具菜单 |
| `MemoryChronicle.cs` | 横向 CLPA 篇章、缩放、光标、多选与框选 |
| `MemoryTimelineModel.cs` | 年、象和 ELS 推断时段的展示模型 |
| `MemoryTimelineView.cs` | 纵向时间流、折叠、选择、总结状态和 Activity 绘制 |
| `MemoryDetails.cs` | 常驻详情与原位编辑 |
| `MemorySelectionBar.cs` | 批量总结、归档、删除与清除选择 |
| `MemoryCreateDialog.cs` | 新建四层记忆 |
| `MemoryTabPawnSelector.cs` | 可搜索 Pawn 选择窗口 |
| `MemoryArchiveTransferService.cs` | 导入导出文件交互适配 |
| `MemoryArchiveText.cs` | 新 UI 翻译文本入口 |

复杂记忆操作仍由 `MemoryArchiveCommands`、`MemorySummarizer` 和 `MemoryMaintainer` 执行。

## 二、共享上下文

`MemoryTabWindow` 创建一个 `MemoryTabContext`，并在构造 Header、Chronicle、Timeline、Details 和 SelectionBar 时注入。各区域绘制时只接收自己的 `Rect`。

```text
MemoryTabWindow
└── MemoryTabContext
    ├── MemoryTabState
    └── MemoryTabRuntimeModel
```

- `MemoryTabState` 是整个标签共享的一份 UI 状态，切换 Pawn 时不会创建 Pawn 私有状态对象。
- `MemoryTabRuntimeModel` 每帧从当前 `FourLayerMemoryComp` 刷新记忆全集、CLPA 查询结果和生命时间边界。
- Context 仅对需要同步多个引用的操作提供方法，例如固定后替换选择/焦点、刷新失效引用和批处理。
- Header 与 SelectionBar 的业务入口由各自实例绘制，主窗口不再承载区域内部控件。

## 三、注册入口

当前维护的 RimWorld 1.6 在 `1.6/Defs/MainTabDefs.xml` 注册：

`RimTalk.Memory.UI.MemoryTabWindow`

## 四、主窗口区域

1. 顶部栏：Pawn 搜索选择、标签筛选、层级/类型筛选、四层统计和工具菜单。
2. 人生篇章：横向 CLPA 跨度、时间轴、缩放、阅读光标和重叠篇章入口。
3. 记忆时间流：年、象、ELS 三级结构及 ABM/SCM 具体经历。
4. 详情栏：完整内容、元数据、状态及原位编辑，可拖拽调整宽度。
5. 选择栏：只对明确选中项执行总结、归档和删除。

## 五、当前功能

- 世界选择与主窗口 Pawn 同步；非 Pawn 选择不清空当前目标。
- 可搜索当前地图中带有 `FourLayerMemoryComp` 的 Pawn。
- 按标签、层级和类型筛选，显示 ABM/SCM/ELS/CLPA 数量。
- 年、象与 ELS 支持折叠和组复选框。
- ABM/SCM/ELS/CLPA 分别使用蓝青、绿、琥珀、紫色标识。
- 时间流显示总结中动态紫色边框和已总结紫色边框。
- 时间流与篇章区均支持单选、Ctrl 多选、Shift 范围选择和框选。
- 篇章区滚轮只平移篇章视窗；时间轴滚轮缩放；点击或拖动时间轴移动阅读光标。
- 时间流中央节点与篇章阅读光标双向同步。
- 每张记忆卡片提供固定按钮；固定 `RoundMemory` 时允许业务层私有化。
- 详情编辑正文、备注、标签和重要性，不改变固定状态，也不会触发 `RoundMemory` 私有化。
- 编辑态持续暂停游戏；已有记忆正文允许为空。
- 明确选择后可总结、归档和删除。
- 可创建四层记忆、导入导出和触发全局总结。

## 六、状态生命周期

- 时间流宽度写入 Mod Config。
- 光标、滚动、折叠、筛选、焦点和选择属于主标签全局单一状态，不写入存档。
- 切换 `FourLayerMemoryComp` 时清空焦点和选择，并让时间流按当前光标重新定位。
- 当前组件中的记忆发生异步变化时，下一帧刷新派生模型并清除失效焦点和选择。

## 七、保留的独立窗口

常识库、注入预览、总结提示词、标签测试和通用文本输入仍位于 `Source/Memory/UI/`。它们是独立工具，不属于主窗口区域实例。
