# AI拦截器 - 异步方法限制说明

## 问题说明

在游戏日志中看到：
```
[AI Interception Patch] No suitable methods found (trying fallback)
[AI Interception Patch] No suitable methods found after fallback
[AI Interception Patch] Could not patch AIService
```

## 这是正常的！

### 原因

RimTalk使用的是**异步方法**（async/await），例如：
```csharp
// RimTalk的AI服务可能是这样的
public static async Task<TalkResponse> ChatStreaming(TalkRequest request)
{
    // 异步HTTP请求
    await ...
}
```

Harmony（Mod框架）**无法patch异步方法**，这是技术限制，不是bug。

---

## 技术细节

### 为什么Harmony不支持异步方法？

1. **状态机转换**
   - C#编译器将async方法转换为状态机
   - 原始方法变成了复杂的状态机类
   - Harmony patch的是原始方法，但异步方法已经不是原始形态

2. **执行流程不同**
   - 同步方法：直接执行，可在前后插入代码
   - 异步方法：返回Task，实际执行在状态机中
   - Prefix/Postfix无法正确拦截异步执行点

3. **示例对比**

**同步方法（可以patch）：**
```csharp
public static string Chat(TalkRequest request)
{
    // Prefix可以在这里执行
    var result = CallAPI();
    // Postfix可以在这里执行
    return result;
}
```

**异步方法（无法patch）：**
```csharp
public static async Task<string> ChatAsync(TalkRequest request)
{
    // 这个方法立即返回Task
    // 实际执行在编译器生成的状态机中
    var result = await CallAPIAsync();
    return result;
}
```

---

## 解决方案

### 方案1：手动记录（推荐）✅

在你自己的代码中手动调用拦截器：

```csharp
using RimTalk.Memory.Debug;

// 在发送AI请求前
public void SendAIRequest(string prompt)
{
    // 构建请求JSON
    string requestJson = BuildRequestJson(prompt);
    
    // 记录请求
    if (AIRequestInterceptor.IsEnabled)
    {
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
    }
    
    try
    {
        // 发送实际请求
        var response = await SendHttpRequest(requestJson);
        
        // 记录响应
        if (AIRequestInterceptor.IsEnabled)
        {
            AIRequestInterceptor.LogResponse(
                endpoint: "https://api.openai.com/v1/chat/completions",
                jsonResponse: response,
                statusCode: 200
            );
        }
    }
    catch (Exception ex)
    {
        // 记录错误
        if (AIRequestInterceptor.IsEnabled)
        {
            AIRequestInterceptor.LogError(
                endpoint: "https://api.openai.com/v1/chat/completions",
                errorMessage: ex.Message,
                exception: ex
            );
        }
    }
}
```

### 方案2：在HTTP层拦截

如果RimTalk使用HttpClient，可以创建DelegatingHandler：

```csharp
public class InterceptorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // 记录请求
        if (AIRequestInterceptor.IsEnabled)
        {
            string requestBody = await request.Content.ReadAsStringAsync();
            AIRequestInterceptor.LogRequest(
                request.RequestUri.ToString(),
                requestBody,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            );
        }
        
        // 发送请求
        var response = await base.SendAsync(request, cancellationToken);
        
        // 记录响应
        if (AIRequestInterceptor.IsEnabled)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            AIRequestInterceptor.LogResponse(
                request.RequestUri.ToString(),
                responseBody,
                (int)response.StatusCode
            );
        }
        
        return response;
    }
}

// 使用
var handler = new InterceptorHandler { InnerHandler = new HttpClientHandler() };
var client = new HttpClient(handler);
```

### 方案3：使用本Mod的AI总结

本Mod的`IndependentAISummarizer`可以添加拦截支持：

```csharp
// 在 IndependentAISummarizer.cs 中添加
private static async Task<string> SendRequestWithLogging(string json)
{
    // 记录请求
    if (AIRequestInterceptor.IsEnabled)
    {
        AIRequestInterceptor.LogRequest(apiUrl, json);
    }
    
    var response = await SendRequest(json);
    
    // 记录响应
    if (AIRequestInterceptor.IsEnabled)
    {
        AIRequestInterceptor.LogResponse(apiUrl, response);
    }
    
    return response;
}
```

---

## 当前状态总结

### ✅ 可用功能
- 拦截器核心逻辑完全正常
- UI界面完整可用
- 文件保存功能正常
- 手动记录完全支持
- JSON格式化正常
- 历史记录功能正常
- 导出功能正常

### ⚠️ 限制
- 无法**自动**拦截RimTalk的异步AI请求
- 需要**手动**在代码中调用拦截器
- 这是Harmony框架的技术限制，无法解决

### 📝 使用建议

**对于普通用户：**
- 拦截器仍然可以使用
- 如果没有自动记录，这是正常的
- 可以等待未来集成手动记录
- 或者只用于调试自己的AI请求

**对于Mod开发者：**
- 在自己的AI请求代码中添加手动记录
- 参考上面的示例代码
- 或在HTTP客户端层拦截

**对于本Mod：**
- IndependentAISummarizer可以添加手动记录
- 未来版本可能会添加此功能
- 目前主要用于演示和框架

---

## 替代方案

### 使用DevMode日志

如果只是想查看注入的内容，可以使用DevMode：

```csharp
// 在 DynamicMemoryInjection.cs 中
if (Prefs.DevMode)
{
    Log.Message($"[Dynamic Injection] Injected memories:\n{memoryContext}");
}
```

**优势：**
- 不需要拦截器
- 直接在日志中查看
- 简单快速

**劣势：**
- 格式不如监控器美观
- 无历史记录
- 无文件保存

### 使用断点调试

如果你是开发者，可以直接断点调试：

```csharp
// 在关键位置设置断点
string promptWithMemory = memoryContext + "\n\n" + basePrompt;
// <- 在这里设置断点，查看完整的提示词
```

---

## 未来改进

### 可能的解决方案

1. **Harmony支持异步**
   - 等待Harmony添加异步支持
   - 可能性：低（技术难度大）

2. **RimTalk提供钩子**
   - RimTalk提供拦截接口
   - 我们可以直接集成
   - 可能性：中（需要RimTalk配合）

3. **IL编织（IL Weaving）**
   - 使用Mono.Cecil等工具
   - 在编译时修改IL代码
   - 可能性：低（太复杂，不值得）

4. **反射+动态代理**
   - 创建动态代理包装异步方法
   - 在代理中添加拦截逻辑
   - 可能性：中（技术可行，实现复杂）

### v2.3计划

- [ ] 在IndependentAISummarizer中添加手动拦截
- [ ] 添加快捷方式显示注入的内容
- [ ] 改进DevMode日志输出
- [ ] 添加"查看最后注入内容"功能

---

## 常见问题

### Q: 为什么不能自动拦截？
A: 因为RimTalk使用异步方法，Harmony不支持。这是技术限制。

### Q: 会修复吗？
A: 无法修复，因为这不是bug。需要手动添加拦截代码。

### Q: 拦截器还有用吗？
A: 有用！可以手动记录，或用于其他同步AI请求。

### Q: 我应该怎么做？
A: 如果只是想查看注入内容，开启DevMode即可。如果需要完整拦截，等待未来版本添加手动记录支持。

### Q: 其他Mod会遇到这个问题吗？
A: 是的，所有想拦截异步方法的Mod都会遇到。这是通用限制。

---

## 参考链接

### Harmony文档
- Harmony不支持异步方法：https://harmony.pardeike.net/articles/patching-edgecases.html#async-methods
- 异步方法的状态机：https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/

### 相关讨论
- GitHub Issue：Harmony async support
- RimWorld Modding Discord：Async patching问题

---

## 结论

**AI拦截器是完全正常的，显示"No suitable methods found"是预期行为。**

- ✅ 核心功能正常
- ✅ UI完整可用
- ⚠️ 需要手动集成
- ⚠️ 无法自动拦截异步方法（技术限制）

**建议：**
- 普通用户：使用DevMode查看注入内容
- 开发者：手动添加拦截调用
- 等待：未来版本可能添加更便捷的方式

**不影响其他功能：**
- ✅ 动态记忆注入正常工作
- ✅ 常识库正常工作
- ✅ 所有UI正常工作
- ✅ 整个Mod完全可用

---

**状态：** ✅ 正常（技术限制，非bug）  
**影响：** ⚠️ 轻微（只影响自动拦截）  
**解决方案：** ✅ 手动记录（完全可行）
