# ?? Pawn专属常识功能 v3.3.3

## ? **功能概述**

为常识库添加**Pawn专属**功能，让常识可以指定只对特定殖民者可见。

---

## ?? **核心特性**

| 特性 | 描述 |
|------|------|
| ? **自动专属** | 自动生成的Pawn状态常识自动设置为专属 |
| ? **手动控制** | 用户可在编辑界面选择全局或专属 |
| ? **智能筛选** | 常识注入时自动过滤（只显示全局+专属） |
| ? **UI友好** | 下拉菜单选择，清晰显示可见范围 |

---

## ?? **使用场景**

### **场景1: 自动生成的状态常识**

```
系统自动生成：
- Alice是殖民地的新成员，3天前加入
  → targetPawnId = Alice.thingIDNumber
  → 只有Alice可以看到这条常识

- Bob是殖民地的新成员，1天前加入  
  → targetPawnId = Bob.thingIDNumber
  → 只有Bob可以看到这条常识
```

**效果**：
- ? Alice对话时会注入Alice的状态常识
- ? Bob对话时会注入Bob的状态常识
- ? Alice看不到Bob的常识，反之亦然

---

### **场景2: 手动添加的专属常识**

用户可以手动为特定Pawn添加常识：

```
编辑常识：
标签: 角色背景
内容: 你曾是边境哨兵，擅长狙击
专属殖民者: [选择] Charlie ←— 只有Charlie可见

或

专属殖民者: [选择] 全局可见 ←— 所有殖民者可见
```

---

## ?? **UI操作**

### **1. 编辑面板 - 专属Pawn选择器**

```
┌─────────────────────────────────────┐
│ 编辑常识                            │
├─────────────────────────────────────┤
│ 标签:       [角色背景             ] │
│ 重要性:     [======>         ] 0.8  │
│ 专属殖民者: [全局可见 ▼          ]  │  ← 点击选择
│             提示: 全局常识：所有殖民者都可以看到
│ 内容:                               │
│ [你是一名经验丰富的狙击手        ]  │
│ [                                 ]  │
└─────────────────────────────────────┘
[保存] [取消]
```

### **2. 下拉菜单**

```
点击 [全局可见 ▼] 后显示：

┌─────────────────────────────┐
│ 全局可见（所有殖民者）      │  ← 默认选项
├─────────────────────────────┤
│ 专属: Alice                 │
│ 专属: Bob                   │
│ 专属: Charlie               │
│ 专属: Diana                 │
└─────────────────────────────┘
```

### **3. 详情面板**

```
┌─────────────────────────────────────┐
│ [角色背景] 详情                     │
├─────────────────────────────────────┤
│ 重要性:     0.8                     │
│ 状态:       启用                    │
│ 可见范围:   专属: Charlie           │  ← 显示专属信息
│ 内容:                               │
│ 你是一名经验丰富的狙击手            │
└─────────────────────────────────────┘
[编辑]
```

---

## ?? **技术实现**

### **1. 数据结构**

```csharp
public class CommonKnowledgeEntry
{
    public int targetPawnId = -1;  // -1=全局，>=0=专属于特定Pawn
}
```

### **2. 自动设置（Pawn状态常识）**

```csharp
// PawnStatusKnowledgeGenerator.cs
var newEntry = new CommonKnowledgeEntry(statusTag, newContent)
{
    importance = defaultImportance,
    isEnabled = true,
    isUserEdited = false,
    targetPawnId = pawn.thingIDNumber  // ? 自动设置
};
```

### **3. 智能筛选（常识注入）**

```csharp
// CommonKnowledgeLibrary.cs - InjectKnowledgeWithDetails()
var filteredEntries = entries
    .Where(e => e.isEnabled)
    .Where(e => e.targetPawnId == -1 ||  // 全局常识
               (currentPawn != null && e.targetPawnId == currentPawn.thingIDNumber)) // 或专属常识
    .ToList();
```

### **4. UI选择器**

```csharp
// Dialog_CommonKnowledge.cs - DrawEditPanel()
if (Widgets.ButtonText(rect, buttonLabel))
{
    List<FloatMenuOption> options = new List<FloatMenuOption>();
    
    // 全局选项
    options.Add(new FloatMenuOption("全局可见（所有殖民者）", delegate
    {
        editTargetPawnId = -1;
    }));
    
    // 专属选项
    foreach (var pawn in colonists)
    {
        int pawnId = pawn.thingIDNumber;
        options.Add(new FloatMenuOption($"专属: {pawn.LabelShort}", delegate
        {
            editTargetPawnId = pawnId;
        }));
    }
    
    Find.WindowStack.Add(new FloatMenu(options));
}
```

---

## ?? **筛选逻辑**

### **示例数据**

| ID | 内容 | targetPawnId | 可见对象 |
|----|------|--------------|----------|
| 1 | 边缘世界规则 | -1 | 所有人 |
| 2 | Alice是新成员 | 123 (Alice) | 只有Alice |
| 3 | Bob是狙击手 | 456 (Bob) | 只有Bob |
| 4 | 殖民地历史 | -1 | 所有人 |

### **Alice对话时**

筛选结果：
- ? ID 1: 边缘世界规则（全局）
- ? ID 2: Alice是新成员（专属于Alice）
- ? ID 3: Bob是狙击手（专属于Bob，Alice看不到）
- ? ID 4: 殖民地历史（全局）

**注入内容**：
```
1. [规则] 边缘世界规则
2. [殖民地成员] Alice是殖民地的新成员，3天前加入
3. [历史] 殖民地历史
```

---

## ?? **实际应用**

### **1. 角色专属背景**

```
为刺客角色添加：
[角色背景|0.9] 你曾是帝国情报部门的特工，擅长潜行和暗杀
专属殖民者: Shadow (刺客角色)
```

### **2. 种族专属常识**

```
为龙王种角色添加：
[种族|0.9] 你是龙王种索拉克亚种，拥有强大的魔法能力，但惧怕寒冷
专属殖民者: Draco (龙王种角色)
```

### **3. 新成员引导**

```
自动生成（系统）：
[殖民地成员|0.5] Rookie是殖民地的新成员，今天加入，对殖民地的历史和成员关系尚不熟悉
专属殖民者: Rookie (自动设置)
```

---

## ?? **注意事项**

### **1. Pawn删除后**

如果专属的Pawn被删除（死亡/离开）：
- 常识仍然存在
- 详情面板显示：`专属: Pawn已删除 (ID:123)`
- 不会被任何Pawn看到（因为找不到对应的Pawn）

**建议**：
- 定期清理无效的专属常识
- 或手动改为全局可见

### **2. 存档兼容性**

- ? 旧存档中的常识：`targetPawnId` 默认为 `-1`（全局）
- ? 完全向后兼容

### **3. 性能**

- 筛选操作在常识注入时进行
- 时间复杂度：O(n)，n为常识数量
- 对于<1000条常识，性能影响可忽略

---

## ?? **未来扩展**

v3.3.4 计划功能：

- [ ] **批量设置** - 一键将所有状态常识设为专属
- [ ] **标签筛选** - 按专属/全局快速筛选常识列表
- [ ] **自动清理** - 检测并清理无效的专属常识
- [ ] **多Pawn专属** - 一条常识可专属于多个Pawn
- [ ] **阵营专属** - 专属于整个阵营而非单个Pawn

---

## ?? **代码位置**

| 文件 | 关键代码 |
|------|----------|
| `CommonKnowledgeLibrary.cs` | `targetPawnId`字段，筛选逻辑 |
| `PawnStatusKnowledgeGenerator.cs` | 自动设置`targetPawnId` |
| `Dialog_CommonKnowledge.cs` | UI选择器，编辑/详情面板 |

---

## ?? **总结**

**Pawn专属常识**功能让常识系统更加智能和个性化：

? **自动化** - 系统自动为Pawn状态常识设置专属  
? **灵活性** - 用户可手动控制全局/专属  
? **易用性** - 友好的UI，清晰的提示  
? **兼容性** - 完全向后兼容旧存档

**让每个殖民者都有自己的专属知识！** ??
