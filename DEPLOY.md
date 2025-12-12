# RimTalk-ExpandMemory Mod 部署指南

## ?? 快速部署

### 方法1：使用自动部署脚本（推荐）

```powershell
# 直接运行部署脚本
.\deploy.ps1
```

### 方法2：Visual Studio 自动部署

构建项目后会自动部署到 RimWorld Mod 目录（需要设置环境变量）。

---

## ?? 配置 RimWorld 路径

### 永久配置（推荐）

**已为您配置好的路径：**
```
D:\steam\steamapps\common\RimWorld
```

环境变量已设置为 `RIMWORLD_DIR`，重启 Visual Studio 后自动生效。

### 修改路径（如需更换）

1. **修改环境变量：**
   ```powershell
   setx RIMWORLD_DIR "新的RimWorld路径"
   ```

2. **修改配置文件：**
   编辑 `.rimworld.config` 文件，更新 `RIMWORLD_DIR` 的值。

3. **手动指定路径：**
   ```powershell
   .\deploy.ps1 -RimWorldDir "新的RimWorld路径"
   ```

---

## ?? Mod 文件结构

部署后的目录结构：
```
D:\steam\steamapps\common\RimWorld\Mods\RimTalk-MemoryPatch\
├── About\              # Mod 信息和预览图
├── Defs\               # 游戏定义文件
├── Languages\          # 多语言翻译
├── Textures\           # 纹理资源
└── 1.6\                # 游戏版本专属文件
    └── Assemblies\     # 编译后的 DLL
```

---

## ?? 启用 Mod

1. 启动 RimWorld
2. 主菜单 → **Mods**
3. 找到 `RimTalk-MemoryPatch`
4. 勾选启用
5. 重启游戏

---

## ?? 开发流程

### 1. 修改代码

在 `Source\` 目录下编辑 C# 源码。

### 2. 构建项目

**Visual Studio：**
- 按 `Ctrl+Shift+B` 或 菜单 → **生成** → **生成解决方案**

**命令行：**
```powershell
dotnet build
```

### 3. 部署到游戏

**自动部署（推荐）：**
- 构建项目后自动部署（需环境变量）

**手动部署：**
```powershell
.\deploy.ps1
```

### 4. 测试

- 启动 RimWorld
- 启用 Mod
- 测试功能

---

## ?? 配置文件说明

### `.rimworld.config`

存储 RimWorld 路径和部署配置的本地文件。

```ini
RIMWORLD_DIR=D:\steam\steamapps\common\RimWorld
MOD_FOLDER_NAME=RimTalk-MemoryPatch
GAME_VERSION=1.6
```

### `deploy.ps1`

PowerShell 部署脚本，支持自定义参数：

```powershell
# 使用默认配置
.\deploy.ps1

# 自定义路径
.\deploy.ps1 -RimWorldDir "C:\Games\RimWorld"

# 自定义 Mod 名称和版本
.\deploy.ps1 -ModName "MyMod" -GameVersion "1.5"
```

---

## ? 常见问题

### Q: 环境变量未生效？

**A:** 设置环境变量后需要**重启 Visual Studio**。

### Q: 部署失败，提示权限不足？

**A:** 以**管理员身份**运行 PowerShell 或 Visual Studio。

### Q: robocopy 返回错误码？

**A:** robocopy 返回码 0-7 都是成功，8+ 才是错误。脚本已处理。

### Q: 如何查看当前环境变量？

```powershell
echo $env:RIMWORLD_DIR
```

---

## ?? GitHub Copilot 提示词（让 AI 记住路径）

您可以在与 Copilot 对话时使用以下提示词：

```
# 工作区配置提示词

我的 RimWorld 安装路径是：
D:\steam\steamapps\common\RimWorld

Mod 文件夹路径是：
D:\steam\steamapps\common\RimWorld\Mods

当前项目 Mod 名称是：
RimTalk-MemoryPatch

请在后续所有涉及路径的操作中使用这些配置。
```

或者创建 `.github/copilot-instructions.md` 文件（如果使用 GitHub Copilot Workspace）：

```markdown
# Copilot 工作区指令

## RimWorld 配置
- 安装路径：`D:\steam\steamapps\common\RimWorld`
- Mod 文件夹：`D:\steam\steamapps\common\RimWorld\Mods`
- 当前 Mod：`RimTalk-MemoryPatch`

## 部署流程
1. 构建项目：`dotnet build`
2. 运行部署：`.\deploy.ps1`
3. 启动游戏测试
```

---

## ?? 相关资源

- [RimWorld Modding Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials)
- [Harmony Documentation](https://harmony.pardeike.net/)
- [项目 GitHub](https://github.com/sanguodxj-byte/RimTalk-ExpandMemory)

---

## ? 检查清单

部署前确认：
- [x] 环境变量已设置（`RIMWORLD_DIR`）
- [x] 项目已构建成功
- [x] `.rimworld.config` 配置正确
- [x] `deploy.ps1` 脚本可执行

部署后确认：
- [x] Mod 文件夹存在：`D:\steam\steamapps\common\RimWorld\Mods\RimTalk-MemoryPatch`
- [x] DLL 文件存在：`1.6\Assemblies\RimTalkMemoryPatch.dll`
- [x] RimWorld 中能看到 Mod

---

**?? 祝您开发愉快！**
