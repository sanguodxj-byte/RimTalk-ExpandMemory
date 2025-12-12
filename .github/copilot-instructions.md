# GitHub Copilot 工作区指令

## 项目配置

### RimWorld 路径配置（用户本地）

**重要：** RimWorld 路径是用户特定的，不应硬编码在代码中。

**配置方式：**
- **本地配置文件**：`.rimworld.config`（用户创建，不提交）
- **环境变量**：`RIMWORLD_DIR`
- **示例模板**：`.rimworld.config.example`（提供给用户参考）

**常见路径（仅供参考）：**
- Steam (Windows)：`C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
- Steam (Linux)：`~/.steam/steam/steamapps/common/RimWorld`
- Steam (macOS)：`~/Library/Application Support/Steam/steamapps/common/RimWorld`

### 项目固定配置

- **Mod 名称**：`RimTalk-MemoryPatch`
- **游戏版本**：`1.6`
- **目标框架**：`.NET Framework 4.7.2`

---

## 部署流程

### 标准部署（推荐）
```powershell
.\deploy.ps1
```

### 首次配置
```powershell
# 1. 创建本地配置
Copy-Item .rimworld.config.example .rimworld.config
Copy-Item deploy.ps1.example deploy.ps1

# 2. 编辑 .rimworld.config 填入用户的 RimWorld 路径

# 3. 部署
.\deploy.ps1
```

### 手动构建和部署
```powershell
# 1. 构建项目
dotnet build

# 2. 部署到游戏
.\deploy.ps1
```

---

## 文件结构约定

### 源代码目录
- `Source\Memory\` - 核心记忆系统
- `Source\Memory\UI\` - 用户界面
- `Source\Patches\` - Harmony 补丁

### 资源目录
- `About\` - Mod 信息和预览图
- `Defs\` - 游戏定义 XML
- `Languages\` - 多语言翻译
- `Textures\` - UI 纹理
- `1.6\Assemblies\` - 编译输出目录

### 配置文件（用户本地，不提交）
- `.rimworld.config` - 用户的 RimWorld 路径
- `deploy.ps1` - 用户的部署脚本

### 示例模板（提交到仓库）
- `.rimworld.config.example` - 配置文件模板
- `deploy.ps1.example` - 部署脚本模板
- `DEPLOY.md` - 详细部署指南

---

## 代码约定

### 命名空间
- 主命名空间：`RimTalk.Memory`
- 补丁命名空间：`RimTalk.Memory.Patches`
- UI 命名空间：`RimTalk.Memory.UI`

### 关键类
- `DynamicMemoryInjection` - 场景感知记忆注入系统（简化版，4种场景）
- `SceneAnalyzer` - 高级场景分析（可选，7种场景）
- `CommonKnowledgeLibrary` - 常识库管理
- `FourLayerMemoryComp` - 四层记忆组件

### 设计原则
- **无上帝模式**：TimeDecay 永不为 0，所有记忆都会衰减
- **场景聚焦**：根据场景调整关注点，但严格遵守评分结果
- **用户优先**：用户编辑和固定记忆的绝对优先级

---

## AI 助手指令

### 路径处理规则

**永远不要：**
- ? 在代码中硬编码具体的 RimWorld 路径
- ? 假设所有用户使用相同的路径
- ? 将用户本地配置文件提交到 Git

**应该：**
- ? 使用环境变量：`$env:RIMWORLD_DIR`
- ? 从配置文件读取：`.rimworld.config`
- ? 提供示例模板：`.rimworld.config.example`
- ? 在文档中列出常见路径供参考

### 配置文件创建

当需要创建配置相关文件时：

```powershell
# 创建示例模板（提交到 Git）
# 文件名：xxx.example

# 提示用户复制并修改
# "请复制 xxx.example 为 xxx 并填入您的配置"
```

### 部署脚本编写

部署脚本应支持多种配置方式：

1. **优先级1**：命令行参数 `-RimWorldDir`
2. **优先级2**：环境变量 `$env:RIMWORLD_DIR`
3. **优先级3**：配置文件 `.rimworld.config`
4. **失败提示**：引导用户配置

---

## 常用命令

```powershell
# 快速部署（配置后）
.\deploy.ps1

# 首次配置
Copy-Item .rimworld.config.example .rimworld.config
Copy-Item deploy.ps1.example deploy.ps1

# 检查环境变量
echo $env:RIMWORLD_DIR

# 构建项目
dotnet build

# 清理构建
dotnet clean

# 查看 Git 状态
git status

# 提交更改
git add .
git commit -m "描述信息"
git push origin main
```

---

## 注意事项

1. **路径分隔符**：
   - Windows 使用反斜杠 `\`
   - 代码中建议使用 `Path.Combine()` 或正斜杠 `/`（跨平台）

2. **权限**：
   - 部署到 Steam 目录可能需要管理员权限

3. **平台支持**：
   - Windows：使用 `robocopy` 或 `Copy-Item`
   - Linux/macOS：使用 `rsync`

4. **游戏版本**：
   - 当前支持 RimWorld 1.6
   - 目标框架 .NET Framework 4.7.2

5. **依赖项**：
   - Harmony 2.x
   - 无需 SQLite（VectorDB 已移除）

---

## Git 提交规范

### 文件分类

**应该提交：**
- ? 源代码 (`Source\`)
- ? 资源文件 (`About\`, `Defs\`, `Languages\`, `Textures\`)
- ? 示例模板 (`*.example`)
- ? 文档 (`README.md`, `DEPLOY.md`)
- ? 项目配置 (`.csproj`, `.gitignore`)

**不应提交：**
- ? 用户本地配置 (`.rimworld.config`, `deploy.ps1`)
- ? 构建输出 (`bin\`, `obj\`, `1.6\Assemblies\`)
- ? IDE 文件 (`.vs\`, `.vscode\`, `.idea\`)
- ? 用户特定数据（路径、密钥等）

### 提交消息格式

```
类别：简短描述

详细描述（可选）

- 改动1
- 改动2
```

**类别示例：**
- `功能` - 新功能
- `修复` - Bug 修复
- `优化` - 性能优化
- `重构` - 代码重构
- `文档` - 文档更新
- `配置` - 配置文件更新

---

**最后更新**：2025-01-13
**项目版本**：v3.3.12
