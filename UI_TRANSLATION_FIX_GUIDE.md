# MainTabWindow_Memory.cs 翻译键修复指南

## 问题说明

`MainTabWindow_Memory.cs` 中有大量硬编码的英文字符串，需要全部替换为翻译键（`.Translate()`）。

## 需要修复的位置

### 1. DrawMemoryCard 方法（约第600-700行）

**Pin/Unpin tooltip:**
```csharp
// 修复前：
TooltipHandler.TipRegion(pinButtonRect, memory.isPinned ? "Unpin" : "Pin");

// 修复后：
TooltipHandler.TipRegion(pinButtonRect, memory.isPinned ? "RimTalk_MindStream_Unpin".Translate() : "RimTalk_MindStream_Pin".Translate());
```

**Edit tooltip:**
```csharp
// 修复前：
TooltipHandler.TipRegion(editButtonRect, "Edit");

// 修复后：
TooltipHandler.TipRegion(editButtonRect, "RimTalk_MindStream_Edit".Translate());
```

**Header "with" text:**
```csharp
// 修复前：
header += $" ? with {memory.relatedPawnName}";

// 修复后：
header += $" ? {"RimTalk_MindStream_With".Translate()} {memory.relatedPawnName}";
```

**Importance/Activity tooltips:**
```csharp
// 修复前：
TooltipHandler.TipRegion(importanceBarRect, $"Importance: {memory.importance:F2}");
TooltipHandler.TipRegion(activityBarRect, $"Activity: {memory.activity:F2}");

// 修复后：
TooltipHandler.TipRegion(importanceBarRect, "RimTalk_MindStream_ImportanceLabel".Translate(memory.importance.ToString("F2")));
TooltipHandler.TipRegion(activityBarRect, "RimTalk_MindStream_ActivityLabel".Translate(memory.activity.ToString("F2")));
```

### 2. SummarizeSelectedMemories 方法（约第800行）

```csharp
// 修复前：
Messages.Message("No SCM memories selected", MessageTypeDefOf.RejectInput, false);
Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
    $"Summarize {scmMemories.Count} SCM memories to ELS?",
    delegate { /*...*/ }
));
Messages.Message($"Summarized {scmMemories.Count} memories", MessageTypeDefOf.PositiveEvent, false);

// 修复后：
Messages.Message("RimTalk_MindStream_NoSCMSelected".Translate(), MessageTypeDefOf.RejectInput, false);
Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
    "RimTalk_MindStream_SummarizeConfirm".Translate(scmMemories.Count),
    delegate { /*...*/ }
));
Messages.Message("RimTalk_MindStream_SummarizedN".Translate(scmMemories.Count), MessageTypeDefOf.PositiveEvent, false);
```

### 3. ArchiveSelectedMemories 方法（约第830行）

```csharp
// 修复前：
Messages.Message("No ELS memories selected", MessageTypeDefOf.RejectInput, false);
Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
    $"Archive {elsMemories.Count} ELS memories to CLPA?",
    delegate { /*...*/ }
));
Messages.Message($"Archived {elsMemories.Count} memories", MessageTypeDefOf.PositiveEvent, false);

// 修复后：
Messages.Message("RimTalk_MindStream_NoELSSelected".Translate(), MessageTypeDefOf.RejectInput, false);
Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
    "RimTalk_MindStream_ArchiveConfirm".Translate(elsMemories.Count),
    delegate { /*...*/ }
));
Messages.Message("RimTalk_MindStream_ArchivedN".Translate(elsMemories.Count), MessageTypeDefOf.PositiveEvent, false);
```

### 4. DeleteSelectedMemories 方法（约第860行）

```csharp
// 修复前：
Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
    $"Delete {count} selected memories?",
    delegate { /*...*/ }
));
Messages.Message($"Deleted {count} memories", MessageTypeDefOf.PositiveEvent, false);

// 修复后：
Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
    "RimTalk_MindStream_DeleteConfirm".Translate(count),
    delegate { /*...*/ }
));
Messages.Message("RimTalk_MindStream_DeletedN".Translate(count), MessageTypeDefOf.PositiveEvent, false);
```

### 5. SummarizeAll 方法（约第890行）

```csharp
// 修复前：
Messages.Message($"Queued {pawnsToSummarize.Count} colonists for summarization", MessageTypeDefOf.TaskCompletion, false);
Messages.Message("No colonists need summarization", MessageTypeDefOf.RejectInput, false);

// 修复后：
Messages.Message("RimTalk_MindStream_QueuedSummarization".Translate(pawnsToSummarize.Count), MessageTypeDefOf.TaskCompletion, false);
Messages.Message("RimTalk_MindStream_NoNeedSummarization".Translate(), MessageTypeDefOf.RejectInput, false);
```

### 6. ArchiveAll 方法（约第910行）

```csharp
// 修复前：
Messages.Message($"Archived memories for {count} colonists", MessageTypeDefOf.PositiveEvent, false);

// 修复后：
Messages.Message("RimTalk_MindStream_ArchivedForN".Translate(count), MessageTypeDefOf.PositiveEvent, false);
```

### 7. DrawNoPawnSelected 方法（约第1010行）

```csharp
// 修复前：
Widgets.Label(rect, "Select a colonist to view memories");

// 修复后：
Widgets.Label(rect, "RimTalk_MindStream_SelectColonist".Translate());
```

### 8. DrawNoMemoryComponent 方法（约第1020行）

```csharp
// 修复前：
Widgets.Label(rect, "Selected pawn has no memory component");

// 修复后：
Widgets.Label(rect, "RimTalk_MindStream_NoMemoryComp".Translate());
```

### 9. OpenCommonKnowledgeDialog 方法（约第1030行）

```csharp
// 修复前：
Messages.Message("Must enter game first", MessageTypeDefOf.RejectInput, false);
Messages.Message("Cannot find memory manager", MessageTypeDefOf.RejectInput, false);

// 修复后：
Messages.Message("RimTalk_MindStream_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput, false);
Messages.Message("RimTalk_MindStream_CannotFindManager".Translate(), MessageTypeDefOf.RejectInput, false);
```

## 左侧面板显示不全问题

### 问题描述
控制面板内容过多，底部的"Summarize All"和"Archive All"按钮被遮挡。

### 解决方案
在`DrawControlPanel`方法中使用滚动视图：

```csharp
private void DrawControlPanel(Rect rect)
{
    Widgets.DrawMenuSection(rect);
    
    // ? 添加滚动视图
    Rect innerRect = rect.ContractedBy(SPACING);
    
    // 计算总内容高度
    float contentHeight = 800f; // 根据实际内容调整
    Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, contentHeight);
    
    Vector2 scrollPosition = Vector2.zero; // 需要添加为类成员变量
    Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);
    
    float y = 0f;
    
    // Title
    Text.Font = GameFont.Medium;
    Widgets.Label(new Rect(0f, y, viewRect.width, 30f), "RimTalk_MindStream_MemoryFilters".Translate());
    Text.Font = GameFont.Small;
    y += 35f;
    
    // ... 其他内容 ...
    
    Widgets.EndScrollView();
}
```

**需要添加的类成员变量：**
```csharp
private Vector2 controlPanelScrollPosition = Vector2.zero;
```

## 记忆修改和固定功能问题

### Dialog_EditMemory 缺失

如果编译器报错找不到`Dialog_EditMemory`，需要创建这个类。

参考代码：
```csharp
public class Dialog_EditMemory : Window
{
    private MemoryEntry memory;
    private FourLayerMemoryComp memoryComp;
    private string editedContent;
    
    public override Vector2 InitialSize => new Vector2(600f, 400f);
    
    public Dialog_EditMemory(MemoryEntry memory, FourLayerMemoryComp memoryComp)
    {
        this.memory = memory;
        this.memoryComp = memoryComp;
        this.editedContent = memory.content;
        this.doCloseX = true;
    }
    
    public override void DoWindowContents(Rect inRect)
    {
        // 编辑UI
        Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "RimTalk_MindStream_Edit".Translate());
        
        // 文本框
        Rect textRect = new Rect(0f, 40f, inRect.width, inRect.height - 90f);
        editedContent = Widgets.TextArea(textRect, editedContent);
        
        // 按钮
        if (Widgets.ButtonText(new Rect(0f, inRect.height - 40f, 100f, 35f), "Save".Translate()))
        {
            memory.content = editedContent;
            memory.isUserEdited = true;
            Close();
        }
        
        if (Widgets.ButtonText(new Rect(110f, inRect.height - 40f, 100f, 35f), "Cancel".Translate()))
        {
            Close();
        }
    }
}
```

## 检查清单

- [ ] 修复所有硬编码英文为翻译键
- [ ] 添加控制面板滚动视图
- [ ] 确保Dialog_EditMemory类存在
- [ ] 测试固定功能（PinMemory方法）
- [ ] 测试编辑功能
- [ ] 验证所有翻译键都在XML中定义
- [ ] 中英文都要测试

## 快速测试

1. 启动游戏，切换语言到英文/中文
2. 打开Memory标签页
3. 检查是否还有翻译键显示（如"RimTalk_xxx"）
4. 测试固定按钮（??图标）
5. 测试编辑按钮（??图标）
6. 滚动左侧面板，确认底部按钮可见

## 注意事项

- 所有用户可见的文本都必须使用`.Translate()`
- Tooltip也需要翻译
- 确认翻译键在两个XML文件中都存在（English和ChineseSimplified）
