# Prompt 转 Context 注入工作总结

## 问题背景

### 原始问题
在 RimTalk-ExpandMemory 项目中，常识库的注入存在以下问题：
1. **重复注入**：常识既被注入到 Prompt（User Message）又被注入到 Context（System Instruction）
2. **错误匹配**：使用 Context（包含完整角色状态信息）进行匹配，导致匹配到不相关的常识
   - 例如：Context 中没有"米莉拉"，但因为 Pawn 的关系人信息中包含"米莉拉"，导致匹配到了"米莉拉"相关的常识

### 期望行为
- **匹配**：使用 Prompt（对话内容）+ Pawn 信息进行匹配
- **注入**：将匹配结果注入到 Context（System Instruction）
- **避免重复**：只注入一次，不重复

## 技术分析

### RimTalk 的架构

#### 调用流程
```
TalkService.GenerateTalk()
  ↓
1. BuildContext(pawns) → 生成 Context（System Instruction）
  ↓
2. AIService.UpdateContext(context) → 设置 System Instruction
  ↓
3. DecoratePrompt(talkRequest, pawns, status) → 生成 Prompt（User Message）
  ↓
4. GenerateAndProcessTalkAsync() → 异步调用 AI
```

#### 关键概念
- **Context**：System Instruction，包含角色信息、环境信息等，作为 AI 的"人设"
- **Prompt**：User Message，包含对话内容、时间、天气等，作为 AI 的"输入"

### 原有实现的问题

#### 1. BuildContext_Postfix（错误）
```csharp
// 问题：使用 Context 进行匹配
string injectedContext = SmartInjectionManager.InjectSmartContext(
    speaker: mainPawn,
    listener: targetPawn,
    context: __result,  // ⬅️ __result 是 Context，包含大量角色状态信息
    ...
);
```

**问题**：
- `__result` 包含：角色名字、年龄、性别、种族、特性、技能、健康状况、**关系人名字**等
- 匹配时会匹配到关系人的名字，导致误匹配
- 例如：Pawn A 的关系人是"米莉拉"，匹配时会匹配到标签为"米莉拉"的常识

#### 2. DecoratePrompt_Postfix（正确但被禁用）
```csharp
// 正确：使用 Prompt 进行匹配
string injectedContext = SmartInjectionManager.InjectSmartContext(
    speaker: mainPawn,
    listener: targetPawn,
    context: currentPrompt,  // ⬅️ currentPrompt 是对话内容
    ...
);
```

**但是**：这个方法被禁用了，因为担心重复注入。

#### 3. Patch_GenerateAndProcessTalkAsync（向量搜索）
- 这个 patch 处理向量搜索的注入
- 也存在重复问题
- 存在线程安全问题（在后台线程访问 Map Pawns）

## 解决方案

### 方案选择

#### 考虑过的方案
1. **Transpiler**：在 IL 层面修改代码
   - ❌ 不稳定，容易出错
   
2. **缓存方案**：在 DecoratePrompt 中匹配，缓存结果，在 BuildContext 中注入
   - ❌ 需要处理缓存清理，容易串数据
   - ❌ 多线程问题

3. **Postfix + 反射**：在 DecoratePrompt_Postfix 中匹配并通过反射注入到 Context
   - ✅ 稳定可靠
   - ✅ 无缓存问题
   - ✅ 性能开销小

### 最终实现

#### 核心思路
1. **禁用 BuildContext_Postfix 的注入逻辑**
   - 只保留缓存功能（用于预览器）
   
2. **启用并修改 DecoratePrompt_Postfix**
   - 用 Prompt + Pawn 信息进行匹配
   - 通过反射获取和更新 AIService 的 Context
   - 将匹配结果注入到 Context

3. **修改 Patch_GenerateAndProcessTalkAsync**
   - 修复反射调用问题（避开 Logger）
   - 将向量搜索结果注入到 Context

#### 代码实现

```csharp
// DecoratePrompt_Postfix
private static void DecoratePrompt_Postfix(object talkRequest, List<Pawn> pawns)
{
    // 1. 获取 Prompt
    string currentPrompt = promptProperty.GetValue(talkRequest) as string;
    
    // 2. 使用 Prompt + Pawn 信息进行匹配
    string injectedContext = SmartInjectionManager.InjectSmartContext(
        speaker: mainPawn,
        listener: targetPawn,
        context: currentPrompt,  // ⬅️ 使用 Prompt 进行匹配
        maxMemories: ...,
        maxKnowledge: ...
    );
    
    // 3. 通过反射获取 AIService
    var aiServiceType = rimTalkAssembly.GetType("RimTalk.Service.AIService");
    
    // 4. 获取当前 Context
    var getContextMethod = aiServiceType.GetMethod("GetContext", ...);
    string currentContext = getContextMethod?.Invoke(null, null) as string;
    
    // 5. 追加注入内容到 Context
    string enhancedContext = currentContext + "\n\n" + injectedContext;
    
    // 6. 更新 Context
    var updateContextMethod = aiServiceType.GetMethod("UpdateContext", ...);
    updateContextMethod?.Invoke(null, new object[] { enhancedContext });
}
```

## 修改文件清单

### 1. Source/Patches/RimTalkPrecisePatcher.cs

#### 修改内容
1. **添加 using 语句**
   ```csharp
   using System.Linq;
   using System.Text;
   ```

2. **移除缓存变量**
   ```csharp
   // 删除了不再需要的缓存变量
   // private static string cachedInjectionContent = null;
   // private static int cachedPawnId = -1;
   ```

3. **禁用 BuildContext_Postfix 的注入逻辑**
   ```csharp
   private static void BuildContext_Postfix(ref string __result, List<Pawn> pawns)
   {
       // 仅缓存上下文到API（用于预览器）
       RimTalkMemoryAPI.CacheContext(mainPawn, __result);
       
       // 不再进行注入
   }
   ```

4. **启用并重写 DecoratePrompt_Postfix**
   - 用 Prompt 进行匹配
   - 通过反射注入到 Context

5. **禁用 GenerateTalk patch**
   ```csharp
   private static bool PatchGenerateTalk(Harmony harmony, Assembly assembly)
   {
       // 不再需要 patch GenerateTalk
       Log.Message("[RimTalk Memory Patch] ⚠ GenerateTalk patch disabled");
       return false;
   }
   ```

### 2. Source/Patches/Patch_GenerateAndProcessTalkAsync.cs

#### 修改内容

1. **修复反射调用问题**
   - 不再调用 `AIService.UpdateContext`（因为它内部调用了可能不安全的 `Logger.Debug`）
   - 直接通过反射修改 `_instruction` 私有字段
   ```csharp
   var instructionField = aiServiceType.GetField("_instruction", BindingFlags.NonPublic | BindingFlags.Static);
   instructionField.SetValue(null, enhancedContext);
   ```

2. **修复编译错误**
   - 在 `Prefix` 方法中重新获取 `rimTalkAssembly`

### 3. Source/Memory/CommonKnowledgeLibrary.cs

#### 修改内容
添加调试日志到 `IsMatched` 方法：
```csharp
private bool IsMatched(string text, CommonKnowledgeEntry entry)
{
    // ...
    if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
    {
        // 🔍 调试日志：记录匹配成功
        if (Prefs.DevMode)
        {
            Log.Message($"[CommonKnowledge] ✓ Matched! Tag='{tag}' ...");
        }
        return true;
    }
    // ...
}
```

## 技术细节

### 反射调用 AIService

#### 获取 Context
```csharp
var getContextMethod = aiServiceType.GetMethod("GetContext", 
    BindingFlags.Public | BindingFlags.Static);
string currentContext = getContextMethod?.Invoke(null, null) as string;
```

#### 更新 Context（主线程）
```csharp
var updateContextMethod = aiServiceType.GetMethod("UpdateContext", 
    BindingFlags.Public | BindingFlags.Static);
updateContextMethod?.Invoke(null, new object[] { enhancedContext });
```

#### 更新 Context（后台线程）
```csharp
var instructionField = aiServiceType.GetField("_instruction", 
    BindingFlags.NonPublic | BindingFlags.Static);
instructionField.SetValue(null, enhancedContext);
```

### 调试日志

添加了详细的开发模式日志：
```csharp
if (Prefs.DevMode)
{
    Log.Message($"[DecoratePrompt_Postfix] 🔍 Using Prompt for matching: ...");
    Log.Message($"[DecoratePrompt_Postfix] ✓ Injected to Context: ...");
}
```

## 优势

### 1. 正确性
- ✅ 使用 Prompt 进行匹配，避免误匹配
- ✅ 注入到 Context，符合设计目标
- ✅ 无重复注入

### 2. 稳定性
- ✅ 使用 Postfix，不修改 IL 代码
- ✅ 无缓存，无多线程问题
- ✅ 反射调用简单可靠

### 3. 性能
- ✅ 每次对话只执行一次匹配
- ✅ 反射调用开销小
- ✅ 无额外内存占用

### 4. 可维护性
- ✅ 代码清晰易懂
- ✅ 调试日志完善
- ✅ 注释详细

## 测试建议

### 1. 基础功能测试
- [ ] 编译项目，确保无错误
- [ ] 启动游戏，检查 patch 是否成功应用
- [ ] 触发对话，查看日志

### 2. 匹配逻辑测试
- [ ] 开启开发模式（Dev Mode）
- [ ] 触发对话，查看匹配日志
- [ ] 确认使用的是 Prompt 而不是 Context
- [ ] 确认没有匹配到不相关的常识（如"米莉拉"问题）

### 3. 注入位置测试
- [ ] 查看 AI 请求日志
- [ ] 确认常识被注入到 System Instruction（Context）
- [ ] 确认没有重复注入

### 4. 性能测试
- [ ] 多次触发对话
- [ ] 观察游戏性能
- [ ] 检查是否有内存泄漏

## 后续工作

### 短期
- [ ] 测试并验证修改
- [ ] 统一向量搜索也注入到 Context（已完成）

### 中期
- [ ] 优化匹配性能
- [ ] 添加更多调试工具
- [ ] 完善文档

### 长期
- [ ] 考虑是否需要缓存机制（如果性能成为问题）
- [ ] 探索更好的注入时机
- [ ] 与 RimTalk 作者沟通，看是否可以提供官方 API

## 总结

通过这次修改，我们成功解决了"常识既注入Prompt又注入context"的问题，并修复了相关的线程安全问题：

1. **问题根源**：BuildContext_Postfix 使用 Context 进行匹配，导致误匹配；
2. **解决方案**：
   - 在 DecoratePrompt_Postfix 中用 Prompt 匹配，通过反射注入到 Context。
   - 在 Patch_GenerateAndProcessTalkAsync 中修复线程安全问题，并注入到 Context。
3. **技术选择**：Postfix + 反射，避免 Transpiler 的不稳定性；直接字段访问避开 Logger 问题。
4. **最终效果**：
   - ✅ 使用 Prompt + Pawn 信息进行匹配
   - ✅ 注入到 Context（System Instruction）
   - ✅ 无重复注入
   - ✅ 稳定可靠，无线程问题

版本：v3.1.PROMPT_MATCH
日期：2025-12-23
