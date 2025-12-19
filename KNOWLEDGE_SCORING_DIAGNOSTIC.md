# 常识库评分诊断脚本

## 测试步骤

### 1. 启用开发模式
游戏中按 `~` 打开控制台，输入：
```
DevMode
```

### 2. 测试关键词提取

在常识库预览器中输入以下测试用例：

#### 测试用例1：纯中文2字
**输入上下文**：`李四`
**期望关键词**：`["李四"]`

#### 测试用例2：纯中文3字
**输入上下文**：`王五和`
**期望关键词**：`["王五", "五和", "王五和"]`

#### 测试用例3：混合文本
**输入上下文**：`李四和王五在聊天`
**期望关键词**：`["李四", "王五", "聊天", ...]`

### 3. 查看日志输出

在 `Player.log` 中查找：
```
[Knowledge] ExtractContextKeywords: X keywords total
[Knowledge] Top 10 final: [关键词列表]
```

### 4. 检查关键词匹配

查找日志：
```
[Knowledge] - Core: X (by length desc + alpha asc)
[Knowledge] - Fuzzy: X (alphabetical from X pool)
```

---

## 常见问题诊断

### 问题1：关键词提取为0
**症状**：日志显示 `ExtractContextKeywords: 0 keywords total`

**可能原因**：
1. SuperKeywordEngine 过滤了所有关键词
2. 输入文本全是停用词
3. 输入文本全是标点符号

**解决方案**：
检查 `SuperKeywordEngine.cs` 第219行 `IsLowQualityKeyword` 函数

### 问题2：关键词提取成功但评分为0
**症状**：日志显示提取了关键词，但常识评分为0

**可能原因**：
1. 关键词未匹配到常识内容（`IndexOf` 返回-1）
2. 常识被禁用（`isEnabled=false`）
3. 评分低于阈值（`threshold=0.1`）

**解决方案**：
检查常识内容是否真的包含这些关键词（区分大小写！）

### 问题3：2字3字评分为0，4字及以上正常
**症状**：长关键词有分数，短关键词无分数

**可能原因**：
1. `SuperKeywordEngine` 的 `IsLowQualityKeyword` 过滤了短词
2. 完全匹配加成只计算>=3字的关键词（第241-255行）

**当前代码问题**：
```csharp
// CommonKnowledgeLibrary.cs 第241行
var longestKeywords = contextKeywords
    .Where(k => k.Length >= 3)  // ? 这里过滤了2字关键词！
    .OrderByDescending(k => k.Length)
    .Take(5);
```

**修复方案**：
将 `k.Length >= 3` 改为 `k.Length >= 2`

---

## 快速测试代码

在开发者控制台中运行：

```csharp
// 测试关键词提取
var text = "李四和王五在聊天";
var keywords = RimTalk.Memory.SuperKeywordEngine.ExtractKeywords(text, 20);
Log.Message($"提取关键词数量：{keywords.Count}");
foreach (var kw in keywords.Take(10))
{
    Log.Message($"  - {kw.Word} (权重={kw.Weight:F3})");
}
```

---

## 预期结果

### 正确的关键词提取结果
```
提取关键词数量：6
  - 李四 (权重=0.150)
  - 王五 (权重=0.150)
  - 聊天 (权重=0.140)
  - 四和 (权重=0.080)
  - 和王 (权重=0.070)
  - 五在 (权重=0.060)
```

### 正确的常识评分结果
**常识**: `[社交]李四和王五是好朋友`

**评分明细**:
- 基础分（importance=0.5）：0.025
- 标签匹配（社交）：0.15
- 内容匹配：
  - "李四" (2字): 0.10 ? (修复后)
  - "王五" (2字): 0.10 ? (修复后)
  - "朋友" (2字): 0.10 ? (修复后)
- **总分**: 0.025 + 0.15 + 0.30 = **0.475** ?

### 错误的评分结果（当前bug）
**常识**: `[社交]李四和王五是好朋友`

**评分明细**:
- 基础分（importance=0.5）：0.025
- 标签匹配（社交）：0.15
- 内容匹配：
  - "李四" (2字): ? 未提取或未匹配 = 0
  - "王五" (2字): ? 未提取或未匹配 = 0
  - "朋友" (2字): ? 未提取或未匹配 = 0
- **总分**: 0.025 + 0.15 + 0 = **0.175** ?

---

## 修复建议

### 修复1：SuperKeywordEngine 不过滤2字中文词

在 `SuperKeywordEngine.cs` 第219行修改：

```csharp
private static bool IsLowQualityKeyword(string word)
{
    if (string.IsNullOrEmpty(word))
        return true;
    
    // ? 修复：只过滤1-2位纯数字，不过滤2字中文
    if (word.Length <= 2 && word.All(char.IsDigit))
        return true;
    
    // ? 修复：2字中文词不视为低质量
    if (word.Length == 2 && ContainsChinese(word))
        return false;  // 保留2字中文词
    
    // ...其他逻辑...
}
```

### 修复2：完全匹配加成包含2字关键词

在 `CommonKnowledgeLibrary.cs` 第241行修改：

```csharp
// ? 修复：完全匹配加成包含2字关键词
var longestKeywords = contextKeywords
    .Where(k => k.Length >= 2)  // ? 改为 >= 2
    .OrderByDescending(k => k.Length)
    .Take(5);

foreach (var keyword in longestKeywords)
{
    if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
    {
        if (keyword.Length >= 6)
            exactMatchBonus += 0.6f;
        else if (keyword.Length >= 5)
            exactMatchBonus += 0.4f;
        else if (keyword.Length >= 4)
            exactMatchBonus += 0.25f;
        else if (keyword.Length >= 3)
            exactMatchBonus += 0.15f;  // 3字小加成
        else if (keyword.Length == 2)
            exactMatchBonus += 0.10f;  // ? 新增：2字小加成
    }
}
```

### 修复3：提升2字和3字的基础权重

在 `CommonKnowledgeLibrary.cs` 第213行修改：

```csharp
// ? 修复后的评分曲线
if (keyword.Length >= 6)
    contentMatchScore += 0.35f;  // 6字+
else if (keyword.Length == 5)
    contentMatchScore += 0.28f;  // 5字
else if (keyword.Length == 4)
    contentMatchScore += 0.22f;  // 4字
else if (keyword.Length == 3)
    contentMatchScore += 0.16f;  // 3字（提升 0.12→0.16）
else if (keyword.Length == 2)
    contentMatchScore += 0.10f;  // 2字（提升 0.05→0.10）
else
    contentMatchScore += 0.05f;  // 1字
```

---

## 验证修复效果

修复后，使用相同测试用例验证：

**输入**: "李四和王五在聊天"
**常识**: "[社交]李四和王五是好朋友"

**期望评分**:
- 基础分：0.025
- 标签匹配：0.15
- 内容匹配：
  - "李四" (2字): 0.10 ?
  - "王五" (2字): 0.10 ?
  - "朋友" (2字): 0.10 ? (如果提取了)
- 完全匹配加成：
  - "李四" (2字): 0.10 ?
  - "王五" (2字): 0.10 ?
- **总分**: 0.025 + 0.15 + 0.30 + 0.20 = **0.675** ?

---

请按照上述步骤诊断问题，并告诉我具体的日志输出结果！
