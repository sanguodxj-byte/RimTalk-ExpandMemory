# RimTalk-ExpandMemory Mod 部署指南

## ?? 快速开始

### 第一次使用？请先配置路径

**步骤1：创建配置文件**

```powershell
# 复制示例配置文件
Copy-Item .rimworld.config.example .rimworld.config
Copy-Item deploy.ps1.example deploy.ps1
```

**步骤2：编辑配置文件**

打开 `.rimworld.config`，将 `YOUR_RIMWORLD_PATH_HERE` 替换为您的 RimWorld 路径。

常见路径示例：
- **Steam (Windows)**：`C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
- **Steam (自定义)**：`D:\steam\steamapps\common\RimWorld`
- **GOG**：`C:\GOG Games\RimWorld`
- **Linux**：`~/.steam/steam/steamapps/common/RimWorld`
- **macOS**：`~/Library/Application Support/Steam/steamapps/common/RimWorld`

**步骤3：（可选）设置环境变量**

```powershell
# Windows
setx RIMWORLD_DIR "您的RimWorld路径"

# Linux/macOS（添加到 ~/.bashrc 或 ~/.zshrc）
export RIMWORLD_DIR="您的RimWorld路径"
```

### 快速部署

配置完成后，每次部署只需：

```powershell
.\deploy.ps1
```

---

## ?? 配置方式对比

| 方式 | 优点 | 缺点 | 推荐度 |
|------|------|------|--------|
| **配置文件** `.rimworld.config` | ? 项目级配置<br>? 不影响其他项目<br>? 不上传到 Git | 需要每个项目配置 | ????? |
| **环境变量** `RIMWORLD_DIR` | ? 全局生效<br>? Visual Studio 自动部署 | 需要重启 VS | ???? |
| **命令行参数** `-RimWorldDir` | ? 灵活临时使用 | 每次都要输入 | ??? |

---

## ?? 安全提示

> **?? 重要：本地配置文件已添加到 `.gitignore`**
>
> - `.rimworld.config` - 您的本地路径配置
> - `deploy.ps1` - 您的自定义部署脚本
>
> 这些文件**不会**被提交到 Git 仓库，保护您的隐私。
>
> 其他用户需要：
> 1. 复制 `.rimworld.config.example` → `.rimworld.config`
> 2. 复制 `deploy.ps1.example` → `deploy.ps1`
> 3. 填入自己的路径

---

## ?? Mod 文件结构

部署后的目录结构：
```
<RimWorld路径>\Mods\RimTalk-MemoryPatch\
├── About\              # Mod 信息和预览图
├── Defs\               # 游戏定义文件
├── Languages\          # 多语言翻译
├── Textures\           # 纹理资源
└── 1.6\                # 游戏版本专属文件
    └── Assemblies\     # 编译后的 DLL
```

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

```powershell
.\deploy.ps1
```

**或者使用自定义路径：**
```powershell
.\deploy.ps1 -RimWorldDir "C:\Games\RimWorld"
```

### 4. 测试

1. 启动 RimWorld
2. 主菜单 → **Mods**
3. 启用 `RimTalk-MemoryPatch`
4. 重启游戏

---

## ?? 配置文件说明

### `.rimworld.config`（本地配置，不提交）

```ini
# 您的 RimWorld 路径（根据实际情况修改）
RIMWORLD_DIR=D:\steam\steamapps\common\RimWorld

# Mod 名称（通常不需要修改）
MOD_FOLDER_NAME=RimTalk-MemoryPatch

# 游戏版本（通常不需要修改）
GAME_VERSION=1.6
```

### `.rimworld.config.example`（示例模板，会提交）

其他用户的参考模板，包含常见路径示例。

### `deploy.ps1`（本地脚本，不提交）

您的自定义部署脚本，可根据需要修改。

### `deploy.ps1.example`（示例模板，会提交）

其他用户的参考模板，支持多种配置方式。

---

## ? 常见问题

### Q: 为什么找不到 .rimworld.config 文件？

**A:** 这是正常的！首次使用需要您创建：

```powershell
Copy-Item .rimworld.config.example .rimworld.config
```

然后编辑 `.rimworld.config` 填入您的路径。

### Q: 我的配置会被推送到 GitHub 吗？

**A:** 不会！`.rimworld.config` 和 `deploy.ps1` 已添加到 `.gitignore`，您的本地路径是安全的。

### Q: 环境变量未生效？

**A:** 设置环境变量后需要**重启 Visual Studio** 或**重新打开 PowerShell**。

### Q: 部署失败，提示权限不足？

**A:** 以**管理员身份**运行 PowerShell：

```powershell
# 右键点击 PowerShell 图标 → 以管理员身份运行
cd "您的项目路径"
.\deploy.ps1
```

### Q: robocopy 返回错误码？

**A:** robocopy 返回码 0-7 都是成功，8+ 才是错误。脚本已处理。

### Q: 如何查看当前环境变量？

```powershell
echo $env:RIMWORLD_DIR
```

### Q: macOS/Linux 如何部署？

**A:** 使用示例脚本的 rsync 部分（已包含在 `deploy.ps1.example` 中）。

---

## ?? GitHub Copilot 配置

项目已包含 `.github/copilot-instructions.md`，GitHub Copilot 会自动读取并记住常用路径和配置。

**但请注意：** Copilot 指令文件中不应包含您的个人路径，它只包含通用的项目结构说明。

---

## ?? 相关资源

- [RimWorld Modding Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials)
- [Harmony Documentation](https://harmony.pardeike.net/)
- [项目 GitHub](https://github.com/sanguodxj-byte/RimTalk-ExpandMemory)

---

## ? 检查清单

**首次配置：**
- [ ] 复制 `.rimworld.config.example` → `.rimworld.config`
- [ ] 复制 `deploy.ps1.example` → `deploy.ps1`
- [ ] 编辑 `.rimworld.config` 填入您的 RimWorld 路径
- [ ] （可选）设置环境变量 `RIMWORLD_DIR`

**每次部署：**
- [ ] 构建项目（`dotnet build` 或 `Ctrl+Shift+B`）
- [ ] 运行部署脚本（`.\deploy.ps1`）
- [ ] 检查 Mod 文件夹是否存在
- [ ] 启动 RimWorld 并启用 Mod

---

**?? 祝您开发愉快！**
