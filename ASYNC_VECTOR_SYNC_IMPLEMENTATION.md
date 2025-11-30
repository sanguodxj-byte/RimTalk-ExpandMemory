# 异步向量化系统实现文档 v3.3.2.2

## ?? 实现目标

实现真正的**异步向量化同步**功能，使`autoSyncToVectorDB`配置生效，自动将重要记忆和常识同步到向量数据库。

---

## ?? 新增文件

### `Source/Memory/VectorDB/AsyncVectorSyncManager.cs`
异步向量化管理器，负责：
- 维护同步队列
- 批量异步处理
- 性能统计监控
- 降级处理（API失败时使用哈希向量）

---

## ?? 核心功能

### 1. **同步队列系统**

```csharp
// 配置参数
MAX_QUEUE_SIZE = 100           // 最大队列大小
BATCH_SIZE = 5                 // 每批处理数量
SYNC_INTERVAL_TICKS = 150      // 同步间隔（~2.5秒）
IMPORTANCE_THRESHOLD = 0.7f    // 重要性阈值
```

**特点：**
- ? 避免UI卡顿（后台异步处理）
- ? 批量处理（每批5个，间隔2.5秒）
- ? 队列限制（最多100个，防止内存泄漏）
- ? 重要性过滤（只同步 importance ≥ 0.7 的记忆）

### 2. **自动触发机制**

#### **记忆添加时触发**
```csharp
// FourLayerMemoryComp.cs - AddActiveMemory方法
if (ownerPawn != null && RimTalkMemoryPatchMod.Settings.autoSyncToVectorDB)
{
    VectorDB.AsyncVectorSyncManager.QueueMemorySync(memory, ownerPawn);
}
```

#### **定期处理队列**
```csharp
// MemoryManager.cs - WorldComponentTick方法
VectorDB.AsyncVectorSyncManager.ProcessSyncQueue();
```

### 3. **降级处理**

```csharp
if (settings.enableSemanticEmbedding)
{
    vector = await AI.EmbeddingService.GetEmbeddingAsync(task.Content);
    
    if (vector == null)
    {
        // API失败，降级到哈希向量
        vector = GenerateFallbackVector(task.Content);
    }
}
else
{
    // 直接使用哈希向量
    vector = GenerateFallbackVector(task.Content);
}
```

**降级策略：**
1. **首选**：语义嵌入（需要API配置）
2. **降级**：哈希向量（本地生成，<1ms）

---

## ?? 工作流程

### **记忆同步流程**

```
用户操作
   ↓
记忆添加 (AddActiveMemory)
   ↓
检查配置 (autoSyncToVectorDB?)
   ↓
检查重要性 (importance ≥ 0.7?)
   ↓
加入队列 (QueueMemorySync)
   ↓
后台处理 (每2.5秒批处理5个)
   ↓
获取向量 (语义嵌入 or 哈希向量)
   ↓
存储到数据库 (VectorDBManager.StoreKnowledgeVector)
```

### **常识同步流程**

```
常识导入
   ↓
检查重要性
   ↓
加入队列 (QueueKnowledgeSync)
   ↓
后台异步处理
   ↓
存储到向量库
```

---

## ?? 配置项

### **启用异步同步**
```
选项 → Mod设置 → RimTalk-Expand Memory
→ 实验性功能
→ ?? 启用向量数据库
→ ?? 自动同步重要记忆
```

### **重要性阈值**
默认：`0.7f`（硬编码在AsyncVectorSyncManager中）

**筛选规则：**
- `importance ≥ 0.7` → 自动同步
- `importance < 0.7` → 跳过

---

## ?? 性能特点

| 特性 | 指标 |
|------|------|
| **队列容量** | 100个任务 |
| **批处理大小** | 5个/批 |
| **处理间隔** | 2.5秒 |
| **向量生成** | 语义嵌入: 100-500ms<br>哈希向量: <1ms |
| **内存占用** | ~10KB (队列) + 4KB/向量 |

### **吞吐量估算**

**场景1：全语义嵌入（API模式）**
```
5个/批 × 300ms/个 = 1.5秒/批
每小时最多: 3600 / 4 = 900个记忆
```

**场景2：纯哈希向量（本地模式）**
```
5个/批 × 1ms/个 = 5ms/批
实际受限于间隔（2.5秒）
每小时最多: 3600 / 2.5 × 5 = 7200个记忆
```

---

## ??? API接口

### **队列管理**

```csharp
// 添加记忆到队列
AsyncVectorSyncManager.QueueMemorySync(MemoryEntry memory, Pawn pawn)

// 添加常识到队列
AsyncVectorSyncManager.QueueKnowledgeSync(CommonKnowledgeEntry knowledge)

// 处理队列（从MemoryManager调用）
AsyncVectorSyncManager.ProcessSyncQueue()
```

### **统计信息**

```csharp
// 获取统计
string stats = AsyncVectorSyncManager.GetStats();
// 输出：Queue: 5/100 | Synced: 42 | Failed: 3

// 清空队列
AsyncVectorSyncManager.ClearQueue();

// 重置统计
AsyncVectorSyncManager.ResetStats();
```

---

## ?? 故障处理

### **常见问题**

**Q1: 队列满了怎么办？**
```
A: 自动移除最旧的任务（FIFO策略）
Warning日志：Queue full (100), dropping oldest task
```

**Q2: API调用失败？**
```
A: 自动降级到哈希向量（不会丢失数据）
Warning日志：Embedding API failed, using fallback hash vector
```

**Q3: 向量化太慢？**
```
A: 检查以下配置：
1. 关闭语义嵌入（使用哈希向量）
2. 提高重要性阈值（减少同步数量）
3. 检查API网络连接
```

### **日志示例**

```
[AsyncVectorSync] Queued memory: John被虫族咬伤了腿... (queue: 5)
[AsyncVectorSync] Batch complete: 5 tasks processed (success: 42, failed: 3, queue: 10)
```

---

## ?? 与现有系统的集成

### **不冲突的功能**
- ? 手动"注入向量库"按钮（仍使用哈希向量）
- ? RAG检索（可查询异步同步的向量）
- ? 向量数据库统计（包含异步同步的数据）

### **互补关系**
| 功能 | 手动注入 | 自动同步 |
|------|---------|---------|
| **触发方式** | 点击按钮 | 记忆添加时 |
| **处理方式** | 同步（瞬间完成） | 异步（后台队列） |
| **向量类型** | 哈希向量 | 语义/哈希向量 |
| **适用场景** | 批量导入常识 | 日常记忆积累 |

---

## ?? 未来改进方向

1. **可配置阈值**
   - 在设置UI中暴露`IMPORTANCE_THRESHOLD`
   - 支持用户自定义过滤条件

2. **智能批处理**
   - 根据队列长度动态调整批量大小
   - 空闲时加速处理，繁忙时降速

3. **优先级队列**
   - 高重要性记忆优先处理
   - 用户手动标记的记忆立即同步

4. **持久化队列**
   - 未处理任务保存到存档
   - 游戏重启后继续处理

---

## ? 实现状态

| 功能 | 状态 | 说明 |
|------|------|------|
| **异步队列** | ? 完成 | 支持100个任务缓冲 |
| **批量处理** | ? 完成 | 每批5个，间隔2.5秒 |
| **自动触发** | ? 完成 | 记忆添加时自动入队 |
| **降级处理** | ? 完成 | API失败时使用哈希向量 |
| **性能监控** | ? 完成 | 统计成功/失败数量 |
| **集成到Tick** | ? 完成 | MemoryManager定期处理 |
| **编译通过** | ? 完成 | v3.3.2.2编译成功 |

---

## ?? 总结

**实现成果：**
- ? `autoSyncToVectorDB`配置真正生效
- ? 异步后台处理，不卡UI
- ? 智能降级，保证可用性
- ? 性能优化，队列限流

**用户体验：**
- 开启"自动同步"后，重要记忆会自动同步到向量库
- 完全后台处理，不影响游戏体验
- 即使没有API配置，也能使用哈希向量

**技术优势：**
- 真正的异步（Task.Run）
- 批量优化（减少API调用频率）
- 容错处理（API失败降级）
- 资源限制（队列上限防止内存泄漏）

**现在"自动同步重要记忆"真的能用了！** ???
