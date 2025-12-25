# RimTalk 常识库公共 API 文档

## ?? 概述

`CommonKnowledgeAPI` 提供了完整的常识库操作接口，允许其他 Mod 轻松注入、更新和管理常识。

## ?? 快速开始

### 1. 添加依赖

在您的 Mod 的 `.csproj` 文件中添加引用：

```xml
<Reference Include="RimTalkMemoryPatch">
  <HintPath>..\..\RimTalk-ExpandMemory\1.6\Assemblies\RimTalkMemoryPatch.dll</HintPath>
  <Private>False</Private>
</Reference>
```

### 2. 引入命名空间

```csharp
using RimTalk.Memory;
```

## ?? API 参考

### 添加常识

#### 简单添加

```csharp
// 添加一条简单的常识
string id = CommonKnowledgeAPI.AddKnowledge(
    tag: "世界观,边缘世界",
    content: "这是一个科技倒退的时代，人类文明散落在星系各处",
    importance: 0.7f  // 重要性 0-1，默认 0.5
);
```

#### 高级添加

```csharp
// 添加一条带完整参数的常识
string id = CommonKnowledgeAPI.AddKnowledgeEx(
    tag: "规则,对话",
    content: "你必须用中文回复，保持角色扮演",
    importance: 0.9f,
    matchMode: KeywordMatchMode.All,  // All=所有标签必须匹配，Any=任意一个即可
    targetPawnId: -1,  // -1=全局，其他=仅对特定Pawn有效
    canBeExtracted: true,  // 是否可以被提取（用于常识链）
    canBeMatched: true     // 是否可以被匹配（用于常识链）
);
```

#### 批量添加

```csharp
var knowledgeList = new List<(string tag, string content)>
{
    ("世界观,科技", "光速引擎已经失传"),
    ("世界观,社会", "机械体和人类共存"),
    ("规则,语气", "使用幽默的语气")
};

int count = CommonKnowledgeAPI.AddKnowledgeBatch(knowledgeList, importance: 0.6f);
Log.Message($"成功添加 {count} 条常识");
```

### 更新常识

```csharp
// 更新内容
bool success = CommonKnowledgeAPI.UpdateKnowledge(id, "新的内容");

// 更新标签
bool success = CommonKnowledgeAPI.UpdateKnowledgeTag(id, "新标签");

// 更新重要性
bool success = CommonKnowledgeAPI.UpdateKnowledgeImportance(id, 0.8f);

// 启用/禁用
bool success = CommonKnowledgeAPI.SetKnowledgeEnabled(id, false);
```

### 查询常识

```csharp
// 根据ID查找
CommonKnowledgeEntry entry = CommonKnowledgeAPI.FindKnowledgeById("ck-abc123");

// 根据标签查找（支持部分匹配）
List<CommonKnowledgeEntry> entries = CommonKnowledgeAPI.FindKnowledge("世界观");

// 根据内容查找
List<CommonKnowledgeEntry> entries = CommonKnowledgeAPI.FindKnowledgeByContent("光速");

// 获取所有常识
List<CommonKnowledgeEntry> allEntries = CommonKnowledgeAPI.GetAllKnowledge();

// 获取常识数量
int count = CommonKnowledgeAPI.GetKnowledgeCount();
```

### 删除常识

```csharp
// 根据ID删除
bool success = CommonKnowledgeAPI.RemoveKnowledge(id);

// 根据标签删除所有匹配的
int count = CommonKnowledgeAPI.RemoveKnowledgeByTag("旧标签");

// 清空所有常识（危险操作！）
bool success = CommonKnowledgeAPI.ClearAllKnowledge();
```

### 导入/导出

```csharp
// 导出为文本
string text = CommonKnowledgeAPI.ExportToText();

// 从文本导入
int count = CommonKnowledgeAPI.ImportFromText(text, clearExisting: false);
```

### 统计信息

```csharp
KnowledgeStats stats = CommonKnowledgeAPI.GetStats();
Log.Message($"总数: {stats.TotalCount}");
Log.Message($"启用: {stats.EnabledCount}");
Log.Message($"禁用: {stats.DisabledCount}");
Log.Message($"用户编辑: {stats.UserEditedCount}");
Log.Message($"全局常识: {stats.GlobalCount}");
Log.Message($"Pawn专属: {stats.PawnSpecificCount}");
```

## ?? 使用场景

### 场景1: 添加 Mod 专属规则

```csharp
public class MyModInitializer : Mod
{
    public MyModInitializer(ModContentPack content) : base(content)
    {
        // 在 Mod 加载时添加规则
        LongEventHandler.QueueLongEvent(() =>
        {
            CommonKnowledgeAPI.AddKnowledge(
                tag: "规则,MyMod",
                content: "你是一个魔法世界的角色，可以使用魔法技能",
                importance: 0.9f
            );
        }, "InitializingMyMod", false, null);
    }
}
```

### 场景2: 动态更新事件

```csharp
public class MyEventHandler
{
    private string knowledgeId;

    public void OnEventStart()
    {
        // 事件开始时添加常识
        knowledgeId = CommonKnowledgeAPI.AddKnowledge(
            tag: "事件,活跃,MyEvent",
            content: "当前正在进行魔法仪式，需要保持安静",
            importance: 0.8f
        );
    }

    public void OnEventEnd()
    {
        // 事件结束时删除常识
        if (!string.IsNullOrEmpty(knowledgeId))
        {
            CommonKnowledgeAPI.RemoveKnowledge(knowledgeId);
        }
    }
}
```

### 场景3: Pawn 专属常识

```csharp
public void AddPawnSpecificKnowledge(Pawn pawn)
{
    // 为特定 Pawn 添加专属常识
    CommonKnowledgeAPI.AddKnowledgeEx(
        tag: $"角色背景,{pawn.Name.ToStringShort}",
        content: $"{pawn.Name.ToStringShort} 曾经是一名传奇法师",
        importance: 0.7f,
        targetPawnId: pawn.thingIDNumber  // 只对这个 Pawn 有效
    );
}
```

### 场景4: 批量管理

```csharp
public void InitializeQuestKnowledge()
{
    // 查找并删除旧的任务常识
    int removed = CommonKnowledgeAPI.RemoveKnowledgeByTag("任务,旧");
    
    // 添加新的任务常识
    var questKnowledge = new List<(string, string)>
    {
        ("任务,活跃", "需要收集10个魔法水晶"),
        ("任务,活跃", "避免在夜晚外出"),
        ("任务,活跃", "保护村庄免受怪物攻击")
    };
    
    int added = CommonKnowledgeAPI.AddKnowledgeBatch(questKnowledge, 0.8f);
    Log.Message($"任务常识已更新: 删除 {removed} 条，添加 {added} 条");
}
```

## ?? 注意事项

### 1. 性能考虑

- 避免在每一帧调用查询操作
- 批量操作优于单条操作
- 缓存查询结果

```csharp
// ? 不好的做法
public override void Tick()
{
    var entries = CommonKnowledgeAPI.FindKnowledge("世界观"); // 每帧查询
}

// ? 好的做法
private List<CommonKnowledgeEntry> cachedEntries;
private int lastUpdateTick = 0;

public override void Tick()
{
    if (Find.TickManager.TicksGame - lastUpdateTick > 2500) // 每小时更新一次
    {
        cachedEntries = CommonKnowledgeAPI.FindKnowledge("世界观");
        lastUpdateTick = Find.TickManager.TicksGame;
    }
}
```

### 2. 标签命名建议

- 使用有意义的标签：`"规则,对话"` 而不是 `"rule1"`
- 支持多个标签（逗号分隔）：`"世界观,科技,光速"`
- 使用命名空间避免冲突：`"MyMod,规则,魔法"`

### 3. 重要性设置

| 重要性 | 说明 | 示例 |
|--------|------|------|
| 0.9-1.0 | 核心规则，必须遵守 | 游戏规则、角色扮演要求 |
| 0.7-0.8 | 重要信息 | 世界观设定、活跃事件 |
| 0.5-0.6 | 一般信息 | 背景故事、参考资料 |
| 0.3-0.4 | 可选信息 | 细节描述、彩蛋 |
| 0.1-0.2 | 低优先级 | 不重要的提示 |

### 4. 常识链功能

如果您想让常识支持链式匹配（一条常识触发另一条常识）：

```csharp
CommonKnowledgeAPI.AddKnowledgeEx(
    tag: "魔法,火系",
    content: "火系魔法威力强大但容易失控",
    importance: 0.7f,
    canBeExtracted: true,  // ? 允许提取：这条常识的内容可以用于触发其他常识
    canBeMatched: true     // ? 允许匹配：其他常识的内容可以触发这条常识
);
```

## ?? 故障排除

### 1. 常识未生效

检查：
- 常识是否启用（`isEnabled = true`）
- 标签是否正确匹配
- 重要性是否过低

```csharp
var entry = CommonKnowledgeAPI.FindKnowledgeById(id);
if (entry != null)
{
    Log.Message($"Enabled: {entry.isEnabled}");
    Log.Message($"Tag: {entry.tag}");
    Log.Message($"Importance: {entry.importance}");
}
```

### 2. 找不到常识

```csharp
// 检查常识是否存在
bool exists = CommonKnowledgeAPI.ExistsKnowledge(id);
if (!exists)
{
    Log.Warning($"Knowledge not found: {id}");
}
```

### 3. API 返回 null

```csharp
// 检查游戏状态
if (Current.Game == null)
{
    Log.Warning("Game not loaded yet!");
    return;
}

// 确保在主线程调用
LongEventHandler.QueueLongEvent(() =>
{
    var id = CommonKnowledgeAPI.AddKnowledge("test", "test content");
}, "AddingKnowledge", false, null);
```

## ?? 完整示例

```csharp
using RimTalk.Memory;
using Verse;

namespace MyMod
{
    public class MyKnowledgeManager
    {
        private Dictionary<string, string> knowledgeIds = new Dictionary<string, string>();

        public void Initialize()
        {
            // 添加基础规则
            string ruleId = CommonKnowledgeAPI.AddKnowledgeEx(
                tag: "规则,MyMod",
                content: "你是一个魔法世界的角色",
                importance: 0.9f,
                canBeExtracted: false,
                canBeMatched: false
            );
            knowledgeIds["rule"] = ruleId;

            // 添加世界观
            var worldKnowledge = new List<(string, string)>
            {
                ("世界观,魔法", "魔法能量来自月光"),
                ("世界观,种族", "精灵族擅长治疗魔法"),
                ("世界观,历史", "古代文明已经消失")
            };
            CommonKnowledgeAPI.AddKnowledgeBatch(worldKnowledge, 0.7f);

            // 输出统计
            var stats = CommonKnowledgeAPI.GetStats();
            Log.Message($"[MyMod] Initialized {stats.TotalCount} knowledge entries");
        }

        public void OnEventStart(string eventName, string description)
        {
            // 添加事件常识
            string eventId = CommonKnowledgeAPI.AddKnowledge(
                tag: $"事件,活跃,{eventName}",
                content: description,
                importance: 0.8f
            );
            knowledgeIds[eventName] = eventId;
        }

        public void OnEventEnd(string eventName)
        {
            // 删除事件常识
            if (knowledgeIds.TryGetValue(eventName, out string eventId))
            {
                CommonKnowledgeAPI.RemoveKnowledge(eventId);
                knowledgeIds.Remove(eventName);
            }
        }

        public void Cleanup()
        {
            // 清理所有 MyMod 相关的常识
            int removed = CommonKnowledgeAPI.RemoveKnowledgeByTag("MyMod");
            Log.Message($"[MyMod] Removed {removed} knowledge entries");
        }
    }
}
```

## ?? 相关链接

- [RimTalk GitHub](https://github.com/your-repo)
- [Steam 创意工坊](https://steamcommunity.com/your-workshop)
- [API 更新日志](CHANGELOG.md)

## ?? 许可证

本 API 遵循 RimTalk 的许可证条款。
