# 常识库超深度关键词优化计划 v3.3.2.25

## ?? 目标

**完全移除RAG/向量依赖，直接使用SuperKeywordEngine实现高准确率关键词检索**

---

## ?? 当前问题诊断

### 1. 向量库残留代码（已废弃但未清理）
```csharp
// ? 这些代码应该删除
VectorDB.KnowledgeVectorSyncManager.SyncKnowledge(entry);
VectorDB.KnowledgeVectorSyncManager.RemoveKnowledgeVector(entry.id);
VectorDB.KnowledgeVectorSyncManager.ClearAllKnowledgeVectors();
```

### 2. RAGManager仍被调用（但已无用）
- `SmartInjectionManager` → `RAGManager` → `AdvancedScoringSystem`
- 多余的中间层，徒增复杂度

### 3. 关键词提取未使用SuperKeywordEngine
```csharp
// ? 旧方法：简单的滑动窗口分词
for (int length = 2; length <= 4; length++)
{
    for (int i = 0; i <= text.Length - length; i++)
    {
        string word = text.Substring(i, length);
        // ...
    }
}

// ? 应该使用：SuperKeywordEngine（TF-IDF + 权重）
var weightedKeywords = SuperKeywordEngine.ExtractKeywords(text, 100);
```

---

## ?? 优化方案

### 阶段1：清理向量库残留代码 ?
**目标**：完全移除VectorDB.KnowledgeVectorSyncManager调用

**修改文件**：
- `CommonKnowledgeLibrary.cs`

**改动**：
```csharp
// 删除所有VectorDB调用
public void AddEntry(string tag, string content)
{
    var entry = new CommonKnowledgeEntry(tag, content);
    entries.Add(entry);
    // ? 删除: VectorDB.KnowledgeVectorSyncManager.SyncKnowledge(entry);
}
```

---

### 阶段2：移除RAGManager中间层 ?
**目标**：让`SmartInjectionManager`直接调用`CommonKnowledgeLibrary`

**修改文件**：
- `SmartInjectionManager.cs`

**改动**：
```csharp
// ? 旧逻辑
var result = RAG.RAGManager.Retrieve(context, speaker, listener, config);

// ? 新逻辑
var memoryManager = Find.World?.GetComponent<MemoryManager>();
string injectedKnowledge = memoryManager?.CommonKnowledge.InjectKnowledge(
    context, 
    maxKnowledge,
    speaker,
    listener
);
```

---

### 阶段3：集成SuperKeywordEngine ??
**目标**：替换滑动窗口分词为SuperKeywordEngine

**修改文件**：
- `CommonKnowledgeLibrary.cs` → `ExtractContextKeywords()`

**改动**：
```csharp
/// <summary>
/// 提取上下文关键词（超级引擎版）
/// ? v3.3.2.25: 使用SuperKeywordEngine替代滑动窗口
/// </summary>
private List<string> ExtractContextKeywords(string text)
{
    if (string.IsNullOrEmpty(text))
        return new List<string>();

    // 截断过长文本
    const int MAX_TEXT_LENGTH = 500;
    if (text.Length > MAX_TEXT_LENGTH)
    {
        text = text.Substring(0, MAX_TEXT_LENGTH);
    }

    // ? 使用SuperKeywordEngine（TF-IDF + 权重）
    var weightedKeywords = SuperKeywordEngine.ExtractKeywords(text, 100);
    
    // 返回关键词列表（已按权重排序）
    return weightedKeywords.Select(kw => kw.Word).ToList();
}
```

**优势**：
1. ? **智能分词** - 识别"龙王种索拉克"而不是"龙王"、"王种"、"种索"
2. ? **TF-IDF权重** - 高频词降权，罕见词加权
3. ? **N-gram组合** - 保留长短关键词组合
4. ? **性能优化** - 缓存+预处理，比滑动窗口更快

---

### 阶段4：优化长关键词权重 ??
**目标**：在评分算法中加强长关键词匹配

**修改文件**：
- `CommonKnowledgeEntry.CalculateRelevanceScore()`

**改动**：
```csharp
// 2. ? 内容匹配（长关键词加权）
float contentMatchScore = 0f;
if (!string.IsNullOrEmpty(content))
{
    foreach (var keyword in contextKeywords)
    {
        if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ? 长关键词权重更高
            if (keyword.Length >= 5)
                contentMatchScore += 0.30f;  // "龙王种索拉克"
            else if (keyword.Length >= 4)
                contentMatchScore += 0.20f;  // "龙王种索"
            else if (keyword.Length == 3)
                contentMatchScore += 0.12f;  // "龙王种"
            else
                contentMatchScore += 0.05f;  // "种族"
        }
    }
}
contentMatchScore = Math.Min(contentMatchScore, 1.5f); // 限制最高分
```

---

### 阶段5：添加精确匹配加成 ??
**目标**：完整匹配"龙王种索拉克"应该获得极高分

**改动**：
```csharp
// 3. ? 完全匹配加成（内容包含连续的长查询串）
float exactMatchBonus = 0f;
if (!string.IsNullOrEmpty(content))
{
    // 检查最长的关键词（通常是完整查询）
    var longestKeywords = contextKeywords
        .Where(k => k.Length >= 3)
        .OrderByDescending(k => k.Length)
        .Take(5);
    
    foreach (var keyword in longestKeywords)
    {
        if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (keyword.Length >= 6)
                exactMatchBonus += 0.8f; // "龙王种索拉克" 超强加成
            else if (keyword.Length >= 5)
                exactMatchBonus += 0.5f; // "龙王种索" 强力加成
            else if (keyword.Length >= 4)
                exactMatchBonus += 0.3f; // "龙王种" 中等加成
        }
    }
}
exactMatchBonus = Math.Min(exactMatchBonus, 1.0f);

// 综合评分
float totalScore = baseScore + tagPart + contentPart + exactMatchBonus;
```

---

## ?? 预期效果

### 匹配准确率提升
| 场景 | 旧方法（滑动窗口） | 新方法（SuperKeywordEngine） |
|------|-------------------|----------------------------|
| "龙王种索拉克" | 65% | **95%** ?? +30% |
| "美狐族" | 70% | **90%** ?? +20% |
| "精通烹饪" | 75% | **92%** ?? +17% |
| "健康状态良好" | 60% | **85%** ?? +25% |

### 性能提升
- **旧方法**：O(n?) 滑动窗口，500字符耗时~15ms
- **新方法**：O(n) SuperKeywordEngine + 缓存，500字符耗时~**3ms** ?

### 代码简洁度
- **删除文件**：RAGManager.cs, RAGResult.cs, RAGMatch.cs, RAGConfig.cs
- **代码行数**：-800行（从3500 → 2700）
- **依赖链路**：SmartInjectionManager → RAGManager → AdvancedScoringSystem → CommonKnowledgeLibrary
  - 简化为：SmartInjectionManager → CommonKnowledgeLibrary（直接2层）

---

## ?? 实施步骤

1. ? 拉取最新Git代码（v3.3.2.7）
2. ?? 阶段1：清理向量库残留
3. ?? 阶段2：移除RAGManager
4. ?? 阶段3：集成SuperKeywordEngine
5. ?? 阶段4：优化长关键词权重
6. ?? 阶段5：添加精确匹配加成
7. ? 编译测试
8. ?? 游戏内测试常识库匹配
9. ?? 生成部署文档

---

## ?? 注意事项

### 1. SuperKeywordEngine依赖检查
确保`Source/Memory/SuperKeywordEngine.cs`存在且编译正常

### 2. 兼容性保护
保留`InjectKnowledgeWithDetails`的多个重载版本，供预览器使用

### 3. 日志调试
添加DevMode日志追踪：
```csharp
if (Prefs.DevMode)
{
    Log.Message($"[Knowledge] ExtractKeywords: {text.Length} chars → {keywords.Count} keywords");
    Log.Message($"[Knowledge] Top 5: {string.Join(", ", keywords.Take(5))}");
}
```

---

## ?? 成功标准

- ? 编译无错误
- ? 游戏启动无异常
- ? 常识库预览器显示正确评分
- ? "龙王种索拉克"查询命中率 ≥ 90%
- ? 响应时间 ≤ 10ms（100条常识）
- ? 代码简洁度提升30%

---

## ?? 时间估算

| 阶段 | 预计时间 | 优先级 |
|------|---------|--------|
| 阶段1：清理向量库 | 10分钟 | P0 ?? |
| 阶段2：移除RAGManager | 15分钟 | P0 ?? |
| 阶段3：集成SuperKeywordEngine | 20分钟 | P0 ?? |
| 阶段4：长关键词权重 | 15分钟 | P1 ?? |
| 阶段5：精确匹配加成 | 10分钟 | P1 ?? |
| 测试+部署 | 20分钟 | P0 ?? |
| **总计** | **90分钟** | - |

---

## ?? 版本号

**v3.3.2.25 - "SuperKeyword Integration"**

发布日期：2025-01-XX
前置版本：v3.3.2.7
