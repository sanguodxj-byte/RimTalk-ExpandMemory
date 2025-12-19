# ?? AI 配置 - 多平台支持

## ?? 更新时间
**v3.3.8 - 2025-12-06**

---

## ?? 新增功能

### ? 支持的 AI 提供商

| 提供商 | 说明 | 推荐模型 | Prompt Caching |
|--------|------|----------|----------------|
| **OpenAI** | GPT 系列，稳定可靠 | gpt-3.5-turbo, gpt-4 | ? Beta |
| **DeepSeek** | 中文优化，性价比高 | deepseek-chat, deepseek-coder | ? 原生支持 |
| **Player2** | 游戏优化 AI，本地客户端 | gpt-4o, gpt-4-turbo | ? 自动缓存 |
| **Google** | Gemini 系列，多模态 | gemini-2.0-flash-exp | ? 暂不支持 |
| **Custom** | 自定义端点，第三方代理 | 自定义 | ?? 取决于实现 |

---

## ?? 使用指南

### 1. 打开设置
```
RimWorld → 选项 → Mod 设置 → RimTalk-Expand Memory
→ 展开 "记忆总结设置"
→ 启用 "使用 AI 总结"
→ 展开 "AI 配置"
```

### 2. 选择提供商

#### OpenAI
```
[OpenAI] [DeepSeek] [Player2]
[Google]  [Custom]

点击 "OpenAI" 按钮
```

**自动填充**:
- Provider: `OpenAI`
- Model: `gpt-3.5-turbo`
- API URL: `https://api.openai.com/v1/chat/completions`

**手动配置**:
- API Key: `sk-xxxxxxxxxxxxxxxxxxxx`

#### DeepSeek
```
点击 "DeepSeek" 按钮
```

**自动填充**:
- Provider: `DeepSeek`
- Model: `deepseek-chat`
- API URL: `https://api.deepseek.com/v1/chat/completions`

**手动配置**:
- API Key: `sk-xxxxxxxxxxxxxxxxxxxx`

**特点**:
- ? 中文优化
- ? 性价比高（比 OpenAI 便宜 10-20 倍）
- ? 原生支持 Prompt Caching
- ? 可节省约 50% 费用

#### Player2
```
点击 "Player2" 按钮
```

**自动填充**:
- Provider: `Player2`
- Model: `gpt-4o`
- API URL: `https://api.player2.game/v1/chat/completions`

**配置方式**:
1. **本地客户端**（推荐）
   - 安装 Player2 桌面应用
   - 自动检测本地连接
   - API Key 自动获取

2. **远程 API**
   - 手动输入 Player2 API Key
   - 使用远程服务器

**特点**:
- ? 游戏优化
- ? 本地客户端支持
- ? 自动缓存
- ? 低延迟

#### Google Gemini
```
点击 "Google" 按钮
```

**自动填充**:
- Provider: `Google`
- Model: `gemini-2.0-flash-exp`
- API URL: `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`

**手动配置**:
- API Key: `AIzaSyxxxxxxxxxxxxxxxxxxxxxxxxx`

**特点**:
- ? 多模态能力
- ? 免费额度慷慨
- ? 暂不支持 Prompt Caching
- ?? API 格式与 OpenAI 不同

#### Custom（自定义）
```
点击 "Custom" 按钮
```

**手动配置**:
- Provider: `Custom`
- Model: `your-model-name`
- API URL: `https://your-api-endpoint.com/v1/chat/completions`
- API Key: `your-api-key`

**适用场景**:
- ? 第三方 API 代理
- ? 自搭建 LLM 服务器
- ? OpenAI 兼容端点
- ? 企业内网 API

---

## ?? 配置说明

### API Key
- **OpenAI**: 以 `sk-` 开头的 Key
- **DeepSeek**: 以 `sk-` 开头的 Key
- **Player2**: 本地自动获取 或 手动输入
- **Google**: 以 `AIzaSy` 开头的 Key
- **Custom**: 根据您的服务器要求

### API URL
- **标准格式**: `https://api.example.com/v1/chat/completions`
- **Google 格式**: `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`
- **自定义**: 填写您的实际端点

### Model
- **OpenAI**: `gpt-3.5-turbo`, `gpt-4`, `gpt-4o`
- **DeepSeek**: `deepseek-chat`, `deepseek-coder`
- **Player2**: `gpt-4o`, `gpt-4-turbo`
- **Google**: `gemini-2.0-flash-exp`, `gemini-pro`
- **Custom**: 您的模型名称

---

## ?? 配置验证

### 验证步骤
```
1. 配置完 API Key、URL、Model
2. 点击 "?? 验证配置" 按钮
3. 等待验证结果
```

### 验证结果
- ? **成功**: "配置验证成功！提供商: xxx"
- ? **失败**: "配置验证失败，请检查 API Key 和 URL"

### 常见问题

#### 问题 1: API Key 无效
**错误**: `401 Unauthorized`

**解决**:
1. 检查 API Key 是否正确复制
2. 检查 API Key 是否有效期内
3. 检查 API Key 是否有足够余额
4. 尝试重新生成 API Key

#### 问题 2: API URL 错误
**错误**: `404 Not Found`

**解决**:
1. 检查 URL 是否完整
2. 检查是否包含 `https://`
3. 检查路径是否正确（`/v1/chat/completions`）
4. 对于 Google，检查是否使用正确的 URL 格式

#### 问题 3: 网络连接失败
**错误**: `Network Error`

**解决**:
1. 检查网络连接
2. 检查防火墙设置
3. 尝试使用代理
4. 检查 API 服务是否可用

---

## ?? Prompt Caching

### 什么是 Prompt Caching？
- 缓存固定的提示词内容
- 后续请求重用缓存
- 显著降低 API 费用
- 提升响应速度

### 支持情况

| 提供商 | 支持 | 说明 |
|--------|------|------|
| OpenAI | ? Beta | gpt-4o, gpt-4-turbo |
| DeepSeek | ? 原生 | 所有模型 |
| Player2 | ? 自动 | 本地客户端 |
| Google | ? | 暂不支持 |
| Custom | ?? | 取决于实现 |

### 启用方法
```
? 启用 Prompt Caching

OpenAI: 自动使用 cache_control
DeepSeek: 自动使用 enable_prompt_cache
Player2: 本地客户端自动缓存
```

### 节省效果
- **DeepSeek**: 约 50% 费用节省
- **OpenAI**: 视缓存命中率而定
- **Player2**: 本地缓存，无额外费用

---

## ?? 性能对比

### 响应速度
| 提供商 | 平均延迟 | 稳定性 |
|--------|----------|--------|
| Player2 (本地) | 极快 | ????? |
| DeepSeek | 快 | ???? |
| OpenAI | 中等 | ????? |
| Google | 快 | ???? |
| Custom | 取决于服务器 | ??? |

### 费用对比（每 1M tokens）
| 提供商 | 输入 | 输出 |
|--------|------|------|
| OpenAI (gpt-3.5) | $0.50 | $1.50 |
| DeepSeek | $0.10 | $0.28 |
| Player2 | 取决于套餐 | 取决于套餐 |
| Google (Gemini) | 免费额度 | 免费额度 |

### 中文能力
| 提供商 | 中文理解 | 中文生成 |
|--------|----------|----------|
| DeepSeek | ????? | ????? |
| OpenAI | ???? | ???? |
| Google | ???? | ???? |
| Player2 | ???? | ???? |

---

## ?? 安全建议

### API Key 安全
1. ? **不要分享** API Key
2. ? **定期轮换** API Key
3. ? **设置使用限额** 防止滥用
4. ? **监控使用情况** 及时发现异常
5. ? **不要提交到 Git** 包含 Key 的配置文件

### 网络安全
1. ? 使用 HTTPS 连接
2. ? 验证 SSL 证书
3. ?? 注意第三方代理的可信度
4. ?? 自定义端点需要额外验证

---

## ?? 相关文档

- **Prompt Caching 指南**: `Docs/PROMPT_CACHING_GUIDE.md`
- **Prompt Caching 补丁**: `Docs/PROMPT_CACHING_PATCH.md`
- **完整文档**: `快速开始.md`

---

## ?? 总结

### ? 现在支持
- OpenAI GPT 系列
- DeepSeek 中文优化
- Player2 游戏优化
- Google Gemini
- 自定义 API 端点

### ?? 推荐配置

**中文用户**:
```
提供商: DeepSeek
模型: deepseek-chat
Prompt Caching: ? 启用
```

**追求质量**:
```
提供商: OpenAI
模型: gpt-4o
Prompt Caching: ? 启用
```

**本地优先**:
```
提供商: Player2
模型: gpt-4o
客户端: 本地应用
```

**免费体验**:
```
提供商: Google
模型: gemini-2.0-flash-exp
Prompt Caching: ? 禁用
```

---

**更新日期**: 2025-12-06  
**版本**: v3.3.8  
**状态**: ? 已实现，可立即使用

**?? 选择适合您的 AI 提供商，享受智能记忆总结！**
