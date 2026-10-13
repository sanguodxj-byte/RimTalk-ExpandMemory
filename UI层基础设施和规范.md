# RimTalk Expand Memory - UI 层基础设施和开发规范

> 面向在自制 UI 框架上开发的贡献者。工作台的现状布局与组件职责见 `UI层.md`。

## 1. 核心抽象

| 抽象 | 文件 | 语义 |
|---|---|---|
| `UIElement` | `Source/UI/UIElement.cs` | 组件树基础元素。`Pulse()` 在 `Layout` 事件先 `Update()`、随后**每个 IMGUI 事件**执行 `Draw()`；`UpdateRect(rect)` 父→子派发布局；`PostClose()` 关闭清理（子树传播由派生类负责） |
| `UIContext` | `Source/UI/UIContext.cs` | 级联作用域上下文。子可沿父链 `GetContext<T>()` 查找祖先，祖先与兄弟分支不可见；`Pulse()` 在 `Layout` 事件收束派生不变量；`PostClose()` 释放状态 |
| `ScrollUIElement` | `Source/UI/ScrollUIElement.cs` | 滚动视口元素。`ScrollPulse()` 把 `Pulse()` 包裹进 `BeginScrollView/EndScrollView`；子类维护内容总高 `_totalRect` |
| `UGUIElement` | `Source/UI/UGUIElement.cs` | UGUI 嵌入元素。子类实现 `InitiateUGUI()` 返回 Canvas 根对象和定位用 RectTransform；基类负责懒加载、屏幕布局、离屏渲染、IMGUI 合成与关闭清理 |
| `MemoryInputField` | `Source/UI/MemoryInputField.cs` | TMP 输入框扩展。以文本高度提供 `preferredHeight`，查询当前输入焦点，并把滚轮转交父级 ScrollRect |

两条根基约束：

- IMGUI 每帧多次进入 `DoWindowContents`（Layout、Repaint、输入事件），**Draw 高频重复执行，必须幂等**。
- 周期性状态同步放 `Update()`（仅 Layout 事件），布局经 `UpdateRect()` / `UpdateRects()` 派发。`Draw()` 负责绘制与即时交互，业务写入由明确的保存或操作入口执行。

## 2. 支撑工具

| 工具 | 用途 |
|---|---|
| `TextBlock(font/color/anchor)` | 字体、颜色、锚点作用域（Verse 自带） |
| `CropBlock(rect)` | `Widgets.BeginGroup` 裁剪作用域；**禁止无参构造与 `default`**（struct 必须经构造函数记录状态，无参构造直接抛异常） |
| `WidgetsUtil` | `ButtonCircle`（圆形按钮）、`CircleImage`/`CircleContains`（圆形命中）、`DrawGradientLine`、`DrawDashedLine` |
| `MathUtil` | `CalculateNiceStep`（1/2/5×10ⁿ 步长）、`GenerateTicks(FromStep)`（延迟枚举，`start+i*step` 防浮点累积）、`SinePulse`（呼吸动画） |
| `TextUtil` | `MemoryLayer`/`MemoryType` 本地化扩展；`InternalFont` 懒加载 TMP 字体 AssetBundle，配置 Arial 与微软雅黑回退字体 |
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

### 3.1 UGUI 物理像素与屏幕位置

UGUI 元素保留 IMGUI 的逻辑 `_rect`，另计算物理像素 `_absRect`。当前换算为：

```csharp
float scale = Prefs.UIScale;
Rect absRect = new(Verse.UI.GUIToScreenPoint(_rect.position) * scale, _rect.size * scale);
rootRect.position = new Vector2(absRect.x, Screen.height - absRect.yMax);
rootRect.sizeDelta = absRect.size;
```

IMGUI 的 Y 轴向下，UGUI 的屏幕 Y 轴向上，因此根 RectTransform 使用左下角锚点与 pivot，并以 `Screen.height - absRect.yMax` 定位。字体大小、边距、间隔、滚条宽度和滚轮灵敏度也从逻辑值乘 `Prefs.UIScale` 派生。

`GUIToScreenPoint` 依赖当前 GUI 窗口与分组上下文，必须在正确的窗口内容作用域内换算。`SetInitialSizeAndPosition()` 提前派发逻辑布局时，UGUI 根对象可能尚未存在；懒加载完成后必须再次调用 `UpdateRects()`。窗口位置或屏幕尺寸变化后，应重新核对布局与命中区域，不能只更新纹理尺寸。

## 4. 绘制与交互规范

- **拖拽三段式**：`button != 0` 直接返回；`MouseDown` 且命中则记锚点并 `Use()`；`anchor` 激活后处理 `MouseDrag`；`rawType is MouseUp` 结束（`Use()` 过的事件 `type` 会变，`rawType` 保留）。参考分隔条、篇章框选、游标拖拽。
- **滚轮**：矩形命中 + 处理后 `Event.current.Use()` 防穿透；同窗口多个滚轮消费者靠各自命中矩形互斥。
- **层级覆盖**用 `GUIBlock(depth)`；命中优先级与视觉层级由 Pulse 遍历顺序保证（浅层先遍历）。
- 任何 `Use()` 都会让同帧后续控件收不到该事件，消费前必须校验事件类型与命中区域。

### 4.1 UGUI 构建、渲染与生命周期

- `InitiateUGUI()` 先创建并停用 Canvas，完成组件关联后再激活，避免 TMP 在字体、文本对象和视口尚未配置时执行 `OnEnable`。Canvas 使用 `ScreenSpaceCamera` 并添加 `GraphicRaycaster`。
- `UGUIElement` 的静态初始化在没有当前 EventSystem 时创建 `EventSystem + StandaloneInputModule`。UGUI 输入由该系统处理，不能依赖 IMGUI `Use()` 来屏蔽它。
- 基类懒加载后创建内部 `UGUIRender`，递归设置 Unity UI layer（5），配置专用离屏相机，并同步根矩形。
- `UGUIRender.LateUpdate()` 手动渲染至全屏 RenderTexture；纹理尺寸随 `Screen.width/height` 重建，渲染结束恢复 `RenderTexture.active`。相机关闭自动渲染，仅拍摄 UI layer。
- `Draw()` 以 `_absRect` 计算 UV 子区域，通过 `GUI.DrawTextureWithTexCoords` 合成到逻辑 `_rect`。UGUI 的视口裁剪由 `RectMask2D` 负责。
- `PostClose()` 必须沿组件树传播。基类销毁 Canvas 并清除缓存引用，渲染组件的 `OnDestroy()` 清理纹理与相机；重开时重建组件树。懒加载字段的访问需考虑初始化前和关闭后的状态。

### 4.2 TMP 字段、滚轮与焦点

- 详情字段使用 `MemoryInputField`，保持 `interactable = true`，通过 `readOnly` 切换编辑模式。只读态用 `SetTextWithoutNotify` 同步模型；编辑态停止刷新，保留输入框中的草稿。
- 纵向字段组使用 `VerticalLayoutGroup + ContentSizeFitter`，输入框高度由 TMP 文本的 `preferredHeight` 提供。长文本的滚动交给外层 ScrollRect。
- `MemoryInputField.OnScroll()` 经 Harmony `FieldRef` 读取 TMP 私有字段 `m_IScrollHandlerParent` 并转发；升级 TMP 时需核对字段名、类型及父级缓存逻辑。
- `MemoryInputField.AnyFocused()` 检查 EventSystem 当前选中对象上的输入组件与 `isFocused`。两个 Harmony 补丁分别清除 IMGUI 文本焦点、接入 `WindowStack.AnySearchWidgetFocused`，用于协调游戏快捷键。
- UGUI 与 IMGUI 的输入周期独立。外层 ScrollRect 处理 UGUI 滚轮后，IMGUI 侧仍需在详情矩形内消费 ScrollWheel，避免游戏下层响应。

## 5. 数据访问规范

- **读**：经 Context 轮询（每 Layout 事件从 `MemoryComp` 重建缓存），变化下一脉动自动反映。编辑态允许持有独立草稿，期间不得用模型刷新覆盖用户输入。
- **写**：一律经统一入口，禁止 UI 直接操作 comp 四层列表：

| 操作 | 入口 |
|---|---|
| Pin / 取消 Pin（含 ABM→SCM 迁移与 RoundMemory 实体化） | `MemoryComp.Interactor.PinMemory` |
| 删除 | `MemoryComp.Interactor.RemoveMemory` |
| 新增 | `MemoryComp.Interactor.AddMemory` |
| 总结 / 归档选中 | `MemoryComp.Summarizer.ManualSummarize / Archive` |
| 全局总结 | `MemorySummarizer.SummarizeAll()`（静态） |
| 导入 / 导出 | `CustomScribe.Import / Export`（以整个 comp 为单位） |
| 编辑单条记忆字段 | `MemoryDetails.SaveEdit` 回写时间并广播保存，`MemoryDetailsMain.SaveEdit` 回写正文和元数据，最后标记 `IsUserEdited` |

详情编辑上下文持有 `EditingMemory`，通过 `BeginEdit / SaveEdit / EndEdit` 协调容器与正文。取消只丢弃草稿，切换关注点结束当前编辑；`SetFocuse` 先更新值再广播，订阅者可读取新目标。

数值字段应区分展示格式与保存精度，避免仅修改其他字段时损失原值。目前详情输入框使用 `F2` 显示并在保存时解析回写，仍存在未改数值被舍入的限制；新字段应保留完整精度或仅回写实际修改的值。

## 6. 性能规范

- **长列表虚拟化**：卡片经 `Dictionary<MemoryEntry, TCard>` 懒缓存 + `GetOrAdd(memory, _cardFactory)`，仅对可见窗口实体化；绝对布局复用数组。参考 `MemoryTimeline`。
- **热路径零分配**：卡片工厂缓存为字段（不在循环内 new 闭包）；集合 `Clear()` + 重填复用；热循环放弃 LINQ；颜色/枚举/纹理 `static readonly` 预计算。
- **纯绘制结果按输入键缓存**：仅依赖 `(rect, 关键输入)` 的计算缓存为字段，键不变跳过重算。参考 `ChronicleAxis.CacheCheck`。
- 每帧全量轮询仅适用于单 Pawn 两位数量级数据；引入无上限数据源时必须改为增量/脏标记。
- UGUI 离屏渲染持有全屏纹理并在激活时每帧渲染；关闭时及时释放。自动布局、TMP 文本刷新和字段实例数也需计入成本，不能套用时间轴卡片的虚拟化假设。

## 7. 本地化规范

- 用户可见文案走翻译 key，命名空间 `RimTalk.Memory.UI.TabWindow.*` / `RimTalk.Memory.Utils.*`，中英语言文件同步维护。
- 枚举显示用 `TextUtil.Translate()` 扩展，不得在 UI 代码内 switch 枚举拼字符串。
- 动态拼接文案（时间轴"N 天前"等）目前允许硬编码中文，属已知债务；新代码原则上仍走 key。

## 8. Do / Don't 速查

| ✅ Do | ❌ Don't |
|---|---|
| 周期同步放 `Update()`，写入收口于交互入口 | 每次 `Draw()` 无条件修改数据模型 |
| `UpdateRect` 链式派发布局 | 子组件反向读父矩形/字段 |
| 构造函数 `GetContext<T>() ?? throw` 尽早失败 | 运行时反复解析上下文、跨兄弟分支取数 |
| 跨组件通信用 Context 事件 | 组件互相持有引用直接调用 |
| `using` 管理 GUI 状态与裁剪 | 手动改 `GUI.color` 等不恢复 |
| 卡片工厂/集合复用 + 虚拟化 | 每帧每卡 new 对象或闭包 |
| 写操作走统一入口（第 5 节） | UI 直接 `Add`/`Remove` 四层列表 |
| 滚轮/点击消费后 `Use()` | 让事件穿透到下层控件 |
| `PostClose` 传播全部子组件并清缓存 | 关闭后遗留缓存与订阅 |
| UGUI 创建后应用布局，在正确 GUI 作用域换算坐标 | 缓存缺少窗口偏移的屏幕坐标 |
| 编辑态保留字段草稿并考虑数值精度 | 用展示格式无条件覆盖原始数值 |
| 同时协调 UGUI 焦点与 IMGUI 事件消费 | 假定 IMGUI `Use()` 能阻止 EventSystem 输入 |
