# Git 提交命令清单

## 快速提交步骤

### 1. 查看变更
```bash
git status
```

### 2. 添加所有拆分文件
```bash
git add Source/Memory/UI/MainTabWindow_Memory.cs
git add Source/Memory/UI/MainTabWindow_Memory_Actions.cs
git add Source/Memory/UI/MainTabWindow_Memory_Controls.cs
git add Source/Memory/UI/MainTabWindow_Memory_ImportExport.cs
git add Source/Memory/UI/MainTabWindow_Memory_Timeline.cs
git add Source/Memory/UI/MainTabWindow_Memory_TopBar.cs
git add Source/Memory/UI/MainTabWindow_Memory_Utilities.cs
git add Docs/拆分*.md
```

### 3. 提交（推荐）
```bash
git commit -m "refactor: 拆分 MainTabWindow_Memory 为 8 个 partial class 文件

主要改进:
- 主文件从 1590 行减少到 130 行 (减少 92%)
- 按功能模块拆分为 7 个部分类文件
- 提高代码可维护性和可读性

文件列表:
- TopBar (145行): Pawn选择器和统计信息
- Controls (376行): 过滤器和批量操作按钮  
- Timeline (440行): 时间线和记忆卡片绘制
- Actions (176行): 批量操作逻辑
- ImportExport (230行): 导入导出功能
- Utilities (210行): 辅助方法和对话框
- Helpers (280行): 记忆聚合算法

Breaking Changes: 无 (完全向后兼容)
"
```

### 4. 推送到远程
```bash
# 推送到分支 1
git push origin 1

# 或推送到 main
git push origin HEAD:main
```

---

## 可选：创建专门的拆分分支

### 创建新分支
```bash
git checkout -b refactor/split-maintabwindow-memory
```

### 提交并推送
```bash
git add Source/Memory/UI/MainTabWindow_Memory*.cs Docs/拆分*.md
git commit -m "refactor: 拆分 MainTabWindow_Memory 为 8 个 partial class 文件"
git push origin refactor/split-maintabwindow-memory
```

### 创建 Pull Request
然后在 GitHub 上创建 Pull Request，合并到 main 分支

---

## 备份文件处理

### 选项 1: 保留备份（推荐用于第一次提交）
```bash
# 备份文件也提交，以便回滚
git add Source/Memory/UI/MainTabWindow_Memory_OLD_BACKUP.cs
```

### 选项 2: 删除备份（后续清理）
```bash
# 删除备份文件
rm Source/Memory/UI/MainTabWindow_Memory_OLD_BACKUP.cs

# 或移到其他位置
mv Source/Memory/UI/MainTabWindow_Memory_OLD_BACKUP.cs ../backup/
```

---

## 验证清单

在推送之前，确认：

- [ ] 所有新文件已添加到 Git
- [ ] 主文件已正确替换
- [ ] 文档文件已包含
- [ ] 提交信息清晰描述了变更
- [ ] （可选）代码已编译通过
- [ ] （可选）功能已测试通过

---

## 回滚方案（如果需要）

### 回滚到拆分前状态
```bash
# 恢复旧文件
git restore Source/Memory/UI/MainTabWindow_Memory.cs

# 或使用备份
cp Source/Memory/UI/MainTabWindow_Memory_OLD_BACKUP.cs Source/Memory/UI/MainTabWindow_Memory.cs

# 删除新文件
git rm Source/Memory/UI/MainTabWindow_Memory_*.cs
```

---

**快速参考**: 复制上面的命令直接执行即可！
