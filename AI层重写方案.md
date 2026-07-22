# AI 层重写方案 — 实现说明

> 文档版本: v2.0（对齐当前代码实现）  
> 适用项目: RimTalk-ExpandMemory (`cj.rimtalk.expandmemory`)  
> 状态: **已实现**（原 v1.0 为设计评审稿，本文按落地代码回写）

---

## 目录

- [一、背景与动机](#一背景与动机)
- [二、设计目标与原则](#二设计目标与原则)
- [三、整体架构](#三整体架构)
- [四、模块清单与职责](#四模块清单与职责)
- [五、Fallback 与配置链](#五fallback-与配置链)
- [六、关键数据类型](#六关键数据类型)
- [七、调用方迁移](#七调用方迁移)
- [八、Settings UI 与持久化](#八settings-ui-与持久化)
- [九、Embedding / 向量](#九embedding--向量)
- [十、目录结构](#十目录结构)
- [十一、行为约定与已知限制](#十一行为约定与已知限制)

---

## 一、背景与动机

### 1.1 旧实现问题

旧链路：

```
MemorySummarizer
  → AIRequestManager (WorldComponent, 10s 节流队列)
  → IndependentAISummarizer.CallAIAsync
```

`IndependentAISummarizer`（约 864 行）职责过重：配置加载、RimTalk 反射、手写 JSON、Gemini 原生协议、正则抠响应、HttpWebRequest 重试、Player2 探测、Prompt Caching 字段等。

### 1.2 重写后入口

```
MemorySummarizer
  → AIService.EnqueueAIRequest (GameComponent, 10s 节流队列)
  → ClientPool (IAIClient, 多配置 fallback)
  → OpenAIClient (UnityWebRequest + OpenAI 兼容端点)
```

---

## 二、设计目标与原则

| 原则 | 落地情况 |
|------|----------|
| 分层清晰 | 编排 / 池 / 工厂 / 客户端 / DTO 分离 |
| 对齐 RimTalk 主模组模式 | OpenAI 兼容端点、Provider 表、多 ApiConfig 表 UI |
| **不复用** RimTalk 类型 | 本模组自有 `ApiConfig` / `AIProvider`；跟随 RimTalk 时经 `RimTalkApiConfigGetter` 映射拷贝 |
| 全 Provider 走 OpenAI 兼容 chat/completions | Google 亦用 `.../v1beta/openai/chat/completions` |
| 不做旧独立配置迁移 | 删除 `independentApiKey/Url/Model/Provider`；升级后需重配或跟随 RimTalk |
| 去掉 Prompt Caching / Player2 | 本期不支持 |
| 验证仅存档内 | 依赖 `AIService` GameComponent 实例 |

---

## 三、整体架构

```
┌─────────────────────────────────────────────────────────────┐
│ 调用方                                                       │
│   MemorySummarizer.SummarizeInternal / Archive               │
│     AIService.EnqueueAIRequest(prompt, callback, dispose)    │
└────────────────────────────┬────────────────────────────────┘
                             │ 10s 现实时间节流
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ AIService : GameComponent                                    │
│   - 队列 + GameComponentUpdate 出列                           │
│   - ExecuteTask → ClientPool.GetChatCompletionAsync          │
│   - Payload.IsValid → callback；finally → dispose            │
│   - ValidateAIConfigAsync / ResetClientPool（静态门户）        │
└────────────────────────────┬────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ ClientPool : IAIClient                                       │
│   - 懒构建客户端列表，失败则工厂再取下一个有效配置               │
│   - 成功: result.IsValid == true 即返回                       │
│   - Reset() 清空池 + 新工厂（设置变更时）                      │
└────────────────────────────┬────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ AIClientFactory                                              │
│   - UseRimTalkAIConfig ? RimTalk 映射列表 : settings.ApiConfigs│
│   - 跳过 !IsEnabled / !IsValid                                │
│   - BuildClient → 目前一律 OpenAIClient                      │
└────────────────────────────┬────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ OpenAIClient : IAIClient                                     │
│   - UnityWebRequest POST                                      │
│   - 单客户端内重试: 最多 3 次尝试，间隔 4s / 8s                  │
│   - 401/403/其它致命 4xx 不重试；429/5xx 可重试                 │
│   - 存档中途 Current.Game==null → abort                       │
│   - ParseSuccessResponse 成功时 payload.IsValid = true        │
└─────────────────────────────────────────────────────────────┘
```

配置来源：

```
UseRimTalkAIConfig == true
  → RimTalkApiConfigGetter.GetRimTalkApiConfigs()
     (Settings.Get().CloudConfigs → 本模组 List<ApiConfig> 深拷贝映射)

UseRimTalkAIConfig == false
  → RimTalkMemoryPatchMod.Settings.ApiConfigs
```

设置写入时 `WriteSettings` 对 API 相关字段做 hash，变化则 `AIService.ResetClientPool()`。

---

## 四、模块清单与职责

| 路径 | 职责 |
|------|------|
| `Source/AI/AIService.cs` | 队列编排、`GameComponent` 生命周期、静态入队/校验/重置 |
| `Source/AI/Client/ClientPool.cs` | 多客户端 fallback 池，实现 `IAIClient` |
| `Source/AI/AIClientFactory.cs` | 按配置链顺序产出下一个有效客户端 |
| `Source/AI/Client/IAIClient.cs` | `GetChatCompletionAsync` / `ValidateAsync` |
| `Source/AI/Client/OpenAIClient.cs` | OpenAI 兼容 HTTP 客户端 |
| `Source/AI/Client/Dto/OpenAIRequest.cs` | 请求 DTO（DataContract） |
| `Source/AI/Client/Dto/OpenAIResponse.cs` | 响应 / Error DTO |
| `Source/AI/Payload.cs` | 统一结果：`IsValid` 默认 false，成功须显式置 true |
| `Source/Integration/RimTalkApiConfigsGetter.cs` | RimTalk `CloudConfigs` → 本模组 `ApiConfig` |
| `Source/Settings/AIProvider.cs` | Provider 枚举 + 默认 endpoint 注册表 |
| `Source/Settings/ApiConfig.cs` | 单条配置 `IExposable` + `IsValid` |
| `Source/Settings/SettingsUIDrawers.cs` | 多备选配置表 UI + 实时无效提示 |
| `Source/Utils/JsonUtil.cs` | DataContractJsonSerializer 序列化/反序列化 |
| `Source/Utils/MessageUtil.cs` | Message + Log.Error 合一 |
| `Source/Memory/EmbeddingService.cs` | 由 `Memory/AI/` 上移（内容未改） |

### 已删除

- `Source/Memory/AI/IndependentAISummarizer.cs`
- `Source/Memory/AI/AIRequestManager.cs`
- `Source/Memory/AI/DTO/GeminiTypes.cs` / `OpenAITypes.cs`
- `Source/Memory/AI/SiliconFlowEmbeddingService.cs`（向量客户端实现；向量入口仍可能用其它路径）

---

## 五、Fallback 与配置链

### 5.1 无 sticky 指针

**没有** `CurrentApiConfigIndex` / `TryNextConfig()`。  
每次 `ClientPool.Reset()` 后从链头重新懒构建；单次请求内按池中已有客户端顺序试，失败再 `TryGetNewClient` 追加下一个有效配置。

### 5.2 有效配置条件

```csharp
apiConfig is { IsEnabled: true, IsValid: true }

// ApiConfig.IsValid:
!string.IsNullOrWhiteSpace(CustomModelName)
&& Uri.IsWellFormedUriString(URL, UriKind.Absolute)

// URL: 非空 CustomUrl 优先，否则 Provider.GetEndpointUrl()
// ApiKey 可为空（兼容部分本地/兼容端点）
```

### 5.3 Provider 注册表（默认 URL）

| Provider | Endpoint |
|----------|----------|
| Google | `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` |
| OpenAI | `https://api.openai.com/v1/chat/completions` |
| DeepSeek | `https://api.deepseek.com/v1/chat/completions` |
| Grok | `https://api.x.ai/v1/chat/completions` |
| GLM | `https://api.z.ai/api/paas/v4/chat/completions` |
| GLMCoding | `https://api.z.ai/api/coding/paas/v4/chat/completions` |
| AlibabaIntl | `https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions` |
| AlibabaCN | `https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions` |
| Custom | 无默认 URL，必须填 `CustomUrl` |

`OpenAIClient`：若 URL path 为 `/`，自动补 `/v1/chat/completions`。

### 5.4 RimTalk 映射

`RimTalkApiConfigGetter` 将 `global::RimTalk.AIProvider` 映射到本模组枚举；`AlibabaIntl` / `AlibabaCN` 各自对应；未知 → `Custom`。  
`BaseUrl` → `CustomUrl`，`CustomModelName` / `ApiKey` / `IsEnabled` 原样拷贝。

工厂对 RimTalk 列表做懒缓存（`??=`）；配置变更依赖 `ResetClientPool()` 换新工厂。

---

## 六、关键数据类型

### 6.1 Payload

```csharp
public class Payload
{
    public string URL, Model, Request, Response, ErrorMessage;
    public int? TokenCount;
    public bool IsValid { get; set; } = false; // 仅成功解析后显式 true
}
```

- `EnableAILog == true` 时请求侧预填 URL/Model/Request，失败可写 ErrorMessage/Response  
- `AIService`：`null` 或 `!IsValid` 不调 callback，仍执行 dispose  
- `ClientPool`：`result?.IsValid ?? false` 为 true 才停止 fallback  

### 6.2 ApiConfig

字段：`IsEnabled`, `Provider`, `ApiKey`, `CustomUrl`, `CustomModelName`。  
`IExposable` 深存于 `ApiConfigs` 集合。

### 6.3 请求 JSON

`JsonUtil.SerializeToJson(OpenAIRequest)`：`model` / `messages` / `max_tokens`（默认取 `Settings.SummaryMaxTokens`）。  
校验 ping：`max_tokens: 1`。

---

## 七、调用方迁移

### 7.1 MemorySummarizer

- 删除 `IndependentAISummarizer.IsAvailable()` 门闩（无配置也会入队，失败由池/客户端处理）
- 入队：

```csharp
AIService.EnqueueAIRequest(
    prompt,
    callback: result => { /* 写 ELS / IsSummarized */ },
    dispose: () => { /* 清除 IsSummarizing */ });
```

### 7.2 BackCompatibilityFix

预注册类型由 `AIRequestManager` 改为 `AIService`。

### 7.3 Dialog_PromptEditor

`summaryMaxTokens` → `SummaryMaxTokens`（scribe key 仍为 `ai_summaryMaxTokens`）。

### 7.4 生命周期

- `AIService(Game game)`：满足 `Game.FillComponents` 的 `Activator.CreateInstance(type, this)`  
- 仅存档内有实例；主菜单 `Enqueue` / `Validate` / `ResetClientPool` 无实例时为空操作或返回 false  

---

## 八、Settings UI 与持久化

### 8.1 字段

| 字段 | Scribe | 说明 |
|------|--------|------|
| `EnableAILog` | `EnableAILog` | AI 请求日志 |
| `UseRimTalkAIConfig` | `UseRimTalkConfig` | 跟随 RimTalk（默认 true） |
| `ApiConfigs` | `ApiConfigs` LookMode.Deep | 独立 fallback 链 |
| `SummaryMaxTokens` | `ai_summaryMaxTokens` | 请求 max_tokens |

**已删除:** `independentApiKey/Url/Model/Provider`、`enablePromptCaching`、`CurrentApiConfigIndex`。  
**不做**旧字段 → `ApiConfigs` 的自动迁移。

### 8.2 UI 行为

1. EnableAILog 复选  
2. PreferRimTalkAI：开 → 绿字跟随说明；关 → 灰字独立链说明  
3. **始终**绘制独立配置表（跟随 RimTalk 时仍可编辑，作为关闭跟随后的链）  
4. 表：Provider 下拉、ApiKey、Custom 时额外 BaseUrl、Model、启用/排序/删除  
5. 启用且 `!IsValid` → 统一红字 `RimTalk_Settings_ApiConfigInvalid`（不区分缺 model / URL）  
6. 验证区：  
   - `Current.Game == null`：只显示 `ValidateInSaveOnly`，**不画按钮**  
   - 存档内：验证按钮 + Tip；`ValidateAIConfig` → `ResetClientPool` + `ValidateAIConfigAsync`  

### 8.3 配置变更与池重置

`RimTalkMemoryPatchMod.WriteSettings`：hash(`UseRimTalkAIConfig` + 各 ApiConfig 字段) 变化 → `AIService.ResetClientPool()`。

---

## 九、Embedding / 向量

- `EmbeddingService.cs` 移至 `Source/Memory/`（namespace 路径调整，逻辑原样）  
- 删除 `SiliconFlowEmbeddingService`  
- `VectorService` 不再回退通用 LLM ApiKey，仅用 `embeddingApiKey`  
- 向量功能视为废弃/低优先级，不随本期 AI 层主路径演进  

---

## 十、目录结构

```
Source/
  AI/
    AIService.cs
    AIClientFactory.cs
    Payload.cs
    Client/
      IAIClient.cs
      ClientPool.cs
      OpenAIClient.cs
      Dto/
        OpenAIRequest.cs
        OpenAIResponse.cs
  Integration/
    RimTalkApiConfigsGetter.cs   // 类名: RimTalkApiConfigGetter
  Settings/
    AIProvider.cs
    ApiConfig.cs
    SettingsUIDrawers.cs         // namespace RimTalk.Memory.UI
  Utils/
    JsonUtil.cs
    MessageUtil.cs
  Memory/
    EmbeddingService.cs          // 自 Memory/AI/ 迁出
  Maintenance/
    MemorySummarizer.cs          // 调用 AIService
  RimTalkMod.cs                  // hash + ResetClientPool
  RimTalkSettings.cs             // 字段 + DrawAIConfigSettings
```

---

## 十一、行为约定与已知限制

### 11.1 约定

| 项 | 行为 |
|----|------|
| 队列节流 | 现实时间 10s 一次出列 |
| 单客户端重试 | 最多 3 次尝试（间隔 4s、8s）；401/403/致命 4xx 立即失败 |
| 池级 fallback | 当前客户端失败后换下一有效配置 |
| 取消信号 | 请求循环中 `Current.Game == null` 则 abort（存档退出） |
| 验证 | 仅存档内；主菜单无按钮 |
| 实时校验 | UI 仅看 `IsValid`，统一文案 |
| 旧配置 | 不迁移 |

### 11.2 已知限制 / 可选后续

1. **`_instance` 出档不清理** — 出主菜单后仍指向旧组件，进新档覆盖；主菜单 UI 已挡验证。可对齐 `RoundMemoryManager` 在 `Current.Game == null` 时置空。  
2. **跟随 RimTalk 时独立表仍显示** — 有意，便于切换。  
3. **无 Player2 / Prompt Caching / Gemini 原生协议**。  
4. **工厂 RimTalk 缓存** — 仅在 `Reset` 换工厂时刷新；改 RimTalk 设置后需本模组 WriteSettings 或进档触发 reset 才一致。  
5. **成功但 content 为 null** — 仍可 `IsValid=true`；Summarizer 对空串按失败处理。  

### 11.3 手测建议

- [ ] 存档内：独立链单配置验证通过/失败  
- [ ] 存档内：多配置，首条失败后 fallback 到下一条  
- [ ] 跟随 RimTalk 配置，总结/归档入队并写回  
- [ ] 主菜单：无验证按钮，仅「仅存档内可用」  
- [ ] 启用行缺 model/URL 时红字提示  
- [ ] 改 ApiConfigs 后 WriteSettings 日志出现 client pool reset  
- [ ] 旧存档加载：无崩溃；需重填独立配置或开跟随  

---

## 附：与 v1.0 设计稿的主要差异

| v1.0 设想 | 实际实现 |
|-----------|----------|
| `CurrentApiConfigIndex` sticky 指针 | **无**；池 + 工厂顺序扫描 |
| `CascadingMemoryClient` / 复杂错误层 | `ClientPool` + 客户端内 retry |
| 旧字段迁移到 `ApiConfigs` | **不做迁移** |
| `WorldComponent` 队列 | **`GameComponent` `AIService`** |
| 主菜单可验证 | **仅存档内** |
| 细粒度 invalid reason key | **统一** `ApiConfigInvalid` |
| HttpWebRequest | **UnityWebRequest** |
| 独立 Google 原生协议 | **全部 OpenAI 兼容** |

本文 v2.0 以仓库当前源码为准；若代码再变，请同步改此文档。
