# Prompt Caching 实现指南（模型端缓存）

## ?? **概述**

**Prompt Caching** 是AI模型提供商（如OpenAI、DeepSeek、Google）提供的显式缓存功能，可以将重复的prompt前缀缓存到模型端，从而：

- ? **降低50%费用**：缓存命中的token按50%价格计费
- ? **提升响应速度**：跳过重复token的处理
- ? **自动管理**：模型端自动维护缓存（有效期5-10分钟）

---

## ?? **核心思路**

### 当前prompt结构
```
完整prompt = 固定system指令 + 变化的记忆/常识 + 用户输入
```

### 优化后结构
```
system消息（可缓存） = 固定指令（如角色设定、规则）
user消息（不缓存） = 记忆注入 + 常识注入 + 用户对话
```

**效果**：
- 每个殖民者的固定指令只在首次调用时计费
- 后续对话（5-10分钟内）缓存命中，费用降低50%

---

## ?? **实现方案**

### 方案1：OpenAI/DeepSeek 缓存

修改 `IndependentAISummarizer.cs` 的 `BuildJsonRequest()` 方法：

```csharp
// OpenAI: 在system消息中添加cache_control
sb.Append("{\"role\":\"system\",");
sb.Append("\"content\":\"" + systemPrompt + "\"");
sb.Append(",\"cache_control\":{\"type\":\"ephemeral\"}");  // ?? 关键
sb.Append("},");

// DeepSeek: 添加cache标记和全局开关
sb.Append(",\"cache\":true");  // 消息级别
sb.Append(",\"enable_prompt_cache\":true");  // 请求级别
```

### 方案2：Google Gemini Context Caching ? 新增

Google Gemini 使用 **Context Caching** 机制，与 OpenAI/DeepSeek 不同：

```csharp
// Gemini: 在generationConfig中启用缓存
{
  "contents": [...],
  "generationConfig": {
    "temperature": 0.7,
    "maxOutputTokens": 200
  },
  "cachedContent": "projects/{project}/locations/{location}/cachedContents/{id}"
}
```

**Gemini 缓存特点**：
- ? 支持缓存（Context Caching）
- ?? 需要先创建 CachedContent 对象（较复杂）
- ?? 免费版（AI Studio）缓存功能有限
- ?? 本项目暂不实现 Gemini 缓存（复杂度高，收益有限）

---

## ?? **性能评估**

### 典型场景（GPT-4 Turbo）

| 场景 | 首次调用 | 缓存命中 | 节省 |
|-----|---------|---------|------|
| 记忆总结（200 tokens） | $0.006 | **$0.003** | 50% |
| 对话生成（500 tokens） | $0.015 | **$0.0075** | 50% |

### 每日费用估算（10个殖民者，每人5次对话）

| 缓存状态 | 每日费用 | 月度费用 |
|---------|---------|---------|
| 无缓存 | $0.75 | $22.5 |
| 启用缓存 | **$0.40** | **$12** |
| **节省** | **$0.35/天** | **$10.5/月** |

---

## ?? **注意事项**

### 1. 缓存有效期
- OpenAI：5-10分钟（自动过期）
- DeepSeek：约10分钟
- Google Gemini：可配置（需要Vertex AI）
- **建议**：高频对话时效果最佳（如游戏中连续对话）

### 2. 兼容性
- ? OpenAI GPT-4 Turbo、GPT-3.5 Turbo（自动支持）
- ? DeepSeek Chat、Coder（自动支持）
- ?? Google Gemini（支持但实现复杂，本项目暂不实现）

### 3. 调试
查看响应头中的缓存信息：
```
X-Cache-Control-Stats: hit=1, miss=0, token_savings=150
```

### 4. 最佳实践
- system prompt保持简洁（<500 tokens）
- 避免频繁修改system内容
- 记忆/常识注入放在user消息中

---

## ?? **测试验证**

### 步骤1：启用DevMode日志
```csharp
if (Prefs.DevMode)
{
    Log.Message($"[AI] Cache enabled: {useCaching}");
    Log.Message($"[AI] System tokens: {systemPrompt.Length / 4}"); // 粗略估算
}
```

### 步骤2：观察API响应
- 首次调用：`usage.cached_tokens = 0`
- 后续调用：`usage.cached_tokens > 0` ?

### 步骤3：费用对比
记录一周的API费用，对比优化前后。

---

## ?? **配置选项**

在 `RimTalkSettings.cs` 中已添加开关：

```csharp
public bool enablePromptCaching = true;  // 启用Prompt Caching（默认开启）
```

在设置UI中（AI配置区域）：
```
?? 启用Prompt Caching（降低50%费用）
```

**注意**：此选项仅对 OpenAI 和 DeepSeek 生效，Google Gemini 暂不支持。

---

## ?? **部署建议**

### 阶段1：测试环境
1. 修改 `BuildJsonRequest()`
2. 使用DevMode验证JSON格式
3. 单次API调用测试

### 阶段2：生产环境
1. 关闭DevMode日志
2. 监控缓存命中率
3. 观察费用变化

### 阶段3：优化
1. 调整system prompt长度
2. 分析缓存失效原因
3. 微调缓存策略

---

## ?? **参考文档**

- [OpenAI Prompt Caching](https://platform.openai.com/docs/guides/prompt-caching)
- [DeepSeek API文档](https://platform.deepseek.com/api-docs)
- [Google Gemini Context Caching](https://ai.google.dev/gemini-api/docs/caching)
- [本项目issue讨论](https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/issues)

---

## ?? **支持**

如有问题，请提交issue或联系开发者。

**祝您降本增效！** ??
