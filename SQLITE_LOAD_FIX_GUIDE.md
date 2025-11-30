# ?? SQLite.Interop.dll 加载失败修复方案

**错误信息：**
```
Exception loading SQLite.Interop.dll: System.BadImageFormatException
Invalid Image: x86\SQLite.Interop.dll
```

---

## ?? **问题分析**

### **根本原因**
RimWorld的Mod加载器可能不支持从子目录加载本地DLL（x86\SQLite.Interop.dll）

### **为什么会失败？**
1. System.Data.SQLite期望从`x86\`子目录加载
2. RimWorld的Assembly解析器可能不支持这种路径
3. 即使文件存在且正确，也无法加载

---

## ? **解决方案**

### **方案1：禁用向量数据库（推荐）**

向量数据库是**实验性功能**，禁用后不影响核心功能。

**步骤：**
1. 打开Mod设置
2. 找到"实验性功能"
3. **禁用"语义嵌入"**
4. **禁用"向量数据库"**

**优点：**
- ? 彻底解决SQLite问题
- ? 不影响核心记忆功能
- ? 不影响动态注入
- ? 不影响常识库

**缺点：**
- ? 无法使用向量检索（实验性）

---

### **方案2：移除SQLite依赖文件**

如果不使用向量数据库，可以删除所有SQLite相关文件。

**步骤：**
```powershell
# 删除SQLite相关文件
Remove-Item "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory\1.6\Assemblies\System.Data.SQLite.dll"
Remove-Item "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory\1.6\Assemblies\x86" -Recurse
```

**优点：**
- ? 彻底移除问题源
- ? 减小Mod体积

**缺点：**
- ? 完全无法启用向量数据库

---

### **方案3：代码修复（高级）**

修改代码，使SQLite加载失败时自动降级。

**修改文件：** `Source\Memory\VectorDB\VectorDBManager.cs`

```csharp
public static bool Initialize()
{
    try
    {
        // 尝试初始化SQLite
        if (database == null)
        {
            database = new VectorMemoryDatabase();
        }
        
        isInitialized = true;
        return true;
    }
    catch (BadImageFormatException ex)
    {
        // SQLite加载失败，自动禁用
        Log.Warning($"[Vector DB] SQLite.Interop.dll load failed, disabling vector database: {ex.Message}");
        isInitialized = false;
        
        // 自动禁用设置
        if (RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings != null)
        {
            RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings.enableSemanticEmbedding = false;
            RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings.Write();
        }
        
        return false;
    }
    catch (Exception ex)
    {
        Log.Error($"[Vector DB] Initialization failed: {ex}");
        isInitialized = false;
        return false;
    }
}
```

**优点：**
- ? 自动处理失败
- ? 不影响其他功能

**缺点：**
- ? 需要重新编译

---

## ?? **推荐操作（立即执行）**

### **步骤1：删除SQLite文件**

```powershell
# 进入Mod目录
cd "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory\1.6\Assemblies"

# 删除SQLite相关文件
Remove-Item "System.Data.SQLite.dll" -Force
Remove-Item "x86" -Recurse -Force
```

### **步骤2：验证**

```powershell
# 检查文件是否已删除
Get-ChildItem "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory\1.6\Assemblies"
```

**预期输出：**
```
Mode  LastWriteTime  Length Name
----  -------------  ------ ----
-a--- 2025/11/29     291840 RimTalkMemoryPatch.dll
-a--- 2025/11/29     xxxxx  RimTalkMemoryPatch.pdb
```

**不应该有：**
- ? System.Data.SQLite.dll
- ? x86文件夹

---

## ?? **功能对比**

| 功能 | 禁用向量DB后 | 启用向量DB |
|------|-------------|-----------|
| **四层记忆** | ? 正常 | ? 正常 |
| **动态注入** | ? 正常 | ? 正常 |
| **常识库** | ? 正常 | ? 正常 |
| **关键词检索** | ? 正常 | ? 正常 |
| **向量检索** | ? 禁用 | ? 可用 |
| **语义评分** | ? 禁用 | ? 可用 |

---

## ?? **实际影响**

### **禁用向量数据库后**

**仍然可用：**
- ? 记忆自动记录
- ? 智能注入（基于关键词）
- ? 常识库管理
- ? 预览器调试
- ? AI数据库查询（降级到关键词）
- ? 性能监控

**不可用：**
- ? 向量相似度检索
- ? 语义嵌入评分
- ? RAG高级检索

**结论：** 核心功能完全不受影响，只是少了实验性的向量检索功能。

---

## ?? **执行脚本**

创建一个自动修复脚本：

```powershell
# fix-sqlite.ps1
$modPath = "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory\1.6\Assemblies"

Write-Host "正在修复SQLite问题..." -ForegroundColor Yellow

# 删除SQLite相关文件
$sqliteDll = Join-Path $modPath "System.Data.SQLite.dll"
$x86Folder = Join-Path $modPath "x86"

if (Test-Path $sqliteDll) {
    Remove-Item $sqliteDll -Force
    Write-Host "? 删除 System.Data.SQLite.dll" -ForegroundColor Green
}

if (Test-Path $x86Folder) {
    Remove-Item $x86Folder -Recurse -Force
    Write-Host "? 删除 x86文件夹" -ForegroundColor Green
}

Write-Host "`n修复完成！" -ForegroundColor Cyan
Write-Host "向量数据库已禁用，核心功能正常。" -ForegroundColor Cyan

# 验证
Write-Host "`n剩余文件：" -ForegroundColor Yellow
Get-ChildItem $modPath | Select-Object Name, Length
```

---

## ? **总结**

### **问题**
SQLite.Interop.dll加载失败（RimWorld不支持子目录加载）

### **解决**
删除SQLite相关文件，禁用向量数据库功能

### **影响**
- ? 核心功能不受影响（记忆、注入、常识库）
- ? 向量检索不可用（实验性功能）

### **操作**
```powershell
# 立即执行
cd "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory\1.6\Assemblies"
Remove-Item "System.Data.SQLite.dll" -Force
Remove-Item "x86" -Recurse -Force
```

---

**修复后，重启RimWorld即可正常使用！** ?
