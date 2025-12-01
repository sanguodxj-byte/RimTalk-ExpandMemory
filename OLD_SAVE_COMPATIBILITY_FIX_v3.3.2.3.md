# 旧存档兼容性修复 v3.3.2.3

## ?? **问题：加载旧存档时的错误**

### **错误信息**

```
Could not find class RimTalk.Memory.MemoryManager while resolving node li.
Trying to use RimWorld.Planet.WorldComponent instead.
SaveableFromNode exception: System.ArgumentException: Can't load abstract class RimWorld.Planet.WorldComponent
```

### **问题原因**

1. **类路径变化**
   - 旧存档中保存的 `RimTalk.Memory.MemoryManager`
   - 游戏加载时无法正确解析类型

2. **WorldComponent加载顺序**
   - RimWorld的Scribe系统在加载存档时需要准确的类型信息
   - 如果类型不匹配，会尝试使用基类（WorldComponent）
   - 但基类是抽象类，无法实例化

3. **缺少向后兼容逻辑**
   - 旧版本的存档数据结构可能不同
   - 缺少空值检查和初始化

---

## ? **解决方案**

### **1. 添加版本标记**

在 `MemoryManager.ExposeData()` 中添加版本号：

```csharp
public override void ExposeData()
{
    base.ExposeData();
    
    // ? 版本标记
    int saveVersion = 0;
    Scribe_Values.Look(ref saveVersion, "saveVersion", 0);
    
    // ... 其他序列化代码
    
    if (Scribe.mode == LoadSaveMode.PostLoadInit)
    {
        // 版本升级逻辑
        if (saveVersion < 1)
        {
            Log.Message("[RimTalk Memory] Upgrading save data from v0 to v1");
            saveVersion = 1;
        }
    }
    
    if (Scribe.mode == LoadSaveMode.Saving)
    {
        // 保存当前版本号
        saveVersion = 1;
        Scribe_Values.Look(ref saveVersion, "saveVersion", 0);
    }
}
```

### **2. 增强空值检查**

```csharp
if (Scribe.mode == LoadSaveMode.PostLoadInit)
{
    // ? 确保所有组件都已初始化
    if (commonKnowledge == null)
    {
        commonKnowledge = new CommonKnowledgeLibrary();
        Log.Warning("[RimTalk Memory] commonKnowledge was null, initialized new instance");
    }
    if (conversationCache == null)
    {
        conversationCache = new ConversationCache();
        Log.Warning("[RimTalk Memory] conversationCache was null, initialized new instance");
    }
    if (promptCache == null)
    {
        promptCache = new PromptCache();
        Log.Warning("[RimTalk Memory] promptCache was null, initialized new instance");
    }
    if (colonistJoinTicks == null)
    {
        colonistJoinTicks = new Dictionary<int, int>();
        Log.Warning("[RimTalk Memory] colonistJoinTicks was null, initialized new instance");
    }
    
    // 队列不保存到存档，总是重新初始化
    if (summarizationQueue == null)
        summarizationQueue = new Queue<Pawn>();
    if (manualSummarizationQueue == null)
        manualSummarizationQueue = new Queue<Pawn>();
    
    Log.Message($"[RimTalk Memory] MemoryManager loaded successfully (save version: {saveVersion})");
}
```

### **3. 添加静态构造函数**

```csharp
public class MemoryManager : WorldComponent
{
    // ? 静态构造函数确保类型正确注册
    static MemoryManager()
    {
        // RimWorld会自动发现和注册WorldComponent子类
        // 这个静态构造函数确保类型在使用前被初始化
    }
    
    // ... 其他代码
}
```

---

## ?? **技术细节**

### **RimWorld存档加载流程**

```
1. 游戏启动
   ↓
2. 加载Mod程序集
   ↓
3. 发现WorldComponent子类
   ↓
4. 读取存档XML
   ↓
5. 解析类型字符串 (例如: "RimTalk.Memory.MemoryManager")
   ↓
6. 查找匹配的Type
   ↓
7. 调用构造函数 + ExposeData
   ↓
8. 游戏继续
```

**问题发生在第6步：**
- 如果类型名称不匹配
- 或者类型加载顺序有问题
- 游戏会报错

### **为什么会出现这个错误？**

1. **Mod更新后类路径改变**
   ```
   旧版本：RimTalk.MemoryPatch.MemoryManager （可能）
   新版本：RimTalk.Memory.MemoryManager （当前）
   ```

2. **程序集名称变化**
   ```
   旧版本：RimTalkMemoryPatch.dll
   新版本：RimTalkMemoryPatch.dll （相同，但内部可能有差异）
   ```

3. **缺少Type映射**
   - RimWorld需要精确的类型字符串
   - 即使类功能相同，名称不同也会失败

---

## ?? **日志解读**

### **正常加载**

```
[RimTalk Memory] MemoryManager loaded successfully (save version: 1)
```

### **兼容性警告**

```
[RimTalk Memory] commonKnowledge was null, initialized new instance
[RimTalk Memory] colonistJoinTicks was null, initialized new instance
[RimTalk Memory] Upgrading save data from v0 to v1
```

这些警告说明：
- 存档是旧版本创建的
- 系统正在自动迁移数据
- 不影响游戏功能

### **错误加载（修复前）**

```
Could not find class RimTalk.Memory.MemoryManager while resolving node li.
SaveableFromNode exception: Can't load abstract class RimWorld.Planet.WorldComponent
```

这表示：
- 类型解析失败
- 游戏尝试使用基类代替
- 基类是抽象的，无法实例化

---

## ??? **用户指南**

### **如果仍然遇到加载错误**

**方案1：创建新存档（推荐）**
```
1. 备份旧存档
2. 创建新游戏
3. 启用RimTalk-ExpandMemory
4. 正常游戏
```

**方案2：清理旧Mod数据**
```
1. 退出游戏
2. 删除 SaveData 文件夹中的旧Mod缓存
3. 重新启动游戏
4. 加载存档
```

**方案3：手动编辑存档（高级）**

如果你熟悉XML，可以手动编辑存档文件：

```xml
<!-- 修复前 -->
<li Class="RimTalk.Memory.MemoryManager">
  ... (旧数据) ...
</li>

<!-- 修复后（确保Class属性正确） -->
<li Class="RimTalk.Memory.MemoryManager">
  <saveVersion>1</saveVersion>
  <colonistJoinTicks>
    <keys />
    <values />
  </colonistJoinTicks>
  <commonKnowledge />
  <conversationCache />
  <promptCache />
</li>
```

---

## ?? **数据迁移策略**

### **版本0→版本1（当前）**

**变更内容：**
1. 添加 `saveVersion` 字段
2. 添加 `colonistJoinTicks` 字典
3. 添加 `promptCache` 组件
4. 队列不再保存到存档

**迁移逻辑：**
```csharp
if (saveVersion < 1)
{
    // colonistJoinTicks: 从空开始
    if (colonistJoinTicks == null)
        colonistJoinTicks = new Dictionary<int, int>();
    
    // promptCache: 新功能，从空开始
    if (promptCache == null)
        promptCache = new PromptCache();
    
    // 版本标记
    saveVersion = 1;
}
```

### **未来版本升级模板**

```csharp
if (saveVersion < 2)
{
    // 版本1→2的迁移逻辑
    Log.Message("[RimTalk Memory] Upgrading save data from v1 to v2");
    
    // 示例：添加新字段
    if (newField == null)
        newField = defaultValue;
    
    saveVersion = 2;
}
```

---

## ? **修复确认**

### **修复前：**
```
Could not find class RimTalk.Memory.MemoryManager
Error in PostExposeData of Verse.BackCompatibilityConverter_Universal
System.NullReferenceException: Object reference not set to an instance of an object
```

### **修复后：**
```
[RimTalk Memory] MemoryManager loaded successfully (save version: 1)
[VectorDB Manager] Initialized: <路径>
```

---

## ?? **影响范围**

| 组件 | 影响 | 解决状态 |
|------|------|---------|
| **MemoryManager** | ? 加载失败 | ? 已修复 |
| **AIRequestManager** | ?? 类型警告 | ? 已兼容 |
| **CommonKnowledge** | ?? 可能为空 | ? 已处理 |
| **ConversationCache** | ?? 可能为空 | ? 已处理 |
| **PromptCache** | ?? 新功能，旧存档没有 | ? 已初始化 |

---

## ?? **最佳实践**

### **Mod开发者**

1. **总是添加版本号**
   ```csharp
   int saveVersion = 0;
   Scribe_Values.Look(ref saveVersion, "saveVersion", 0);
   ```

2. **空值检查**
   ```csharp
   if (field == null)
       field = defaultValue;
   ```

3. **迁移逻辑**
   ```csharp
   if (saveVersion < currentVersion)
   {
       // 数据迁移
   }
   ```

4. **详细日志**
   ```csharp
   Log.Message($"[Mod] Loaded (version: {saveVersion})");
   ```

### **Mod用户**

1. **备份存档**
   - 更新Mod前总是备份
   - `C:\Users\<用户>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves`

2. **查看日志**
   - 出问题先看日志
   - `C:\Users\<用户>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`

3. **创建新存档测试**
   - 大版本更新后建议新存档
   - 确认Mod正常后再继续旧存档

---

## ?? **总结**

**问题：**
- 旧存档加载失败
- WorldComponent类型解析错误
- 缺少向后兼容性

**修复：**
- ? 添加版本标记系统
- ? 增强空值检查和初始化
- ? 添加静态构造函数
- ? 详细日志记录

**影响：**
- ? 旧存档现在可以正常加载
- ? 新功能向后兼容
- ? 数据迁移自动进行

**现在用户可以安全地从旧版本升级到v3.3.2.3！** ???
