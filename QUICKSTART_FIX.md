# ? v3.3.19 快速启动指南

## ?? 目标
在30分钟内完成v3.3.19的最后修复并发布。

## ?? 3步快速完成

### 第1步：自动修复翻译 (5分钟)

```powershell
# 在项目根目录运行
.\quick_fix_translations.ps1

# 脚本会：
# ? 自动备份文件
# ? 替换所有硬编码英文
# ? 显示修复统计
# ? 提示下一步操作
```

**预期输出：**
```
? 完成! 共修复 15 处硬编码文本
?? 备份文件: Source\Memory\UI\MainTabWindow_Memory.cs.backup
? 未发现明显的硬编码英文!
```

**如果脚本失败：**
- 方案A：手动应用修复（参考 `UI_TRANSLATION_FIX_GUIDE.md`）
- 方案B：还原备份重新运行

---

### 第2步：添加滚动视图 (15分钟)

#### 2.1 打开文件
```bash
code Source/Memory/UI/MainTabWindow_Memory.cs
```

#### 2.2 添加成员变量（约第45行）
在类成员变量区域添加：
```csharp
private Vector2 controlPanelScrollPosition = Vector2.zero;
```

#### 2.3 修改 DrawControlPanel 方法（约第200行）

**原始代码：**
```csharp
private void DrawControlPanel(Rect rect)
{
    Widgets.DrawMenuSection(rect);
    Rect innerRect = rect.ContractedBy(SPACING);
    float y = innerRect.y;
    
    // Title
    Text.Font = GameFont.Medium;
    Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 30f), ...);
    // ...
}
```

**修改为：**
```csharp
private void DrawControlPanel(Rect rect)
{
    Widgets.DrawMenuSection(rect);
    Rect innerRect = rect.ContractedBy(SPACING);
    
    // ? 添加滚动支持
    float contentHeight = 600f;
    Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, contentHeight);
    
    Widgets.BeginScrollView(innerRect, ref controlPanelScrollPosition, viewRect);
    
    float y = 0f; // ? 改为从0开始
    
    // Title
    Text.Font = GameFont.Medium;
    Widgets.Label(new Rect(0f, y, viewRect.width, 30f), ...); // ? 使用 viewRect
    Text.Font = GameFont.Small;
    y += 35f;
    
    // Layer Filters
    Rect filterRect = new Rect(0f, 0f, viewRect.width, viewRect.height); // ? 新增
    y = DrawLayerFilters(filterRect, y); // ? 使用 filterRect
    y += 10f;
    
    // Type Filters
    y = DrawTypeFilters(filterRect, y); // ? 使用 filterRect
    y += 10f;
    
    // Statistics
    y = DrawStatistics(filterRect, y); // ? 使用 filterRect
    y += 10f;
    
    // Separator
    Widgets.DrawLineHorizontal(0f, y, viewRect.width); // ? 使用 viewRect
    y += 15f;
    
    // Batch Actions
    y = DrawBatchActions(filterRect, y); // ? 使用 filterRect
    y += 10f;
    
    // Global Actions
    DrawGlobalActions(filterRect, y); // ? 使用 filterRect
    
    Widgets.EndScrollView(); // ? 结束滚动视图
}
```

#### 快速检查
- [ ] `controlPanelScrollPosition` 已添加
- [ ] `BeginScrollView` 已添加
- [ ] `EndScrollView` 已添加
- [ ] 所有子方法使用 `filterRect`
- [ ] 所有坐标使用 `viewRect.width`

---

### 第3步：编译和测试 (10分钟)

#### 3.1 编译
```bash
dotnet clean
dotnet build
```

**预期输出：**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

#### 3.2 快速测试
1. 启动 RimWorld
2. 加载任意存档
3. 打开 Memory 标签页
4. 测试关键功能：

**必测项目（5分钟）：**
- [ ] 殖民者选择器工作
- [ ] 记忆卡片显示
- [ ] 左侧面板可滚动到底部
- [ ] "Summarize All"和"Archive All"按钮可见
- [ ] 无翻译键显示（如`RimTalk_xxx`）
- [ ] 切换语言（英文?中文）

**发现问题？**
- 编译错误 → 检查语法，还原备份
- 翻译键显示 → 确认脚本运行成功
- 滚动不工作 → 检查 `BeginScrollView/EndScrollView` 配对

---

## ? 完成标志

当以下所有项都成功时，即可发布：

- [ ] 脚本运行成功
- [ ] 滚动视图已添加
- [ ] 编译无错误
- [ ] 游戏启动正常
- [ ] Memory 标签页正常
- [ ] 左侧面板完整可见
- [ ] 英文界面无问题
- [ ] 中文界面无问题

---

## ?? 发布步骤（可选）

如果所有测试通过，可以立即发布：

### 1. Git 提交
```bash
git add .
git commit -m "v3.3.19: Mind Stream UI + Complete Translations

Major Changes:
- Added Mind Stream timeline with drag multi-select
- Added Library style knowledge base UI
- Added 100+ EN/CN translation keys
- Added show all humanlikes option
- Added scrollable control panel

Bug Fixes:
- Fixed hardcoded English strings
- Fixed control panel visibility
- Fixed memory card display

Breaking Changes:
- None

Migration Notes:
- Compatible with existing saves
- May need to wait one game day for memory refresh
"

git push origin main
```

### 2. 创建 GitHub Release
```bash
# 在 GitHub 网页操作
1. 进入 Releases 页面
2. 点击 "Draft a new release"
3. Tag version: v3.3.19
4. Release title: v3.3.19 - Mind Stream & Library UI
5. 复制 CHANGELOG.md 到描述
6. 上传编译后的 DLL
7. 点击 "Publish release"
```

### 3. 测试发布版本
```bash
1. 下载刚发布的 Release
2. 安装到游戏
3. 快速测试关键功能
4. 确认无问题
```

---

## ?? 遇到问题？

### 脚本失败
```powershell
# 问题1：权限不足
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# 问题2：文件不存在
# 确认在项目根目录运行
Get-Location  # 应该显示项目路径
```

### 编译失败
```bash
# 问题1：语法错误
# → 还原备份，手动修复
Copy-Item "Source\Memory\UI\MainTabWindow_Memory.cs.backup" "Source\Memory\UI\MainTabWindow_Memory.cs" -Force

# 问题2：缺少引用
# → 确认 using 语句完整

# 问题3：类型不匹配
# → 检查 .ToString() 调用
```

### 游戏崩溃
```bash
# 问题1：Mod冲突
# → 禁用其他Mod测试

# 问题2：存档损坏
# → 加载备份存档

# 问题3：代码错误
# → 查看游戏日志
# → 还原到上一个可用版本
```

---

## ?? 参考文档

如需详细说明，请查看：

| 文档 | 用途 |
|------|------|
| `HOW_TO_FIX.md` | 详细修复指南 |
| `UI_TRANSLATION_FIX_GUIDE.md` | 手动翻译修复 |
| `DEPLOYMENT_SUMMARY_v3.3.19.md` | 完整部署总结 |
| `TASK_STATUS.md` | 任务状态追踪 |
| `QUICKSTART_UI_v3.3.19.md` | UI使用说明 |

---

## ?? 时间估算

| 步骤 | 预计时间 | 实际时间 |
|------|----------|----------|
| 自动修复翻译 | 5分钟 | ___ |
| 添加滚动视图 | 15分钟 | ___ |
| 编译测试 | 10分钟 | ___ |
| **总计** | **30分钟** | ___ |

---

## ?? 恭喜！

如果你看到这里，说明你已经完成了v3.3.19的所有必需任务！

**下一步：**
1. 享受你的新UI ??
2. 收集用户反馈
3. 计划下一个版本

**感谢使用 RimTalk - Expand Memory！** ??

---

**最后更新**: 2025-01-XX  
**版本**: v3.3.19  
**状态**: ? 待完成最后30分钟
