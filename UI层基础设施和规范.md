# RimTalk Expand Memory - UI 层基础设施和开发规范

> 面向在自制 UI 框架上开发的贡献者。工作台的现状布局与组件职责见 `UI层.md`。

## 1. 核心抽象

| 抽象 | 文件 | 语义 |
|---|---|---|
| `UIElement` | `Source/UI/UIElement.cs` | 组件树基础元素。`Pulse()` 在 `Layout` 事件先 `Update()`、随后**每个 IMGUI 事件**执行 `Draw()`；`UpdateRect(rect)` 父→子派发布局；`PostClose()` 关闭清理（子树传播由派生类负责） |
| `UIContext` | `Source/UI/UIContext.cs` | 级联作用域上下文。子可沿父链 `GetContext<T>()` 查找祖先，祖先与兄弟分支不可见；`Pulse()` 在 `Layout` 事件收束派生不变量；`PostClose()` 释放状态 |
| `ScrollUIElement` | `Source/UI/ScrollUIElement.cs` | 滚动视口元素。`ScrollPulse()` 把 `Pulse()` 包裹进 `BeginScrollView/EndScrollView`；子类维护内容总高 `_totalRect` |

两条根基约束：

- IMGUI 每帧多次进入 `DoWindowContents`（Layout、Repaint、输入事件），**Draw 高频重复执行，必须幂等**。
- 状态更新与布局计算放 `Update()`（仅 Layout 事件），`Draw()` 只做绘制与即时交互响应，不修改数据模型。

## 2. 支撑工具

| 工具 | 用途 |
|---|---|
| `TextBlock(font/color/anchor)` | 字体、颜色、锚点作用域（Verse 自带） |
| `CropBlock(rect)` | `Widgets.BeginGroup` 裁剪作用域；**禁止无参构造与 `default`**（struct 必须经构造函数记录状态，无参构造直接抛异常） |
| `WidgetsUtil` | `ButtonCircle`（圆形按钮）、`CircleImage`/`CircleContains`（圆形命中）、`DrawGradientLine`、`DrawDashedLine` |
| `MathUtil` | `CalculateNiceStep`（1/2/5×10ⁿ 步长）、`GenerateTicks(FromStep)`（延迟枚举，`start+i*step` 防浮点累积）、`SinePulse`（呼吸动画） |
| `TextUtil` | `MemoryLayer`/`MemoryType` 枚举本地化扩展（`layer.Translate()`，可空值返回"全部××"） |
| `EnumUtil` / `DictionaryExtensions` | 枚举值枚举（过滤 Obsolete）/ `GetOrAdd` 卡片缓存 |

GUI 全局状态（颜色、字体、深度、裁剪）一律 `using` 块管理。

## 3. 坐标规范

1. 组件树内部一律使用**窗口局部坐标（零基）**；布局只经 `UpdateRect` 链父→子传递，子组件不得反向读取父组件矩形。
2. `Context.InRect` 只承载尺寸（经两次 `AtZero()` 位置恒为原点），**不得当作屏幕矩形使用**。
3. 弹出真 Window（侧边栏/对话框）需屏幕坐标，换算公式：

```csharp
Rect windowRect = Find.WindowStack.currentlyDrawnWindow.windowRect; // 当前正在绘制的 Bridge
float margin = Window.StandardMargin;                               // 18f
Vector2 screenPos = localPos + windowRect.position + new Vector2(margin, margin);
```

`currentlyDrawnWindow` 在 `DoWindowContents` 前被赋值，组件 `Draw` 内取值安全。

4. 嵌套作用域（`GUI.BeginGroup` / `BeginScrollView` / `Listing.Begin`）内部坐标**再次归零且绘制与命中均被裁剪**，控件矩形必须相对当前作用域原点分配。

## 4. 绘制与交互规范

- **拖拽三段式**：`button != 0` 直接返回；`MouseDown` 且命中则记锚点并 `Use()`；`anchor` 激活后处理 `MouseDrag`；`rawType is MouseUp` 结束（`Use()` 过的事件 `type` 会变，`rawType` 保留）。参考分隔条、篇章框选、游标拖拽。
- **滚轮**：矩形命中 + 处理后 `Event.current.Use()` 防穿透；同窗口多个滚轮消费者靠各自命中矩形互斥。
- **层级覆盖**用 `GUIBlock(depth)`；命中优先级与视觉层级由 Pulse 遍历顺序保证（浅层先遍历）。
- 任何 `Use()` 都会让同帧后续控件收不到该事件，消费前必须校验事件类型与命中区域。

## 5. 数据访问规范

- **读**：经 Context 轮询（每 Layout 事件从 `MemoryComp` 重建缓存），UI 不持有数据快照，变化下一脉动自动反映。
- **写**：一律经统一入口，禁止 UI 直接操作 comp 四层列表：

| 操作 | 入口 |
|---|---|
| Pin / 取消 Pin（含 ABM→SCM 迁移与 RoundMemory 实体化） | `MemoryComp.Interactor.PinMemory` |
| 删除 | `MemoryComp.Interactor.RemoveMemory` |
| 新增 | `MemoryComp.Interactor.AddMemory` |
| 总结 / 归档选中 | `MemoryComp.Summarizer.ManualSummarize / Archive` |
| 全局总结 | `MemorySummarizer.SummarizeAll()`（静态） |
| 导入 / 导出 | `CustomScribe.Import / Export`（以整个 comp 为单位） |

## 6. 性能规范

- **长列表虚拟化**：卡片经 `Dictionary<MemoryEntry, TCard>` 懒缓存 + `GetOrAdd(memory, _cardFactory)`，仅对可见窗口实体化；绝对布局复用数组。参考 `MemoryTimeline`。
- **热路径零分配**：卡片工厂缓存为字段（不在循环内 new 闭包）；集合 `Clear()` + 重填复用；热循环放弃 LINQ；颜色/枚举/纹理 `static readonly` 预计算。
- **纯绘制结果按输入键缓存**：仅依赖 `(rect, 关键输入)` 的计算缓存为字段，键不变跳过重算。参考 `ChronicleAxis.CacheCheck`。
- 每帧全量轮询仅适用于单 Pawn 两位数量级数据；引入无上限数据源时必须改为增量/脏标记。

## 7. 本地化规范

- 用户可见文案走翻译 key，命名空间 `RimTalk.Memory.UI.TabWindow.*` / `RimTalk.Memory.Utils.*`，中英语言文件同步维护。
- 枚举显示用 `TextUtil.Translate()` 扩展，不得在 UI 代码内 switch 枚举拼字符串。
- 动态拼接文案（时间轴"N 天前"等）目前允许硬编码中文，属已知债务；新代码原则上仍走 key。

## 8. Do / Don't 速查

| ✅ Do | ❌ Don't |
|---|---|
| 状态更新放 `Update()`，`Draw()` 幂等 | 在 `Draw()` 做布局计算或修改数据模型 |
| `UpdateRect` 链式派发布局 | 子组件反向读父矩形/字段 |
| 构造函数 `GetContext<T>() ?? throw` 尽早失败 | 运行时反复解析上下文、跨兄弟分支取数 |
| 跨组件通信用 Context 事件 | 组件互相持有引用直接调用 |
| `using` 管理 GUI 状态与裁剪 | 手动改 `GUI.color` 等不恢复 |
| 卡片工厂/集合复用 + 虚拟化 | 每帧每卡 new 对象或闭包 |
| 写操作走统一入口（第 5 节） | UI 直接 `Add`/`Remove` 四层列表 |
| 滚轮/点击消费后 `Use()` | 让事件穿透到下层控件 |
| `PostClose` 传播全部子组件并清缓存 | 关闭后遗留缓存与订阅 |
