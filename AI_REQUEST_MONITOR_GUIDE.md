# AI请求监控器 - 使用说明

## 功能概述

AI请求监控器是一个强大的调试工具，可以**拦截、记录和查看**发送给AI的JSON数据包。

### 核心功能

✅ **实时拦截** - 自动捕获AI请求和响应
✅ **JSON格式化** - 美化显示JSON数据
✅ **历史记录** - 保存最近50条请求
✅ **文件导出** - 自动保存到文件
✅ **可视化界面** - 友好的查看器UI
✅ **敏感信息保护** - 自动隐藏API Key

---

## 使用方法

### 1. 打开监控器

**方式A：通过Mod设置**
1. 主菜单 → 选项 → Mod设置
2. 找到 RimTalk-ExpandMemory
3. 滚动到"其他设置"部分
4. 点击"打开AI请求监控器"

**方式B：进入游戏后**
1. 加载任意存档
2. 打开Mod设置
3. 点击"打开AI请求监控器"

### 2. 启用拦截

在监控器窗口中：
1. 点击"启用拦截"按钮
2. 状态显示为"✅ 已启用"
3. 开始监控AI请求

### 3. 触发AI对话

- 如果安装了RimTalk：让小人进行对话
- 如果使用记忆总结：等待每日总结触发
- 所有AI请求都会被自动记录

### 4. 查看请求详情

1. 左侧列表显示历史记录
2. 点击任意记录
3. 右侧显示完整详情：
   - 时间戳
   - 类型（请求/响应/错误）
   - 端点URL
   - Headers（API Key会隐藏）
   - 完整的JSON Payload

### 5. 导出数据

**单次导出：**
- 每个请求自动保存到文件
- 路径：`SaveData/AIRequestLogs/`

**批量导出：**
- 点击"导出历史"按钮
- 导出所有50条历史记录
- 自动打开文件夹

---

## 界面说明

### 监控器窗口布局

```
╔════════════════════════════════════════════════════════╗
║  AI请求监控器                                          ║
╠════════════════════════════════════════════════════════╣
║  ✅ 已启用  [禁用拦截] [清空历史] [导出历史]  记录: 15/50  ║
╠═════════════════════╦══════════════════════════════════╣
║  历史记录列表       ║  请求详情                        ║
║                     ║                                  ║
║  📤 Request         ║  时间：2024-01-15 14:30:25       ║
║  14:30:25          ║  类型：Request                   ║
║  /v1/chat          ║  端点：/v1/chat/completions      ║
║                     ║                                  ║
║  📥 Response        ║  Headers:                        ║
║  14:30:28          ║    Authorization: ***            ║
║  /v1/chat          ║    Content-Type: application/json║
║                     ║                                  ║
║  ❌ Error           ║  Payload:                        ║
║  14:32:10          ║  {                               ║
║  /v1/chat          ║    "model": "gpt-3.5-turbo",     ║
║                     ║    "messages": [...]             ║
║                     ║  }                               ║
╚═════════════════════╩══════════════════════════════════╝
```

### 控制按钮

| 按钮 | 功能 | 说明 |
|------|------|------|
| **启用拦截** | 开始监控 | 启用后会拦截所有AI请求 |
| **禁用拦截** | 停止监控 | 禁用后不会记录新请求 |
| **清空历史** | 清除记录 | 删除内存中的历史（文件不受影响） |
| **导出历史** | 批量导出 | 将所有历史导出到一个文件 |

### 记录类型

| 图标 | 类型 | 颜色 | 说明 |
|------|------|------|------|
| 📤 | Request | 蓝色 | 发送给AI的请求 |
| 📥 | Response | 绿色 | AI返回的响应 |
| ❌ | Error | 红色 | 请求失败或错误 |

---

## 文件位置

### 自动保存位置
```
SaveData/AIRequestLogs/
├── AI_Request_20240115_143025_Request.txt
├── AI_Request_20240115_143028_Response.txt
├── AI_Request_20240115_143210_Error.txt
└── AI_Request_History_20240115_150000.txt  (导出的历史)
```

### 文件内容示例

```
================================================================================
Timestamp: 2024-01-15 14:30:25
Type: Request
Endpoint: https://api.openai.com/v1/chat/completions

Headers:
  Authorization: ***REDACTED***
  Content-Type: application/json

Payload:
{
  "model": "gpt-3.5-turbo",
  "temperature": 1.0,
  "max_tokens": 1000,
  "messages": [
    {
      "role": "user",
      "content": "## 角色记忆\n\n### 当前状态\n- [对话] 刚才和John讨论了食物问题\n\n..."
    }
  ]
}
================================================================================
```

---

## 实际应用场景

### 场景1：调试记忆注入

**目的：** 查看动态注入的记忆是否正确

**步骤：**
1. 启用监控器
2. 触发一次对话
3. 查看Request的Payload
4. 检查`messages`中的内容
5. 确认记忆和常识是否注入

**示例：**
```json
{
  "messages": [
    {
      "role": "user",
      "content": "## 角色记忆\n\n### 当前状态\n- [对话] 与John讨论食物...\n\n### 近期经历\n- [行动] 建造了墙壁...\n\n## 背景常识\n\n- [规则] 食物会腐坏..."
    }
  ]
}
```

### 场景2：优化Token消耗

**目的：** 统计每次请求消耗的Token数量

**步骤：**
1. 记录多次请求
2. 查看Payload长度
3. 粗略估算token数（英文1 token ≈ 4字符，中文1 token ≈ 1-2字）
4. 调整注入数量和内容

### 场景3：排查API错误

**目的：** 找出为什么AI请求失败

**步骤：**
1. 启用监控器
2. 等待错误发生
3. 查看Error类型的记录
4. 检查错误消息和状态码
5. 根据错误调整配置

### 场景4：对比注入策略

**目的：** 比较静态注入vs动态注入的效果

**步骤：**
1. 启用动态注入，记录几次请求
2. 切换到静态注入，再记录几次
3. 导出历史
4. 对比两种策略注入的内容
5. 选择更好的策略

---

## 技术细节

### 拦截机制

#### 自动拦截（Harmony Patch）
```
RimTalk.Service.AIService
    ↓
[Harmony Prefix]
    ↓
AIRequestInterceptor.LogRequest()
    ↓
[原方法执行]
    ↓
[Harmony Postfix]
    ↓
AIRequestInterceptor.LogResponse()
```

**限制：**
- ⚠️ 不支持异步方法（async/await）
- ⚠️ 不支持流式方法（Streaming）
- ⚠️ 如果RimTalk使用这些方法，自动拦截会失败

#### 手动记录（备选方案）
如果自动拦截失败，可以在代码中手动记录：

```csharp
// 在发送请求前
AIRequestInterceptor.LogRequest(endpoint, jsonPayload, headers);

// 在收到响应后
AIRequestInterceptor.LogResponse(endpoint, jsonResponse, statusCode);

// 在发生错误时
AIRequestInterceptor.LogError(endpoint, errorMessage, exception);
```

### JSON格式化

使用简单的状态机进行格式化：
- 自动缩进（2空格）
- 保留字符串内容
- 处理转义字符
- 限制显示长度（5000字符）

### 性能优化

- **内存限制：** 最多保存50条历史
- **自动清理：** 超过限制自动删除最旧的
- **惰性加载：** 只在打开窗口时渲染
- **文件异步：** 保存文件不阻塞主线程

---

## 常见问题

### Q1: 为什么没有记录任何请求？

**可能原因：**
1. ❌ 拦截器未启用 → 点击"启用拦截"
2. ❌ RimTalk使用异步方法 → 自动拦截不支持
3. ❌ 没有触发AI对话 → 让小人对话或等待总结

**解决方法：**
- 检查状态是否为"✅ 已启用"
- 查看日志：`[AI Interception Patch]`
- 如果显示"likely async"，则需要手动记录

### Q2: API Key会被记录吗？

**答：** 不会明文记录

- UI显示：`Authorization: ***`
- 文件保存：`Authorization: ***REDACTED***`
- 所有包含"key"或"token"的Header都会被隐藏

### Q3: 文件保存在哪里？

**答：** SaveData文件夹

- 完整路径：`C:\Users\[用户名]\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\SaveData\AIRequestLogs\`
- 点击"导出历史"会自动打开此文件夹

### Q4: 可以删除旧文件吗？

**答：** 可以

- 文件只用于查看，不影响游戏
- 可以随时删除整个`AIRequestLogs`文件夹
- 下次拦截会自动重新创建

### Q5: 影响性能吗？

**答：** 几乎没有影响

- 禁用时：完全不影响
- 启用时：每次请求约1-2ms开销
- 只在触发AI请求时才工作
- 文件保存是异步的

### Q6: 支持哪些AI服务？

**答：** 理论上支持所有

目前测试过：
- ✅ OpenAI (GPT-3.5, GPT-4)
- ✅ Google (Gemini)
- ⚠️ 其他服务未测试

RimTalk使用的服务都会被拦截。

### Q7: 可以拦截Mod自己的AI请求吗？

**答：** 可以

如果使用`IndependentAISummarizer`，需要在代码中手动添加：

```csharp
// 在发送请求前
if (AIRequestInterceptor.IsEnabled)
{
    AIRequestInterceptor.LogRequest(apiUrl, jsonPayload);
}

// 在收到响应后
if (AIRequestInterceptor.IsEnabled)
{
    AIRequestInterceptor.LogResponse(apiUrl, response);
}
```

---

## 开发者指南

### 手动集成

如果您是Mod开发者，想在自己的代码中使用拦截器：

```csharp
using RimTalk.Memory.Debug;

// 检查是否启用
if (AIRequestInterceptor.IsEnabled)
{
    // 记录请求
    var headers = new Dictionary<string, string>
    {
        { "Authorization", "Bearer " + apiKey },
        { "Content-Type", "application/json" }
    };
    
    AIRequestInterceptor.LogRequest(
        endpoint: "https://api.openai.com/v1/chat/completions",
        jsonPayload: requestJson,
        headers: headers
    );
    
    try
    {
        // 发送实际请求
        var response = SendRequest(...);
        
        // 记录响应
        AIRequestInterceptor.LogResponse(
            endpoint: "https://api.openai.com/v1/chat/completions",
            jsonResponse: responseJson,
            statusCode: 200
        );
    }
    catch (Exception ex)
    {
        // 记录错误
        AIRequestInterceptor.LogError(
            endpoint: "https://api.openai.com/v1/chat/completions",
            errorMessage: ex.Message,
            exception: ex
        );
    }
}
```

### API参考

#### AIRequestInterceptor

**静态方法：**

| 方法 | 参数 | 说明 |
|------|------|------|
| `Enable()` | 无 | 启用拦截器 |
| `Disable()` | 无 | 禁用拦截器 |
| `IsEnabled` | 属性 | 获取启用状态 |
| `LogRequest()` | endpoint, json, headers | 记录请求 |
| `LogResponse()` | endpoint, json, status | 记录响应 |
| `LogError()` | endpoint, error, exception | 记录错误 |
| `ClearHistory()` | 无 | 清空历史 |
| `ExportHistory()` | 无 | 导出历史 |

---

## 最佳实践

### 1. 只在需要时启用

- ❌ 不要一直开着
- ✅ 调试时才启用
- ✅ 调试完毕后禁用

### 2. 定期清理文件

- 定期删除旧的日志文件
- 避免占用过多磁盘空间
- 保留重要的记录

### 3. 导出重要数据

- 发现有价值的记录时立即导出
- 历史只保存50条，会被覆盖
- 文件是永久的

### 4. 分析注入效果

- 对比不同配置的Payload
- 优化注入数量和内容
- 减少无关信息

### 5. 保护隐私

- 不要分享包含API Key的文件
- 导出的文件已隐藏敏感信息
- 但还是要小心

---

## 未来改进

### 计划功能

- [ ] 请求重放（重新发送历史请求）
- [ ] Token计数器（自动统计消耗）
- [ ] 请求对比（diff两个请求）
- [ ] 过滤器（按类型、时间、内容筛选）
- [ ] 实时监控（WebSocket支持）
- [ ] 统计图表（可视化分析）

---

## 故障排除

### 拦截器无法启用

**症状：** 点击"启用拦截"无效

**检查：**
1. 是否在游戏中？（需要加载存档）
2. 查看日志是否有错误
3. 尝试重启游戏

**解决：**
- 确保已进入游戏（不在主菜单）
- 检查Mod是否正确加载
- 查看日志文件

### 文件无法保存

**症状：** 拦截成功但文件为空或不存在

**检查：**
1. SaveData文件夹是否存在？
2. 是否有写入权限？
3. 磁盘空间是否充足？

**解决：**
- 手动创建`SaveData/AIRequestLogs/`文件夹
- 以管理员身份运行游戏
- 清理磁盘空间

### JSON显示乱码

**症状：** Payload显示为乱码

**原因：** 字符编码问题

**解决：**
- 使用支持UTF-8的文本编辑器打开文件
- 推荐：Notepad++, VS Code, Sublime Text

---

**构建状态：** ✅ 成功  
**版本：** v1.0  
**文档更新：** 2024
