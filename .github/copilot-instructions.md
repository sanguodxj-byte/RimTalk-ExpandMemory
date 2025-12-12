# GitHub Copilot 工作区指令

## 项目配置

### RimWorld 路径配置
- **安装目录**：`D:\steam\steamapps\common\RimWorld`
- **Mod 文件夹**：`D:\steam\steamapps\common\RimWorld\Mods`
- **当前 Mod 名称**：`RimTalk-MemoryPatch`
- **游戏版本**：`1.6`

### 环境变量
- `RIMWORLD_DIR` = `D:\steam\steamapps\common\RimWorld`

## 部署流程

### 标准部署（推荐）
```powershell
.\deploy.ps1
```

### 手动构建和部署
```powershell
# 1. 构建项目
dotnet build

# 2. 部署到游戏
.\deploy.ps1
```

### 自动部署（Visual Studio）
- 构建项目后自动部署（基于 `RIMWORLD_DIR` 环境变量）
- 目标路径：`$(RIMWORLD_DIR)\Mods\RimTalk-MemoryPatch`

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

## 代码约定

### 命名空间
- 主命名空间：`RimTalk.Memory`
- 补丁命名空间：`RimTalk.Memory.Patches`
- UI 命名空间：`RimTalk.Memory.UI`

### 关键类
- `DynamicMemoryInjection` - 场景感知记忆注入系统
- `SceneAnalyzer` - 高级场景分析（7种场景）
- `CommonKnowledgeLibrary` - 常识库管理
- `FourLayerMemoryComp` - 四层记忆组件

### 设计原则
- **无上帝模式**：TimeDecay 永不为 0，所有记忆都会衰减
- **场景聚焦**：根据场景调整关注点，但严格遵守评分结果
- **用户优先**：用户编辑和固定记忆的绝对优先级

## AI 助手指令

在后续所有涉及以下操作时，请使用上述配置：

1. **路径相关操作**
   - 使用 `D:\steam\steamapps\common\RimWorld` 作为 RimWorld 根目录
   - 使用 `D:\steam\steamapps\common\RimWorld\Mods\RimTalk-MemoryPatch` 作为 Mod 部署目录

2. **构建和部署**
   - 推荐使用 `.\deploy.ps1` 脚本
   - 确认 `RIMWORLD_DIR` 环境变量已设置

3. **代码生成**
   - 遵循现有命名空间约定
   - 保持设计哲学一致（无上帝模式、场景聚焦、用户优先）

4. **文件创建**
   - 遵循现有文件结构
   - C# 文件放在 `Source\` 对应子目录
   - 资源文件放在对应资源目录

## 常用命令

```powershell
# 快速部署
.\deploy.ps1

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

## 注意事项

1. **路径分隔符**：Windows 使用反斜杠 `\`，但在代码中建议使用 `Path.Combine()` 或正斜杠 `/`
2. **权限**：部署到 Steam 目录可能需要管理员权限
3. **游戏版本**：当前支持 RimWorld 1.6，目标框架 .NET Framework 4.7.2
4. **依赖项**：Harmony 2.x，无需 SQLite（VectorDB 已移除）

---

**最后更新**：2025-01-13
**项目版本**：v3.3.12
