# ?? v3.3.19 文档索引

## ?? 快速开始

**刚开始？从这里开始：**
- **[QUICKSTART_FIX.md](QUICKSTART_FIX.md)** - ? 30分钟完成修复指南（最推荐）

## ?? 核心文档

### 修复指南
| 文档 | 用途 | 适用对象 |
|------|------|----------|
| **[QUICKSTART_FIX.md](QUICKSTART_FIX.md)** | 30分钟快速完成 | 所有人 ? |
| **[HOW_TO_FIX.md](HOW_TO_FIX.md)** | 详细修复说明 | 需要深入了解的开发者 |
| **[UI_TRANSLATION_FIX_GUIDE.md](UI_TRANSLATION_FIX_GUIDE.md)** | 手动翻译修复 | 自动脚本失败时 |

### 工具脚本
| 文件 | 用途 | 使用方法 |
|------|------|----------|
| **[quick_fix_translations.ps1](quick_fix_translations.ps1)** | 自动修复翻译 | `.\quick_fix_translations.ps1` |

### 部署文档
| 文档 | 用途 | 适用对象 |
|------|------|----------|
| **[DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md)** | 完整部署总结 | 项目维护者 |
| **[DEPLOYMENT_CHECKLIST_v3.3.19.md](DEPLOYMENT_CHECKLIST_v3.3.19.md)** | 部署检查清单 | 发布前验证 |
| **[TASK_STATUS.md](TASK_STATUS.md)** | 任务状态追踪 | 项目管理 |

### 用户文档
| 文档 | 用途 | 适用对象 |
|------|------|----------|
| **[QUICKSTART_UI_v3.3.19.md](QUICKSTART_UI_v3.3.19.md)** | UI使用说明 | 最终用户 |
| **[CHANGELOG.md](CHANGELOG.md)** | 更新日志 | 所有用户 |

## ?? 按目标选择文档

### 我想快速完成修复
1. 阅读 **[QUICKSTART_FIX.md](QUICKSTART_FIX.md)** (必读)
2. 运行 `quick_fix_translations.ps1`
3. 按照3步指南操作
4. **预计时间：30分钟**

### 我想手动修复
1. 阅读 **[UI_TRANSLATION_FIX_GUIDE.md](UI_TRANSLATION_FIX_GUIDE.md)** (必读)
2. 逐个修复翻译键
3. 添加滚动视图
4. **预计时间：1小时**

### 我想了解完整状态
1. 阅读 **[TASK_STATUS.md](TASK_STATUS.md)** (已完成/待完成)
2. 阅读 **[DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md)** (详细说明)
3. **预计时间：15分钟**

### 我想准备发布
1. 完成所有修复（参考上面）
2. 使用 **[DEPLOYMENT_CHECKLIST_v3.3.19.md](DEPLOYMENT_CHECKLIST_v3.3.19.md)**
3. 更新 **[CHANGELOG.md](CHANGELOG.md)**
4. **预计时间：2小时**

### 我想学习新UI
1. 阅读 **[QUICKSTART_UI_v3.3.19.md](QUICKSTART_UI_v3.3.19.md)**
2. 打开游戏实际操作
3. **预计时间：15分钟**

## ?? 按问题类型查找

### 翻译相关
- **显示翻译键？** → [QUICKSTART_FIX.md](QUICKSTART_FIX.md) 第1步
- **脚本失败？** → [HOW_TO_FIX.md](HOW_TO_FIX.md) "常见问题"
- **手动修复？** → [UI_TRANSLATION_FIX_GUIDE.md](UI_TRANSLATION_FIX_GUIDE.md)

### UI显示问题
- **左侧面板显示不全？** → [QUICKSTART_FIX.md](QUICKSTART_FIX.md) 第2步
- **按钮被遮挡？** → [HOW_TO_FIX.md](HOW_TO_FIX.md) "控制面板滚动"
- **布局错乱？** → [DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md) "已知问题"

### 功能问题
- **固定无效？** → [DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md) "已知问题 #3"
- **编辑无效？** → 检查 `Dialog_EditMemory.cs` 是否存在
- **多选不工作？** → [QUICKSTART_UI_v3.3.19.md](QUICKSTART_UI_v3.3.19.md) "多选操作"

### 编译问题
- **编译失败？** → [QUICKSTART_FIX.md](QUICKSTART_FIX.md) "遇到问题？"
- **语法错误？** → 还原备份重试
- **缺少引用？** → 检查 `using` 语句

### 测试问题
- **测试清单？** → [DEPLOYMENT_CHECKLIST_v3.3.19.md](DEPLOYMENT_CHECKLIST_v3.3.19.md)
- **测试方法？** → [DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md) "测试检查清单"

## ?? 文档状态

| 文档 | 状态 | 最后更新 |
|------|------|----------|
| QUICKSTART_FIX.md | ? 完成 | 2025-01-XX |
| HOW_TO_FIX.md | ? 完成 | 2025-01-XX |
| UI_TRANSLATION_FIX_GUIDE.md | ? 完成 | 2025-01-XX |
| DEPLOYMENT_SUMMARY_v3.3.19.md | ? 完成 | 2025-01-XX |
| DEPLOYMENT_CHECKLIST_v3.3.19.md | ? 完成 | 2025-01-XX |
| TASK_STATUS.md | ? 完成 | 2025-01-XX |
| QUICKSTART_UI_v3.3.19.md | ? 完成 | 2025-01-XX |
| CHANGELOG.md | ? 完成 | 2025-01-XX |
| quick_fix_translations.ps1 | ? 完成 | 2025-01-XX |

## ?? 推荐阅读顺序

### 开发者（修复和发布）
1. **[QUICKSTART_FIX.md](QUICKSTART_FIX.md)** - 快速开始
2. **[TASK_STATUS.md](TASK_STATUS.md)** - 了解状态
3. **[DEPLOYMENT_CHECKLIST_v3.3.19.md](DEPLOYMENT_CHECKLIST_v3.3.19.md)** - 发布检查

### 维护者（深入了解）
1. **[DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md)** - 完整概览
2. **[HOW_TO_FIX.md](HOW_TO_FIX.md)** - 详细指南
3. **[UI_TRANSLATION_FIX_GUIDE.md](UI_TRANSLATION_FIX_GUIDE.md)** - 技术细节

### 最终用户
1. **[QUICKSTART_UI_v3.3.19.md](QUICKSTART_UI_v3.3.19.md)** - 使用指南
2. **[CHANGELOG.md](CHANGELOG.md)** - 更新内容

## ?? 快速链接

### GitHub
- **Issues**: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/issues
- **Discussions**: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/discussions
- **Releases**: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/releases

### 关键文件
- **About.xml**: `About/About.xml`
- **MainTabWindow_Memory.cs**: `Source/Memory/UI/MainTabWindow_Memory.cs`
- **Dialog_CommonKnowledge.cs**: `Source/Memory/UI/Dialog_CommonKnowledge.cs`
- **English Keys**: `Languages/English/Keyed/MemoryPatch.xml`
- **Chinese Keys**: `Languages/ChineseSimplified/Keyed/MemoryPatch.xml`

## ?? 提示

### 第一次使用？
从 **[QUICKSTART_FIX.md](QUICKSTART_FIX.md)** 开始，它会指导你完成所有必需步骤。

### 遇到问题？
查看 **[HOW_TO_FIX.md](HOW_TO_FIX.md)** 的"常见问题"部分。

### 想深入了解？
阅读 **[DEPLOYMENT_SUMMARY_v3.3.19.md](DEPLOYMENT_SUMMARY_v3.3.19.md)** 获取完整技术细节。

### 准备发布？
使用 **[DEPLOYMENT_CHECKLIST_v3.3.19.md](DEPLOYMENT_CHECKLIST_v3.3.19.md)** 确保不遗漏任何步骤。

## ?? 获取帮助

如果文档无法解决你的问题：

1. **搜索已有Issues**: 可能有人遇到同样问题
2. **创建新Issue**: 详细描述问题和步骤
3. **参与Discussions**: 与社区交流

---

**祝你修复顺利！** ??

**建议的下一步**: 打开 [QUICKSTART_FIX.md](QUICKSTART_FIX.md) 并开始30分钟修复之旅！
