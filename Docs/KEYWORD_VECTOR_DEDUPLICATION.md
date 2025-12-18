# 关键词与向量匹配去重修复

## 问题描述

在 v3.3.28 之前，系统存在一个严重的重复注入问题：

### 双重匹配流程
1. **向量匹配**（`Patch_GenerateAndProcessTalkAsync.Prefix`）
   - 在异步方法中进行向量搜索
   - 直接修改 `TalkRequest.Prompt`
   - 注入格式：`## World Knowledge (Vector Enhanced)`

2. **关键词匹配**（`SmartInjectionManager.InjectSmartContext`）
   - 调用 `CommonKnowledgeLibrary.InjectKnowledgeWithDetails`
   - 通过标签进行关键词匹配
   - 注入格式：`## Current Guidelines` + `## World Knowledge`

### 问题根源
这两个流程是**完全独立**的，没有任何去重机制：
- 如果一个常识条目既有匹配的标签（关键词），又有高相似度（向量）
- 该条目会被注入**两次**，导致 Prompt 冗余

## 解决方案

### 核心思路
**优先关键词，去重向量**
- 关键词匹配优先级更高（精确匹配）
- 向量匹配作为补充（语义相似）
- 向量结果中排除已被关键词匹配的条目

### 实现步骤

#### 1. 在向量匹配前调用关键词匹配
```csharp
// ⭐ 去重逻辑：先获取关键词匹配的条目ID
var keywordMatchedIds = new HashSet<string>();
try
{
    // 调用关键词匹配获取已匹配的条目
    memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
        cleanedContext,
        settings.maxVectorResults,
        out var keywordScores,
        allInvolvedPawns?.FirstOrDefault(),
        allInvolvedPawns?.Skip(1).FirstOrDefault()
    );
    
    if (keywordScores != null)
    {
        foreach (var score in keywordScores)
        {
            keywordMatchedIds.Add(score.Entry.id);
        }
        
        if (keywordMatchedIds.Count > 0)
        {
            Log.Message($"[RimTalk Memory] Found {keywordMatchedIds.Count} keyword-matched entries, will exclude from vector results");
        }
    }
}
catch (Exception ex)
{
    Log.Warning($"[RimTalk Memory] Failed to get keyword matches for deduplication: {ex.Message}");
}
```

#### 2. 过滤向量结果
```csharp
foreach (var (id, similarity) in vectorResults)
{
    // ⭐ 去重：跳过已被关键词匹配的条目
    if (keywordMatchedIds.Contains(id))
    {
        Log.Message($"[RimTalk Memory] Skipping vector result '{id}' (already matched by keyword)");
        continue;
    }
    
    var entry = entriesSnapshot.FirstOrDefault(e => e.id == id);
    if (entry != null)
    {
        float score = similarity + (entry.importance * 0.2f);
        scoredResults.Add((entry, similarity, score));
    }
}
```

#### 3. 空结果检查
```csharp
if (finalResults.Count == 0)
{
    Log.Message("[RimTalk Memory] No unique vector results after deduplication");
    return; // 如果所有向量结果都被关键词覆盖，直接返回
}
```

#### 4. 更新日志信息
```csharp
Log.Message($"[RimTalk Memory] Successfully injected {finalResults.Count} unique vector knowledge entries into prompt (excluded {keywordMatchedIds.Count} keyword-matched entries)");


## 性能影响

### 额外开销
- 在向量匹配前调用一次关键词匹配
- 时间复杂度：O(n)，n 为常识库大小
- 由于在后台线程中执行，**不影响主线程性能**

### 优化点
1. **复用结果**：关键词匹配结果被 `SmartInjectionManager` 复用
2. **早期退出**：如果所有向量结果都被覆盖，直接返回
3. **HashSet 查找**：O(1) 时间复杂度

---

## 测试建议

### 测试用例 1：完全重叠
```
常识库：
[战斗] 优先攻击医疗兵
[防御] 建立工事

输入：战斗防御策略

预期：
- 关键词匹配：2 条
- 向量匹配：0 条（全部去重）
```

### 测试用例 2：部分重叠
```
常识库：
[战斗] 优先攻击医疗兵
[历史] 三年前的战斗
[防御] 建立工事

输入：战斗策略

预期：
- 关键词匹配：1 条（战斗）
- 向量匹配：1 条（历史，语义相关但无关键词）
```

### 测试用例 3：无重叠
```
常识库：
[规则] 回复80字以内
[历史] 三年前的事件

输入：简短回复

预期：
- 关键词匹配：1 条（规则）
- 向量匹配：0 条（历史不相关）
```

---

## 相关文件

- `Source/Patches/Patch_GenerateAndProcessTalkAsync.cs` - 向量匹配 + 去重逻辑
- `Source/Memory/CommonKnowledgeLibrary.cs` - 关键词匹配
- `Source/Memory/SmartInjectionManager.cs` - 注入管理器

---

## 版本信息

- **修复版本**：v3.3.29
- **修复日期**：2025-12-18
- **问题发现者**：用户反馈
- **修复类型**：去重优化

---

## 后续优化建议

1. **缓存关键词结果**
   - 避免重复调用 `InjectKnowledgeWithDetails`
   - 在 `Patch_GenerateAndProcessTalkAsync` 中缓存结果

2. **统一匹配入口**
   - 将关键词和向量匹配合并到一个方法
   - 返回去重后的综合结果

3. **性能监控**
   - 记录去重命中率
   - 统计平均重复条目数量
