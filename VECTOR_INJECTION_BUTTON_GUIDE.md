# ?? 向量库手动注入功能实现指南

## ?? 方案B：在导入区域添加"直接注入向量库"按钮

### ? 实现步骤

#### 1. 添加翻译键（Languages/ChineseSimplified/Keyed/MemoryPatch.xml）

在`MemoryPatch.xml`的`<LanguageData>`中添加：

```xml
<!-- 向量库注入功能 -->
<RimTalk_Knowledge_InjectToVectorDB>注入向量库</RimTalk_Knowledge_InjectToVectorDB>
<RimTalk_Knowledge_InjectToVectorDBDesc>直接解析文本并注入到向量数据库</RimTalk_Knowledge_InjectToVectorDBDesc>
<RimTalk_Knowledge_VectorDBInjectionSuccess>成功注入 {0} 条到向量数据库</RimTalk_Knowledge_VectorDBInjectionSuccess>
<RimTalk_Knowledge_VectorDBNotEnabled>向量数据库未启用，请在Mod设置中启用</RimTalk_Knowledge_VectorDBNotEnabled>
<RimTalk_Knowledge_VectorDBInjectionFailed>向量库注入失败: {0}</RimTalk_Knowledge_VectorDBInjectionFailed>
```

---

#### 2. 修改 `Dialog_CommonKnowledge.cs` 的 `DrawToolbar` 方法

在**导入按钮**后面添加**注入向量库**按钮：

```csharp
private void DrawToolbar(Rect rect)
{
    float buttonWidth = 100f;
    float spacing = 5f;
    float x = 0f;

    // 新建按钮
    if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_New".Translate()))
    {
        CreateNewEntry();
    }
    x += buttonWidth + spacing;

    // 导入按钮
    if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_Import".Translate()))
    {
        ShowImportDialog();
    }
    x += buttonWidth + spacing;

    // ? 新增：注入向量库按钮
    if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth + 20f, 35f), "RimTalk_Knowledge_InjectToVectorDB".Translate()))
    {
        ShowVectorDBInjectionDialog();
    }
    x += buttonWidth + spacing + 20f;

    // 导出按钮
    if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_Export".Translate()))
    {
        ExportToFile();
    }
    x += buttonWidth + spacing;

    // ... 其他按钮保持不变
}
```

---

#### 3. 添加 `ShowVectorDBInjectionDialog()` 方法

```csharp
/// <summary>
/// 显示向量库注入对话框
/// </summary>
private void ShowVectorDBInjectionDialog()
{
    // 检查向量数据库是否启用
    var settings = RimTalkMemoryPatchMod.Settings;
    if (!settings.enableVectorDatabase)
    {
        Messages.Message("RimTalk_Knowledge_VectorDBNotEnabled".Translate(), MessageTypeDefOf.RejectInput, false);
        return;
    }

    Dialog_TextInput dialog = new Dialog_TextInput(
        "注入向量数据库",
        "粘贴常识文本，每行格式：[标签|重要性]内容\n注入后数据将持久化到VectorDB，可用于语义检索",
        "",
        delegate(string text)
        {
            InjectTextToVectorDB(text);
        },
        null,
        multiline: true
    );
    
    Find.WindowStack.Add(dialog);
}
```

---

#### 4. 添加核心注入逻辑 `InjectTextToVectorDB()`

```csharp
/// <summary>
/// 将文本解析后直接注入向量数据库
/// </summary>
private void InjectTextToVectorDB(string text)
{
    try
    {
        if (string.IsNullOrEmpty(text))
        {
            Messages.Message("输入内容为空", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var settings = RimTalkMemoryPatchMod.Settings;
        if (!settings.enableVectorDatabase)
        {
            Messages.Message("RimTalk_Knowledge_VectorDBNotEnabled".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        // 获取VectorDB管理器
        var memoryManager = Find.World.GetComponent<MemoryManager>();
        if (memoryManager?.VectorDBManager == null)
        {
            Messages.Message("VectorDB管理器未初始化", MessageTypeDefOf.RejectInput, false);
            return;
        }

        // 解析文本（复用现有逻辑）
        int injectedCount = 0;
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // 解析格式: [标签|重要性]内容
            var entry = ParseLine(trimmedLine);
            if (entry != null)
            {
                try
                {
                    // 生成向量并存储到VectorDB
                    if (settings.enableSemanticEmbedding)
                    {
                        // 使用语义嵌入
                        var embeddingService = AI.EmbeddingService.Instance;
                        float[] vector = embeddingService.GetEmbedding(entry.content);
                        
                        if (vector != null && vector.Length > 0)
                        {
                            memoryManager.VectorDBManager.StoreKnowledgeVector(
                                entry.id,
                                entry.tag,
                                entry.content,
                                vector,
                                entry.importance
                            );
                            injectedCount++;
                        }
                    }
                    else
                    {
                        // 降级：使用简单哈希向量
                        float[] vector = GenerateFallbackVector(entry.content);
                        memoryManager.VectorDBManager.StoreKnowledgeVector(
                            entry.id,
                            entry.tag,
                            entry.content,
                            vector,
                            entry.importance
                        );
                        injectedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[VectorDB Injection] Failed to inject entry: {ex.Message}");
                }
            }
        }

        if (injectedCount > 0)
        {
            Messages.Message(
                "RimTalk_Knowledge_VectorDBInjectionSuccess".Translate(injectedCount),
                MessageTypeDefOf.PositiveEvent,
                false
            );
            Log.Message($"[VectorDB Injection] Successfully injected {injectedCount} knowledge entries");
        }
        else
        {
            Messages.Message("没有有效的常识条目被注入", MessageTypeDefOf.NeutralEvent, false);
        }
    }
    catch (Exception ex)
    {
        Log.Error($"[VectorDB Injection] Error: {ex.Message}\n{ex.StackTrace}");
        Messages.Message(
            "RimTalk_Knowledge_VectorDBInjectionFailed".Translate(ex.Message),
            MessageTypeDefOf.RejectInput,
            false
        );
    }
}

/// <summary>
/// 生成降级向量（当语义嵌入不可用时）
/// </summary>
private float[] GenerateFallbackVector(string text)
{
    // 简单的哈希向量（384维，匹配text-embedding-ada-002）
    float[] vector = new float[384];
    int hash = text.GetHashCode();
    
    for (int i = 0; i < 384; i++)
    {
        vector[i] = ((hash + i * 37) % 1000) / 1000f;
    }
    
    return vector;
}

/// <summary>
/// 解析单行文本（复用现有逻辑 - 已在CommonKnowledgeLibrary.cs中定义）
/// </summary>
private CommonKnowledgeEntry ParseLine(string line)
{
    // ? 这里调用CommonKnowledgeLibrary的ParseLine方法
    // 由于该方法是private，需要创建临时Library实例来解析
    var tempLibrary = new CommonKnowledgeLibrary();
    
    if (string.IsNullOrEmpty(line))
        return null;

    // 查找 [标签]
    int tagStart = line.IndexOf('[');
    int tagEnd = line.IndexOf(']');

    if (tagStart == -1 || tagEnd == -1 || tagEnd <= tagStart)
    {
        // 没有标签，整行作为内容，默认重要性0.5
        return new CommonKnowledgeEntry("通用", line) { importance = 0.5f };
    }

    // 提取标签部分
    string tagPart = line.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
    string content = line.Substring(tagEnd + 1).Trim();

    if (string.IsNullOrEmpty(content))
        return null;

    // 解析标签和重要性
    string tag;
    float importance = 0.5f; // 默认重要性

    // 检查是否包含重要性 (格式: 标签|0.8)
    int pipeIndex = tagPart.IndexOf('|');
    if (pipeIndex > 0)
    {
        tag = tagPart.Substring(0, pipeIndex).Trim();
        string importanceStr = tagPart.Substring(pipeIndex + 1).Trim();
        
        // 尝试解析重要性
        if (!float.TryParse(importanceStr, out importance))
        {
            importance = 0.5f; // 解析失败，使用默认值
        }
        
        // 限制重要性范围 [0, 1]
        importance = Math.Max(0f, Math.Min(1f, importance));
    }
    else
    {
        // 旧格式，没有重要性
        tag = tagPart;
    }

    return new CommonKnowledgeEntry(tag, content) { importance = importance };
}
```

---

### ?? 调用VectorDB API

确保`VectorDBManager`有以下方法（如果没有需要添加）：

```csharp
// Source/Memory/VectorDB/VectorDBManager.cs

/// <summary>
/// 存储常识向量
/// </summary>
public void StoreKnowledgeVector(string knowledgeId, string tag, string content, float[] vector, float importance)
{
    if (!RimTalkMemoryPatchMod.Settings.enableVectorDatabase)
        return;

    try
    {
        database.StoreVector(
            vectorId: knowledgeId,
            content: content,
            vector: vector,
            metadata: new Dictionary<string, string>
            {
                { "type", "knowledge" },
                { "tag", tag },
                { "importance", importance.ToString("F2") }
            }
        );
    }
    catch (Exception ex)
    {
        Log.Error($"[VectorDB] Failed to store knowledge vector: {ex.Message}");
    }
}
```

---

### ?? 用户体验流程

1. **用户点击"注入向量库"按钮**
2. **弹出文本输入框**（类似导入对话框）
3. **粘贴常识文本：**
   ```
   [世界观|0.9]边缘世界，科技倒退
   [规则|0.8]回复80字内，口语化
   [危机|0.7]海盗定期袭击
   ```
4. **点击"确认"**
5. **系统自动：**
   - 解析文本 → 生成向量 → 存储到VectorDB
   - 显示成功消息："成功注入3条到向量数据库"
6. **数据持久化：** SQLite存储，跨存档可用（如果启用共享模式）

---

### ? 性能优化

- **批量注入：** 一次性提交多个向量到DB（减少I/O）
- **异步处理：** 大量数据时后台处理，避免UI卡顿
- **降级策略：** 语义嵌入不可用时使用简单哈希向量

---

### ?? 测试步骤

1. **启用向量数据库：** Mod设置 → 实验性功能 → 向量数据库 ?
2. **（可选）启用语义嵌入：** 提升向量质量
3. **打开常识库管理：** 点击"注入向量库"按钮
4. **粘贴测试数据：**
   ```
   [测试|0.5]这是一条测试常识
   [世界观|0.9]边缘世界设定
   ```
5. **确认注入 → 查看日志：** 确认VectorDB存储成功

---

### ?? 修改的文件清单

1. **Languages/ChineseSimplified/Keyed/MemoryPatch.xml** - 添加5个翻译键
2. **Source/Memory/UI/Dialog_CommonKnowledge.cs** - 添加3个方法（~100行代码）
3. **Source/Memory/VectorDB/VectorDBManager.cs** - 确认`StoreKnowledgeVector`方法存在

---

### ?? 优势总结

? **简单易用：** 粘贴 → 点击 → 完成  
? **无需常识库：** 直接注入VectorDB，不污染常识库  
? **向后兼容：** 不影响现有导入功能  
? **高性能：** 批量处理，支持降级  
? **易于扩展：** 未来可添加记忆批量注入功能  

---

## ?? 部署命令

修改完成后，运行以下命令部署：

```bash
.\deploy-simple.bat
```

---

**需要我现在开始实现这些修改吗？** ??
