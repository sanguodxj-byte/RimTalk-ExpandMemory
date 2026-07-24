# AI 层重写方案 - 当前实现说明

> 文档版本：v3.0
> 对齐范围：`Source/AI/`、`Source/Maintenance/MemorySummarizer.cs`、AI 设置与池重置逻辑
> 状态：重写主链已落地；本文描述当前代码事实，不再作为待实施设计稿

## 1. 重写结果

旧链路已经删除：

```text
MemorySummarizer
  -> AIRequestManager (WorldComponent)
  -> IndependentAISummarizer
```

当前唯一的总结/归档 AI 链路是：

```text
MemorySummarizer
  -> AIService.EnqueueAIRequest(prompt, callback, dispose)
  -> AIService.GameComponentUpdate (现实时间 10 秒出队一次)
  -> ClientPool.GetChatCompletionAsync
  -> OpenAIClient.GetChatCompletionAsync
  -> OpenAI 兼容 chat/completions 端点
  -> Payload
  -> MemorySummarizer callback / dispose
```

重写后的职责边界：

- `MemorySummarizer` 负责业务提示词、源条目状态和结果写回。
- `AIService` 负责队列、节流、统一结果检查和回调派发。
- `ClientPool` 负责按配置顺序 fallback。
- `AIClientFactory` 负责选择配置并创建客户端。
- `OpenAIClient` 负责请求构造、网络、超时、重试和响应解析。
- AI 基础设施不直接引用或修改 `MemoryEntry`。

## 2. 当前架构

```text
调用方
  MemorySummarizer.SummarizeInternal / ArchiveInternal
          |
          | EnqueueAIRequest(prompt, callback, dispose)
          v
AIService : GameComponent
  - 实例级 Queue<(prompt, callback, dispose)>
  - 静态 _instance 门户
  - GameComponentUpdate 每帧轮询
  - RealTime.LastRealTime 控制 10 秒出队间隔
  - Payload 有效时才调用 callback
  - finally 中调用 dispose
          |
          v
ClientPool : IAIClient
  - 缓存已创建的 IAIClient
  - 每个请求从池中第 0 个客户端开始尝试
  - 失败时复用或懒创建下一客户端
  - 任一 Payload.IsValid == true 即返回
          |
          v
AIClientFactory
  - 跟随 RimTalk：映射 Settings.Get().CloudConfigs
  - 独立配置：读取本模组 Settings.ApiConfigs
  - 跳过禁用或无效配置
  - 当前所有 Provider 都创建 OpenAIClient
          |
          v
OpenAIClient
  - UnityWebRequest POST
  - OpenAI 兼容请求/响应 DTO
  - 首字节等待 120 秒；开始接收后无进度 60 秒超时
  - 429、5xx、连接和读取超时可重试
  - 401、403 和其他 HTTP 错误立即失败
  - Current.Game == null 时 Abort
```

## 3. 模块与真实路径

| 路径 | 当前职责 |
|---|---|
| `Source/AI/AIService.cs` | `GameComponent` 队列、10 秒节流、Payload 检查、回调与清理派发 |
| `Source/AI/AIClientFactory.cs` | 顺序扫描有效配置并创建客户端 |
| `Source/AI/AIProvider.cs` | Provider 枚举和默认端点注册表 |
| `Source/AI/ApiConfig.cs` | 单条可持久化配置和 `IsValid` |
| `Source/AI/Payload.cs` | AI 调用统一结果及可选日志字段 |
| `Source/AI/Integration/RimTalkApiConfigsGetter.cs` | RimTalk `CloudConfigs` 到本模组配置的深拷贝映射 |
| `Source/AI/Client/IAIClient.cs` | 完成请求与验证接口 |
| `Source/AI/Client/ClientPool.cs` | 客户端缓存、配置 fallback、验证 fallback |
| `Source/AI/Client/OpenAIClient.cs` | UnityWebRequest、超时、重试、序列化和响应解析 |
| `Source/AI/Client/Dto/OpenAIRequest.cs` | OpenAI 兼容请求 DTO |
| `Source/AI/Client/Dto/OpenAIResponse.cs` | OpenAI 兼容响应和错误 DTO |
| `Source/Settings/SettingsUIDrawers.cs` | 独立配置链表格 UI |
| `Source/RimTalkSettings.cs` | AI 配置持久化、验证按钮、提示词和 token 设置 |
| `Source/RimTalkMod.cs` | API 设置 hash 变化后重置 ClientPool |
| `Source/Maintenance/MemorySummarizer.cs` | 当前 AI 层的唯一业务调用方 |
| `Source/VectorDB/EmbeddingService.cs` | 独立的向量 Embedding 实现，不经过本聊天完成链路 |

已删除且不应再作为当前架构引用：

- `Source/Memory/AI/IndependentAISummarizer.cs`
- `Source/Memory/AI/AIRequestManager.cs`
- 旧 Gemini/OpenAI 手写 DTO
- `SiliconFlowEmbeddingService`

## 4. 配置来源与 fallback

### 4.1 配置源二选一

```text
UseRimTalkAIConfig == true
  -> RimTalkApiConfigGetter.GetRimTalkApiConfigs()
  -> Settings.Get().CloudConfigs 的映射副本

UseRimTalkAIConfig == false
  -> RimTalkMemoryPatchMod.Settings.ApiConfigs
```

“跟随 RimTalk”与“独立配置链”不是两级 fallback。开关决定本次只使用其中一个来源；跟随模式失败后不会自动转入本模组独立配置。

### 4.2 工厂扫描

`AIClientFactory` 持有 `_configIndex`，从上次已产出配置的下一项继续扫描：

```csharp
apiConfig is { IsEnabled: true, IsValid: true }
```

`ApiConfig.IsValid` 仅要求：

```text
CustomModelName 非空
URL 是合法绝对 URI
```

API Key 可以为空，以兼容不要求认证的本地或代理端点。`CustomUrl` 非空时覆盖 Provider 默认端点。

### 4.3 ClientPool 行为

- 池在 `AIService` 生命周期内缓存已创建客户端。
- 每个新请求都从 `_clients[0]` 开始，不保存“上次成功配置”的 sticky 指针。
- 当前客户端返回 `null` 或无效 Payload 后，尝试下一已缓存客户端或由工厂创建下一客户端。
- 配置耗尽时返回最后一个失败结果；若一个客户端都未创建则可能返回 `null`。
- `maxRetries = 100` 是池循环保护，不代表会对同一配置发送 100 次请求。
- `Reset()` 清空客户端列表并替换工厂，使下一次扫描重新从配置链头开始。

### 4.4 默认端点

| Provider | Endpoint |
|---|---|
| Google | `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` |
| OpenAI | `https://api.openai.com/v1/chat/completions` |
| DeepSeek | `https://api.deepseek.com/v1/chat/completions` |
| Grok | `https://api.x.ai/v1/chat/completions` |
| GLM | `https://api.z.ai/api/paas/v4/chat/completions` |
| GLMCoding | `https://api.z.ai/api/coding/paas/v4/chat/completions` |
| AlibabaIntl | `https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions` |
| AlibabaCN | `https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions` |
| Custom | 无默认端点，必须填写 `CustomUrl` |

若最终 URL 的 path 恰好为 `/`，`OpenAIClient` 自动补成 `/v1/chat/completions`；非根路径保持原样。

## 5. 单客户端请求语义

### 5.1 请求体

普通请求发送：

```json
{
  "model": "<CustomModelName>",
  "messages": [{ "role": "user", "content": "<prompt>" }],
  "max_tokens": 8000
}
```

`max_tokens` 来自 `Settings.SummaryMaxTokens`。配置验证发送内容为 `ping`、`max_tokens = 1`。DTO 预留 `temperature`，当前调用未设置。

### 5.2 超时和重试

- 轮询间隔：100 ms。
- 收到首字节前连续 120 秒无数据：连接超时。
- 已开始接收后连续 60 秒无新增字节：读取超时。
- 最多 3 次总尝试：首次、等待 4 秒后重试、等待 8 秒后重试。
- 429、5xx、连接超时、读取超时进入重试。
- 401、403 立即失败。
- 其他 HTTP/协议错误立即失败。
- 游戏退出时中止请求并返回无效 Payload，不再重试该客户端。

`ValidateAsync()` 不走上述重试循环；它对当前客户端发送一次 ping，失败后由 `ClientPool.ValidateAsync()` 尝试下一配置。

### 5.3 成功判定

只有同时满足以下条件才把 `Payload.IsValid` 置为 `true`：

- 响应文本非空。
- 可以反序列化为 `OpenAIResponse`。
- `choices` 非空。
- 响应没有显式 `error`。

成功内容取 `choices[0].message.content`，token 数取 `usage.total_tokens`。当前解析不会额外验证 `content` 非空，因此“有效 Payload + 空内容”仍可能到达业务 callback；`MemorySummarizer` 会再次拒绝空白结果。

## 6. AIService 队列与生命周期

`AIService` 是 `GameComponent`，构造时把自身写入静态 `_instance`。静态门户行为：

| 方法 | 无实例时 | 有实例时 |
|---|---|---|
| `EnqueueAIRequest` | 静默丢弃 | 加入实例队列 |
| `ValidateAIConfigAsync` | 返回 `false` | 验证池中配置 |
| `ResetClientPool` | 无操作 | 清空池和工厂 |

队列是 FIFO。`GameComponentUpdate()` 每帧检查，但只有距离上次出队至少 10 秒才启动下一项。这里限制的是启动间隔而非并发数：若单次网络调用超过 10 秒，后续请求仍可能启动，因此并不保证全局只有一个在途请求。

`ExecuteTask` 的约定：

- `Payload == null` 或 `IsValid == false`：记录错误，不调用业务 callback。
- 有效结果：调用 `callback(Payload.Response)`。
- 无论成功、失败或 callback 抛异常，`finally` 都调用 `dispose`。
- 没有单独的 callback 异常隔离；异常可从 `async void` 续体冒出，但 `dispose` 仍会执行。

## 7. 与维护层的接口

`MemorySummarizer` 在提交前：

1. 构建总结或归档提示词。
2. 构造尚未插入列表的目标 `MemoryEntry` 快照。
3. 将源条目 `IsSummarizing = true`。
4. 传入成功 callback 和清理 dispose。

成功 callback 负责写入 ELS/CLPA 并把源条目标为 `IsSummarized = true`；dispose 无条件把源条目的 `IsSummarizing` 恢复为 `false`。因此 AI 层本身不知道记忆层级和条目语义。

## 8. 设置与持久化

| 字段 | Scribe key | 当前用途 |
|---|---|---|
| `EnableAILog` | `EnableAILog` | 记录 URL、模型、请求、响应、token 和错误 |
| `UseRimTalkAIConfig` | `UseRimTalkConfig` | 选择 RimTalk 配置副本或独立配置链 |
| `ApiConfigs` | `ApiConfigs`，`LookMode.Deep` | 独立多配置链 |
| `SummaryMaxTokens` | `ai_summaryMaxTokens` | chat/completions 的 `max_tokens` |

单条 `ApiConfig` 持久化 `IsEnabled`、`Provider`、`ApiKey`、`CustomUrl`、`CustomModelName`。

设置窗口始终绘制独立配置表。验证按钮只在存档内显示，因为它依赖 `AIService` 实例。`RimTalkMemoryPatchMod.WriteSettings()` 对来源开关和独立配置字段计算 hash；变化后调用 `AIService.ResetClientPool()`。

注意：hash 不包含 RimTalk 主模组 `CloudConfigs` 的内容。跟随模式下仅修改 RimTalk 设置不会主动通知本模组刷新已缓存的映射副本；需要触发本模组池重置或创建新的 `AIService`。

## 9. Embedding 边界

聊天完成重写没有统一 Embedding 管线：

- Embedding 实现位于 `Source/VectorDB/EmbeddingService.cs`。
- 设置仍使用 `embeddingApiKey`、`embeddingApiUrl`、`embeddingModel`。
- 它不经过 `AIService`、`ClientPool` 或上述 Provider fallback 链。
- 聊天配置与向量配置是两套独立数据源。

## 10. 实现特征

1. `_instance` 没有显式出档清理。返回主菜单后静态字段可能仍指向旧组件，进入新存档时由新实例覆盖。
2. `EnqueueAIRequest` 不返回是否入队。若异常情况下没有实例，业务方已经设置的 `IsSummarizing` 无法通过 dispose 恢复。
3. 队列不持久化、无取消令牌、无请求 ID、无去重和无结果缓存。
4. 10 秒只限制出队启动频率，不限制在途并发。
5. AI 回调仍通过闭包捕获源条目和目标条目，没有 `pawnId + memoryId + requestId` 失效校验。
6. `OpenAIClient` 只在网络轮询期间检查 `Current.Game`；`AIService` 调用 callback 前没有再次检查当前游戏是否仍是原会话。
7. 配置重置可与在途池遍历交错；代码会尝试重新遍历，但没有显式同步。
8. 所有 Provider 都假设支持 OpenAI 兼容协议，不支持 Gemini 原生协议、Player2 或 Prompt Caching 专有字段。
9. 不迁移已删除的旧独立 API 字段；旧用户需启用跟随 RimTalk 或重新填写 `ApiConfigs`。

## 11. 重写结项清单

- [x] 独立配置通过 `ApiConfigs` 提供单配置和多配置链。
- [x] 当前客户端失败后切换到下一有效配置。
- [x] 401/403 不重试同一客户端，池继续 fallback。
- [x] 429/5xx 最多执行 3 次单客户端尝试。
- [x] 跟随 RimTalk 时使用映射后的 `CloudConfigs`。
- [x] 主菜单不显示验证按钮。
- [x] AI 失败仍执行 `dispose`，源条目可再次总结。
- [x] 出档时 OpenAIClient 在网络轮询中中止请求。
- [x] 修改本模组 API 设置并保存后重置 ClientPool，从配置链头重建。
- [x] AI 层重写按当前实现结项。

本文以当前源码为准。旧类名只用于说明迁移历史，不应再出现在当前执行图中。
