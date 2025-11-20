# 记忆重要性评分参考

## 概述

记忆的重要性（importance）是一个0-1之间的浮点数，影响：
1. **动态注入评分**：重要性越高，越容易被选中注入
2. **记忆保留**：重要的记忆衰减更慢
3. **显示优先级**：UI中更突出

---

## 当前评分标准

### 对话记忆（Conversation）

**位置：** `Source\Memory\MemoryAIIntegration.cs` - `RecordConversation()`

| 角色 | 重要性 | 说明 |
|------|--------|------|
| 说话者 | **0.6** | 主动发起对话，重要性较高 |
| 听者 | **0.5** | 被动接收信息，重要性中等 |

**代码：**
```csharp
// 说话者视角
speakerMemory.AddMemory(memoryContent, MemoryType.Conversation, 0.6f, listenerName);

// 听者视角
listenerMemory.AddMemory(memoryContent, MemoryType.Conversation, 0.5f, speakerName);
```

**示例：**
```
Alice说给Bob：今天的食物不够了
- Alice的记忆：重要性 0.6（说话者）
- Bob的记忆：重要性 0.5（听者）
```

---

### 行动记忆（Action）

**位置：** `Source\Patches\JobMemoryPatch.cs` - `GetJobImportance()`

#### 高优先级行动（0.85-1.0）

| 行动类型 | 重要性 | JobDef | 说明 |
|----------|--------|--------|------|
| 结婚 | **1.0** | MarryAdjacentPawn | 人生大事 |
| 亲热 | **0.95** | Lovin | 重要社交 |
| 近战攻击 | **0.9** | AttackMelee | 战斗行为 |
| 远程攻击 | **0.9** | AttackStatic | 战斗行为 |
| 社交冲突 | **0.85** | SocialFight | 负面社交 |

#### 中优先级行动（0.5-0.8）

| 行动类型 | 重要性 | JobDef | 说明 |
|----------|--------|--------|------|
| 观看仪式 | **0.7** | SpectateCeremony | 社交活动 |
| 工作行动 | **0.5** | （其他） | 默认值 |

**代码：**
```csharp
private static float GetJobImportance(JobDef jobDef)
{
    // 战斗和社交最重要
    if (jobDef == JobDefOf.AttackMelee) return 0.9f;
    if (jobDef == JobDefOf.AttackStatic) return 0.9f;
    if (jobDef == JobDefOf.SocialFight) return 0.85f;
    if (jobDef == JobDefOf.MarryAdjacentPawn) return 1.0f;
    if (jobDef == JobDefOf.SpectateCeremony) return 0.7f;
    if (jobDef == JobDefOf.Lovin) return 0.95f;

    // 工作行动默认中等重要
    return 0.5f;
}
```

#### 被过滤的行动（不记录）

以下行动被认为**不重要**，不会产生记忆：

| 行动 | JobDef | 原因 |
|------|--------|------|
| 行走 | Goto | 太频繁 |
| 等待 | Wait* | 无意义 |
| 闲逛 | GotoWander | 日常活动 |
| 站立 | Wait_Stand | 无信息量 |

---

## 重要性对动态注入的影响

### 评分公式

```
记忆总评分 = 时间衰减(30%) + 重要性(30%) + 关键词匹配(40%) + 加成

其中：
重要性得分 = memory.importance * 0.3
```

### 实际影响

假设两条记忆：
- **记忆A**（战斗）：importance = 0.9
- **记忆B**（烹饪）：importance = 0.5

即使记忆B更新鲜，如果对话主题是"战斗"：
```
记忆A得分：
  时间: 0.20（较旧）
  重要性: 0.27（0.9 * 0.3）
  关键词: 0.35（高匹配）
  总分: 0.82 ← 更高

记忆B得分：
  时间: 0.28（更新）
  重要性: 0.15（0.5 * 0.3）
  关键词: 0.05（低匹配）
  总分: 0.48
```

**结论：** 重要性高的记忆更容易被注入，即使它不是最新的。

---

## 调整建议

### 场景1：深度角色扮演

**目标：** 更关注情感和社交

**调整：**
```csharp
// 提高社交和情感行动的重要性
if (jobDef == JobDefOf.SocialFight) return 0.95f; // 0.85 → 0.95
if (jobDef == JobDefOf.Lovin) return 1.0f;        // 保持
if (jobDef.defName.Contains("Social")) return 0.7f; // 新增

// 降低工作行动
return 0.3f; // 0.5 → 0.3
```

### 场景2：战斗为主

**目标：** 强调战斗经历

**调整：**
```csharp
// 提高所有战斗相关
if (jobDef == JobDefOf.AttackMelee) return 1.0f;  // 0.9 → 1.0
if (jobDef == JobDefOf.AttackStatic) return 1.0f; // 0.9 → 1.0
if (jobDef.defName.Contains("Combat")) return 0.9f; // 新增
```

### 场景3：日常生活

**目标：** 记录更多日常细节

**调整：**
```csharp
// 提高工作行动
return 0.6f; // 0.5 → 0.6

// 记录更多行动类型
private static bool IsSignificantJob(JobDef jobDef)
{
    // 减少过滤，允许更多行动
    if (jobDef == JobDefOf.Goto) return false;
    // 移除其他过滤
    return true;
}
```

---

## 手动编辑重要性

用户可以在记忆UI中手动修改重要性：

### 修改方法
1. 打开记忆UI（ITab_Memory）
2. 选择记忆
3. 点击"编辑"
4. 调整重要性滑块（0-1）
5. 保存

### 特殊加成
手动编辑的记忆会获得额外加成：
```csharp
if (memory.isUserEdited) bonusScore += 0.3f;
```

---

## 常识库重要性

**位置：** `Source\Memory\CommonKnowledgeLibrary.cs`

常识条目也有重要性字段：

```csharp
public float importance; // 0.1-1.0，默认0.5
```

### 影响

```csharp
// 常识评分
score = (jaccardScore * 0.7f + tagScore) * importance;
```

**示例：**
```
常识A：[规则]食物会腐坏（importance=0.8）
常识B：[背景]殖民地历史（importance=0.3）

在讨论食物时：
- 常识A得分 = 0.85 * 0.8 = 0.68
- 常识B得分 = 0.45 * 0.3 = 0.14

→ 常识A更可能被注入
```

### 推荐值

| 类型 | 重要性 | 说明 |
|------|--------|------|
| 核心规则 | 0.8-1.0 | 游戏机制、必知信息 |
| 世界观 | 0.6-0.8 | 背景设定 |
| 角色设定 | 0.5-0.7 | 个性、关系 |
| 琐碎信息 | 0.2-0.4 | 不重要的细节 |

---

## 最佳实践

### 1. 分层设置

**高重要性（0.8-1.0）**
- 战斗、死亡、受伤
- 重要社交（结婚、表白、决裂）
- 危机事件

**中重要性（0.5-0.7）**
- 日常对话
- 普通工作
- 观察和思考

**低重要性（0.2-0.4）**
- 重复性工作
- 无关紧要的闲聊
- 环境描述

### 2. 动态调整

根据角色特点调整：
```
战士：战斗 → 0.95，工作 → 0.4
厨师：烹饪 → 0.8，战斗 → 0.6
社交家：对话 → 0.8，工作 → 0.4
```

### 3. 平衡Token消耗

- 提高重要性 → 更多高质量记忆被注入
- 降低重要性 → 节省Token，但可能错过重要信息

**平衡点：**
- 关键事件：0.8+
- 日常活动：0.5
- 可选内容：0.3-

---

## 代码位置速查

### 修改对话重要性
```
文件：Source\Memory\MemoryAIIntegration.cs
方法：RecordConversation()
行数：~130-140
```

### 修改行动重要性
```
文件：Source\Patches\JobMemoryPatch.cs
方法：GetJobImportance()
行数：~75-90
```

### 添加新行动类型
```
文件：Source\Patches\JobMemoryPatch.cs
方法：GetJobImportance()

// 添加新的判断
if (jobDef == JobDefOf.YourNewJob) return 0.8f;
```

### 过滤不重要行动
```
文件：Source\Patches\JobMemoryPatch.cs
方法：IsSignificantJob()
行数：~55-75

// 添加新的过滤
if (jobDef == JobDefOf.TrivialJob) return false;
```

---

## 未来改进

### 计划功能

1. **动态重要性**
   - 根据殖民者特质自动调整
   - 基于事件发生频率动态调整

2. **学习系统**
   - 分析哪些记忆经常被注入
   - 自动提高相关记忆的重要性

3. **设置界面**
   - 允许用户调整各类行动的基础重要性
   - 预设模式（战斗/日常/社交/平衡）

4. **条件重要性**
   - 根据当前情境调整重要性
   - 例如：受伤时，医疗行动重要性+0.2

---

## 总结

| 要素 | 当前默认值 | 调整范围 | 影响 |
|------|----------|---------|------|
| 对话（说） | 0.6 | 0.3-0.9 | 说话者的记忆保留 |
| 对话（听） | 0.5 | 0.3-0.8 | 听者的记忆保留 |
| 战斗 | 0.9 | 0.7-1.0 | 战斗经历的突出度 |
| 社交 | 0.7-1.0 | 0.5-1.0 | 人际关系的重视 |
| 工作 | 0.5 | 0.3-0.7 | 日常工作的记录 |
| 常识 | 0.5 | 0.1-1.0 | 常识注入优先级 |

**核心原则：**
- 💡 **重要事件**应该有高重要性（0.8+）
- ⚖️ **日常活动**保持中等（0.5）
- 🗑️ **琐碎信息**可以更低（0.3-）或直接过滤

---

**文件位置：** MEMORY_IMPORTANCE_GUIDE.md  
**版本：** v2.3.0  
**最后更新：** 2024
