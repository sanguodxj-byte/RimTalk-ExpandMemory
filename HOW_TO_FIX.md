# ?? v3.3.19 快速修复指南

## 问题摘要

v3.3.19 的 UI 重构已完成，但存在以下需要手动修复的问题：

1. ? **翻译键未生效** - 部分代码仍使用硬编码英文
2. ? **左侧面板显示不全** - 底部按钮被遮挡，需要滚动视图
3. ?? **记忆固定/编辑可能无效** - 需要测试确认

## ?? 修复步骤

### 方法一：自动修复（推荐）

#### 1. 运行自动修复脚本

```powershell
# 在项目根目录打开 PowerShell
.\quick_fix_translations.ps1
```

脚本会自动：
- ? 创建备份文件
- ? 替换所有硬编码英文为翻译键
- ? 显示修复统计
- ? 检查遗漏项
- ? 提示编译测试

#### 2. 检查修复结果

```powershell
# 查看修改内容
git diff Source/Memory/UI/MainTabWindow_Memory.cs

# 如果有问题，恢复备份
Copy-Item "Source\Memory\UI\MainTabWindow_Memory.cs.backup" "Source\Memory\UI\MainTabWindow_Memory.cs" -Force
```

#### 3. 手动添加滚动视图

在 `MainTabWindow_Memory.cs` 中：

**添加成员变量**（约第45行）：
```csharp
private Vector2 controlPanelScrollPosition = Vector2.zero;
```

**修改 DrawControlPanel 方法**（约第200行）：
```csharp
private void DrawControlPanel(Rect rect)
{
    Widgets.DrawMenuSection(rect);
    Rect innerRect = rect.ContractedBy(SPACING);
    
    // ? 添加滚动支持
    float contentHeight = 600f; // 可以调整
    Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, contentHeight);
    
    Widgets.BeginScrollView(innerRect, ref controlPanelScrollPosition, viewRect);
    
    float y = 0f; // 改为从0开始
    
    // Title
    Text.Font = GameFont.Medium;
    Widgets.Label(new Rect(0f, y, viewRect.width, 30f), "RimTalk_MindStream_MemoryFilters".Translate());
    // ... 其余代码 ...
    
    Widgets.EndScrollView(); // ? 最后添加
}
```

**修改所有子方法调用**（约第220-280行）：
```csharp
// 从：
y = DrawLayerFilters(innerRect, y);
y = DrawTypeFilters(innerRect, y);
y = DrawStatistics(innerRect, y);
y = DrawBatchActions(innerRect, y);
DrawGlobalActions(innerRect, y);

// 改为：
Rect methodRect = new Rect(0f, 0f, viewRect.width, viewRect.height);
y = DrawLayerFilters(methodRect, y);
y = DrawTypeFilters(methodRect, y);
y = DrawStatistics(methodRect, y);
y = DrawBatchActions(methodRect, y);
DrawGlobalActions(methodRect, y);
```

#### 4. 编译测试

```bash
# 清理并重新编译
dotnet clean
dotnet build

# 检查错误
# 如果有错误，请参考 UI_TRANSLATION_FIX_GUIDE.md
```

#### 5. 游戏测试

1. 启动 RimWorld
2. 加载存档
3. 打开 Memory 标签页
4. 测试以下功能：
   - [ ] 选择殖民者
   - [ ] 勾选"显示所有类人生物"
   - [ ] 查看记忆卡片
   - [ ] 点击固定按钮
   - [ ] 点击编辑按钮
   - [ ] 多选记忆（Ctrl/Shift/拖拽）
   - [ ] 批量总结/归档/删除
   - [ ] 滚动左侧面板到底部
   - [ ] 切换语言（英文/中文）

### 方法二：手动修复

如果自动脚本失败，请按照以下文档手动修复：

1. **翻译修复**: 参考 `UI_TRANSLATION_FIX_GUIDE.md`
2. **滚动视图**: 参考上面的代码片段
3. **详细说明**: 参考 `DEPLOYMENT_SUMMARY_v3.3.19.md`

## ?? 验证检查清单

### 翻译验证

```bash
# 搜索硬编码英文（应该为0）
grep -n 'Messages\.Message("(?!RimTalk_)' Source/Memory/UI/MainTabWindow_Memory.cs
grep -n 'Widgets\.Label(.*?, "(?!RimTalk_)' Source/Memory/UI/MainTabWindow_Memory.cs
grep -n 'TooltipHandler\.TipRegion(.*?, "(?!RimTalk_)' Source/Memory/UI/MainTabWindow_Memory.cs
```

### 游戏内验证

- [ ] **英文界面** - 无翻译键显示（如 `RimTalk_xxx`）
- [ ] **中文界面** - 所有文本为中文
- [ ] **按钮提示** - 鼠标悬停有正确提示
- [ ] **左侧面板** - 可以滚动到底部
- [ ] **记忆操作** - 固定/编辑功能正常
- [ ] **批量操作** - 总结/归档/删除正常
- [ ] **总记忆数** - 顶栏显示正确
- [ ] **类人生物** - 勾选框工作正常

## ?? 已知限制

### 1. 控制面板滚动
- **问题**: 需要手动添加滚动视图代码
- **原因**: 无法自动化修改复杂的嵌套布局
- **解决**: 按照上面的代码片段手动添加

### 2. 性能优化
- **问题**: 大量记忆时可能卡顿
- **建议**: 考虑使用虚拟化滚动
- **临时方案**: 使用过滤器减少显示数量

### 3. 旧存档兼容性
- **问题**: 可能需要重新生成记忆数据
- **建议**: 加载存档后等待一个游戏日
- **注意**: 备份存档文件

## ?? 常见问题

### Q: 脚本执行失败
**A**: 
```powershell
# 允许脚本执行
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# 重新运行
.\quick_fix_translations.ps1
```

### Q: 编译错误
**A**: 
1. 检查语法错误
2. 还原备份文件
3. 参考 `UI_TRANSLATION_FIX_GUIDE.md` 手动修复

### Q: 翻译键仍然显示
**A**:
1. 确认翻译文件存在：
   - `Languages/English/Keyed/MemoryPatch.xml`
   - `Languages/ChineseSimplified/Keyed/MemoryPatch.xml`
2. 确认翻译键拼写正确
3. 重启游戏

### Q: 左侧面板仍然显示不全
**A**:
1. 确认添加了 `controlPanelScrollPosition` 成员变量
2. 确认 `Widgets.BeginScrollView` 和 `EndScrollView` 配对
3. 确认 `contentHeight` 足够大（建议 600f）

### Q: 固定按钮无效
**A**:
1. 检查 `FourLayerMemoryComp.PinMemory` 方法
2. 确认记忆数据正确保存
3. 重启游戏测试

## ?? 获取帮助

- **GitHub Issues**: https://github.com/sanguodxj-byte/RimTalk-ExpandMemory/issues
- **文档**: 查看项目根目录的 `.md` 文件
- **源代码**: 查看 `Source/Memory/UI/` 目录

## ?? 文件清单

修复过程中创建的文件：

| 文件 | 用途 |
|------|------|
| `quick_fix_translations.ps1` | 自动修复翻译的脚本 |
| `UI_TRANSLATION_FIX_GUIDE.md` | 手动修复指南 |
| `DEPLOYMENT_SUMMARY_v3.3.19.md` | 完整部署总结 |
| `DEPLOYMENT_CHECKLIST_v3.3.19.md` | 部署检查清单 |
| `QUICKSTART_UI_v3.3.19.md` | UI 快速入门 |
| `HOW_TO_FIX.md` | 本文件 |

## ? 完成标志

当以下所有项都完成时，v3.3.19 即可发布：

- [x] 翻译键已添加到 XML
- [x] UI 重构完成
- [x] 文档已创建
- [ ] **翻译键已应用到代码** ?? 需要完成
- [ ] **滚动视图已添加** ?? 需要完成
- [ ] 编译无错误
- [ ] 游戏测试通过
- [ ] 两种语言测试通过

---

**祝你修复顺利！** ??
