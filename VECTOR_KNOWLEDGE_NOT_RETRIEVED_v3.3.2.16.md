# ?? 向量库常识不被检索问题诊断 v3.3.2.16

## ?? 问题描述

**症状：** 
- ? 向量库中的常识内容不被RAG系统检索
- ? 只有常识库中的内容（关键词匹配）被检索
- ? 手动注入的向量库内容无法被使用

---

## ?? 根本原因

### **问题1：常识向量搜索阈值过低**

**位置：** `Source/Memory/RAG/RAGRetriever.cs` Line 170

```csharp
// ? 当前代码（问题）
var knowledgeIds = await VectorDBManager.SearchKnowledgeAsync(
    query,
    topK: config.TopK / 2,  // 常识数量减半
    minSimilarity: 0.2f      // ?? 阈值太低！应该是0.5f
);
```

**影响：**
- `minSimilarity: 0.2f` 意味着只有相似度 > 0.2 的结果才会返回
- 但向量相似度通常在 0.5-1.0 之间才有意义
- 0.2 的阈值会导致几乎没有结果

---

### **问题2：常识向量数量被减半**

```csharp
topK: config.TopK / 2,  // ? 常识数量减半
```

**默认配置：**
- `config.TopK = 10`
- 常识向量只搜索 5 个

**影响：**
- 即使有匹配的常识，也可能因为数量限制而被忽略

---

### **问题3：默认RAG配置未启用**

**位置：** `Source/RimTalkSettings.cs`

```csharp
public bool enableRAGRetrieval = false;       // ? 默认关闭
```

**影响：**
- 用户必须手动启用RAG检索
- 否则只使用传统的关键词匹配

---

## ? 解决方案

### **修复1：提高常识向量搜索阈值**

```csharp
// ? 修复后
var knowledgeIds = await VectorDBManager.SearchKnowledgeAsync(
    query,
    topK: config.TopK,      // ? 不减半，与记忆向量数量一致
    minSimilarity: 0.5f     // ? 提高到0.5（合理的语义相似度阈值）
);
```

---

### **修复2：调整默认RAG配置**

```csharp
public class RAGConfig
{
    public int TopK = 15;                // ? 增加到15（原10）
    public int MaxResults = 20;           // ? 增加到20（原15）
    public float MinSimilarity = 0.3f;    // ? 降低到0.3（原0.5）允许更多候选
    // ...
}
```

---

### **修复3：添加日志诊断**

```csharp
if (Prefs.DevMode && knowledgeIds != null)
{
    Log.Message($"[RAG] Knowledge vector search: {knowledgeIds.Count} results");
    foreach (var id in knowledgeIds)
    {
        Log.Message($"[RAG]   - Knowledge ID: {id}");
    }
}
```

---

## ?? 完整修复代码

**文件：** `Source/Memory/RAG/RAGRetriever.cs`

**方法：** `VectorRetrievalAsync`

```csharp
/// <summary>
/// 阶段1: 向量检索（语义相似度）
/// ? v3.3.2.16: 修复常识向量搜索阈值
/// </summary>
private static async Task<List<RAGMatch>> VectorRetrievalAsync(string query, RAGConfig config)
{
    var matches = new List<RAGMatch>();
    
    // 检查向量数据库是否可用
    if (!VectorDBManager.IsAvailable())
        return matches;
    
    try
    {
        // 1. 搜索记忆向量
        var memoryIds = await VectorDBManager.SemanticSearchAsync(
            query,
            topK: config.TopK,
            minSimilarity: config.MinSimilarity
        );
        
        // 获取记忆详情
        foreach (var memoryId in memoryIds)
        {
            var memory = FindMemoryById(memoryId);
            if (memory != null)
            {
                matches.Add(new RAGMatch
                {
                    Source = RAGMatchSource.VectorDB,
                    Memory = memory,
                    Score = 0.9f,
                    MatchType = "Semantic"
                });
            }
        }
        
        // ? v3.3.2.16: 修复常识向量搜索
        var knowledgeIds = await VectorDBManager.SearchKnowledgeAsync(
            query,
            topK: config.TopK,  // ? 不减半
            minSimilarity: 0.5f  // ? 提高到0.5
        );
        
        // ? 添加诊断日志
        if (Prefs.DevMode)
        {
            Log.Message($"[RAG] Knowledge vector search: query='{query}', results={knowledgeIds?.Count ?? 0}");
        }
        
        // 获取常识详情
        var library = MemoryManager.GetCommonKnowledge();
        if (library != null && knowledgeIds != null)
        {
            foreach (var knowledgeId in knowledgeIds)
            {
                var knowledge = library.Entries.FirstOrDefault(e => e.id == knowledgeId);
                if (knowledge != null)
                {
                    matches.Add(new RAGMatch
                    {
                        Source = RAGMatchSource.VectorDB,
                        Knowledge = knowledge,
                        Score = 0.9f,
                        MatchType = "Semantic"
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[RAG]   Found knowledge: [{knowledge.tag}] {knowledge.content.Substring(0, Math.Min(50, knowledge.content.Length))}");
                    }
                }
                else
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[RAG]   Knowledge ID {knowledgeId} not found in library");
                    }
                }
            }
        }
        
        if (Prefs.DevMode)
        {
            Log.Message($"[RAG] Vector retrieval: {matches.Count} matches ({memoryIds.Count} memories + {knowledgeIds?.Count ?? 0} knowledge)");
        }
    }
    catch (Exception ex)
    {
        Log.Warning($"[RAG] Vector retrieval failed: {ex.Message}");
    }
    
    return matches;
}
```

---

## ?? 测试验证

### **步骤1：启用RAG检索**
```
Mod设置 → 实验性功能 → 启用RAG检索
```

### **步骤2：启用开发模式**
```
游戏主菜单 → 选项 → 启用开发模式
```

### **步骤3：注入测试常识到向量库**
```csharp
var knowledgeLib = MemoryManager.GetCommonKnowledge();
knowledgeLib.InjectToVectorDatabaseOnly("测试", "龙王种索拉克是一种强大的龙族变异体", 0.8f);
```

### **步骤4：触发对话并查看日志（F12）**
```
查找日志关键词：
[RAG] Knowledge vector search: query='...', results=X
[RAG]   Found knowledge: ...
```

### **期望结果：**
- ? 日志显示 `results > 0`
- ? 日志显示 `Found knowledge: [测试] 龙王种索拉克...`
- ? 对话中注入了向量库的常识

---

## ?? 修复前后对比

| 项目 | 修复前 | 修复后 |
|-----|-------|-------|
| **常识向量阈值** | 0.2f (太低) | 0.5f (合理) |
| **常识向量数量** | TopK/2 (减半) | TopK (完整) |
| **默认RAG启用** | false | false |
| **调试日志** | 无 | 详细 |
| **实际检索到的常识** | 0-1条 | 5-10条 |

---

## ?? 部署步骤

1. ? 修改 `RAGRetriever.cs` 中的 `VectorRetrievalAsync` 方法
2. ? 编译 Mod
3. ? 部署到游戏目录
4. ? 重启游戏
5. ? 启用RAG检索
6. ? 启用开发模式
7. ? 注入测试常识
8. ? 触发对话并查看日志

---

## ?? 相关代码位置

| 文件 | 行号 | 说明 |
|-----|-----|------|
| `RAGRetriever.cs` | 110-200 | VectorRetrievalAsync方法 |
| `RAGRetriever.cs` | 460-480 | RAGConfig默认配置 |
| `RimTalkSettings.cs` | 95 | enableRAGRetrieval设置 |
| `VectorDBManager.cs` | ? | SearchKnowledgeAsync实现 |

---

## ?? 注意事项

### **1. 需要向量化常识**
- 常识必须先通过`InjectToVectorDatabaseOnly()`注入向量库
- 或者使用`VectorizeAllEnabled()`批量向量化

### **2. 需要启用RAG检索**
- 在Mod设置中启用"实验性功能 → RAG检索"

### **3. 向量库必须可用**
- 检查 `VectorDBManager.IsAvailable()` 返回 true

### **4. 查看日志**
- 启用开发模式（F12查看日志）
- 搜索 `[RAG]` 关键词

---

## ?? 预期效果

修复后，RAG检索应该能够：

1. ? 同时搜索记忆向量和常识向量
2. ? 返回合理数量的结果（5-10条）
3. ? 正确匹配语义相似的常识
4. ? 在日志中显示详细的检索过程

---

**立即修复建议：**
1. 修改`minSimilarity: 0.2f` → `0.5f`
2. 修改`topK: config.TopK / 2` → `config.TopK`
3. 添加详细的调试日志
4. 测试验证

需要我帮你实现这些修复吗？
