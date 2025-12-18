# 向量匹配上下文清理集成报告

## 修复日期
2025-12-18

## 问题描述

向量匹配功能在两个触发点都没有使用 `ContextCleaner` 来清理上下文，导致：

1. **包含大量格式噪音**：RimTalk 的 Prompt 格式（如 `said to 'Rebecca: ...'`、环境信息等）被直接发送给向量匹配
2. **匹配精度下降**：噪音信息干扰了语义相似度计算
3. **不必要的 API 调用成本**：向量化了大量无关内容

### 示例问题

**原始上下文**（包含噪音）：
```
秩序超凡智能() said to 'Rebecca: 阿拉尼亚圣树是一颗黄金色的巨树，是我们的圣树。'.Generate dialogue starting after this. Do not ge
Rebecca(Age:49;女性;ID:Colonist;人类) 闲逛中。
Nearby: 秩序超凡智能(Age:19;女性;ID:;人类) , Yaowen(Age:48;男性;ID:Colonist;人类) 闲逛中。
Time: 6am
Today: 5500年翠象第6天
Season: 春季
Weather: 晴
Location: 室外;6C
Terrain: 沙地
Wealth: impecunious
in ChineseSimplified (简体中文)
```

**期望清理后**（纯语义内容）：
```
阿拉尼亚圣树是一颗黄金色的巨树，是我们的圣树。
```

## 修复内容

### 1. ContextCleaner.cs 正则表达式修复

**文件**: `Source/Memory/ContextCleaner.cs`

**修改**：
- 将 `@"said to '.*?: (.+?)'"` 改为 `@"said to '[^']*?: ([^']+)'"`
- 将 `@"said to '(.+?)'"` 改为 `@"said to '([^']+)'"`
- 添加 `.Trim()` 去除首尾空格

**原因**：
- 原正则使用 `.+?`（非贪婪匹配），在遇到某些字符时会提前停止
- 新正则使用 `[^']+`（匹配除单引号外的所有字符），确保匹配到正确的结束位置

### 2. Dialog_InjectionPreview.cs 测试向量匹配

**文件**: `Source/Memory/UI/Dialog_InjectionPreview.cs`

**修改位置**: `TestVectorMatching()` 方法

**添加**：
```csharp
// ⭐ 使用 ContextCleaner 清理上下文，去除噪音
string cleanedContext = ContextCleaner.CleanForVectorMatching(contextInput);

// 调用向量服务进行匹配
var vectorResults = VectorDB.VectorService.Instance.FindBestLoreIds(
    cleanedContext,  // ⬅️ 使用清理后的上下文
    settings.maxVectorResults * 2,
    settings.vectorSimilarityThreshold
);
```

**日志增强**：
```csharp
Log.Message($"[RimTalk-ExpandMemory] 原始上下文: {contextInput.Substring(0, Math.Min(100, contextInput.Length))}");
Log.Message($"[RimTalk-ExpandMemory] 清理后上下文: {cleanedContext}");
```

### 3. Patch_GenerateAndProcessTalkAsync.cs 后台异步匹配

**文件**: `Source/Patches/Patch_GenerateAndProcessTalkAsync.cs`

**修改位置**: `Prefix()` 方法

**添加**：
```csharp
// ⭐ 使用 ContextCleaner 清理上下文，去除 RimTalk 格式噪音
string cleanedContext = ContextCleaner.CleanForVectorMatching(currentPrompt);

if (string.IsNullOrEmpty(cleanedContext))
{
    Log.Warning($"[RimTalk Memory] Context cleaned to empty, using original prompt for vector search");
    cleanedContext = currentPrompt; // 回退到原始 prompt
}
else
{
    Log.Message($"[RimTalk Memory] Cleaned context: {cleanedContext.Substring(0, Math.Min(100, cleanedContext.Length))}...");
}

// 异步向量搜索并同步等待结果
var vectorResults = VectorService.Instance.FindBestLoreIdsAsync(
    cleanedContext,  // ⬅️ 使用清理后的上下文
    settings.maxVectorResults,
    settings.vectorSimilarityThreshold
).Result;
```

**安全措施**：
- 如果清理后为空，回退到原始 prompt（避免完全失效）
- 添加详细日志以便调试

## 向量匹配触发点总结

### 触发点 1: 调试预览器手动测试
- **位置**: `Dialog_InjectionPreview.TestVectorMatching()`
- **触发**: 用户点击 "🧠 测试向量匹配" 按钮
- **上下文来源**: 用户在预览器中输入的文本
- **清理状态**: ✅ 已集成 ContextCleaner

### 触发点 2: RimTalk 后台自动匹配
- **位置**: `Patch_GenerateAndProcessTalkAsync.Prefix()`
- **触发**: RimTalk 发送 AI 请求时自动触发
- **上下文来源**: RimTalk 的完整 Prompt（包含格式化信息）
- **清理状态**: ✅ 已集成 ContextCleaner

## 清理规则

`ContextCleaner.CleanForVectorMatching()` 会：

1. **提取对话内容**：
   - 格式：`said to 'Name: content'` → 提取 `content`
   - 格式：`said to 'content'` → 提取 `content`

2. **过滤环境噪音**：
   - Time, Today, Season, Weather
   - Location, Terrain, Wealth
   - Nearby, Nearby people
   - in ChineseSimplified

3. **过滤 Pawn 状态**：
   - 格式：`Name(Age:X;性别;ID:...)`

4. **保留有效内容**：
   - 事件块：`[Ongoing events]` ... `[Event list end]`
   - 对话块：`starts conversation` ... 对话内容
   - 独白块：`short monologue` ... 独白内容

## 预期效果

### 匹配精度提升
- **清理前**：向量匹配包含大量格式噪音，相似度计算不准确
- **清理后**：只匹配纯语义内容，相似度更能反映真实相关性

### API 成本优化
- **清理前**：向量化整个 Prompt（可能数百字符）
- **清理后**：只向量化核心对话内容（通常几十字符）
- **预估节省**：60-80% 的向量化成本

### 用户体验改善
- **测试功能**：预览器中可以看到清理前后的对比
- **日志透明**：详细记录清理过程，便于调试
- **安全回退**：清理失败时自动使用原始内容

## 测试建议

### 1. 预览器测试
```
1. 打开调试预览器
2. 输入测试上下文：
   "秩序超凡智能() said to 'Rebecca: 阿拉尼亚圣树是一颗黄金色的巨树，是我们的圣树。'.Generate..."
3. 点击 "🧠 测试向量匹配"
4. 查看日志中的清理结果
```

### 2. 实际游戏测试
```
1. 启用向量增强功能
2. 进行正常对话
3. 查看日志：
   - "[RimTalk Memory] Cleaned context: ..."
   - 确认只包含对话内容，无格式噪音
```

### 3. 边缘情况测试
```
- 空上下文
- 纯环境信息（无对话）
- 多行对话
- 包含特殊字符的对话
```

## 相关文档

- `VECTOR_CONTEXT_CLEANER.md` - ContextCleaner 设计文档
- `ASYNC_VECTOR_INTEGRATION.md` - 异步向量匹配集成
- `VECTOR_MATCHING_FIX.md` - 向量匹配修复历史

## 总结

通过在两个向量匹配触发点集成 `ContextCleaner`，我们确保了：

1. ✅ **语义纯净性**：只有核心对话内容参与向量匹配
2. ✅ **匹配准确性**：去除噪音后相似度计算更精确
3. ✅ **成本优化**：减少不必要的向量化开销
4. ✅ **可调试性**：详细日志记录清理过程
5. ✅ **安全性**：清理失败时有回退机制

这次修复解决了向量匹配中最关键的上下文质量问题，为后续的语义检索奠定了坚实基础。
