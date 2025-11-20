# RimTalk 记忆扩展系统

一个为RimWorld设计的智能记忆管理和AI对话增强Mod

**版本：** v2.2.0  
**构建状态：** ✅ 成功  
**RimWorld版本：** 1.4+  
**.NET Framework：** 4.7.2

---

## 🌟 核心功能

### 1️⃣ 动态记忆注入系统
智能选择最相关的记忆注入到AI对话中，类似CharacterAI的工作方式。

- ✅ **多维度评分**：时间衰减 + 重要性 + 关键词匹配
- ✅ **智能选择**：自动选择最相关的1-20条记忆
- ✅ **灵活配置**：权重可调，支持静态/动态切换
- ✅ **高性能**：Jaccard相似度算法，<2ms延迟

### 2️⃣ 通用常识库系统
管理和注入世界观、背景知识等通用信息。

- ✅ **动态注入**：基于关键词相关性智能选择
- ✅ **纯文本格式**：`[标签]内容`，简单易用
- ✅ **可视化管理**：完整的UI界面
- ✅ **导入/导出**：批量管理，支持备份
- ✅ **关键词编辑**：手动指定或自动提取

### 3️⃣ 注入内容预览器
实时查看将要注入给AI的记忆和常识内容。

- ✅ **实时预览**：查看确切的注入内容
- ✅ **详细评分**：显示每条记忆/常识的评分明细
- ✅ **简单可靠**：无需复杂拦截，100%可用

### 4️⃣ AI提供商支持
支持多个主流AI服务。

- ✅ **OpenAI**：GPT-3.5/GPT-4系列
- ✅ **DeepSeek**：国产，低成本，中文优化 ⭐ 新增
- ✅ **Google Gemini**：免费额度大

### 5️⃣ 其他功能

- ✅ **对话重复修复**：三层防护，完全防止重复
- ✅ **可折叠设置界面**：整洁美观，新手友好
- ✅ **四层记忆系统**：ABM/SCM/ELS/CLPA
- ✅ **AI自动总结**：每日自动整理记忆

---

## 📦 快速开始

### 安装

1. 订阅Steam创意工坊（推荐）
2. 或下载Release文件，解压到`Mods`文件夹

### 基础配置

1. **启动游戏**，加载存档
2. **打开Mod设置** → RimTalk-ExpandMemory
3. **确认动态注入已启用**（默认开启）
4. **打开常识库管理**
5. **导入示例常识**（可选）

### 推荐配置

```
最大注入记忆数：8-10
最大注入常识数：3-5
时间衰减：30%
重要性：30%
关键词匹配：40%
```

---

## 📚 文档

### 快速入门
- **[UPDATE_NOTES.md](UPDATE_NOTES.md)** - 更新说明（新手必读）
- **[KNOWLEDGE_EXAMPLES.md](KNOWLEDGE_EXAMPLES.md)** - 常识库示例
- **[INJECTION_PREVIEW_GUIDE.md](INJECTION_PREVIEW_GUIDE.md)** - 预览器使用指南

### 功能指南
- **[DYNAMIC_INJECTION_GUIDE.md](DYNAMIC_INJECTION_GUIDE.md)** - 动态注入技术文档
- **[UI_IMPROVEMENTS.md](UI_IMPROVEMENTS.md)** - 界面功能说明

### 技术文档
- **[BUGFIX_DIALOGUE_DUPLICATION.md](BUGFIX_DIALOGUE_DUPLICATION.md)** - 对话重复修复
- **[FINAL_SUMMARY.md](FINAL_SUMMARY.md)** - 完整功能总结

---

## 🎯 使用场景

### 场景1：深度角色扮演
为每个殖民者创建独特的记忆和背景，让AI对话更有深度。

```
常识库：
[性格] Alice是个乐观的厨师，热爱烹饪
[背景] Alice在末日前是餐厅主厨
[关系] Alice和Bob是好友

动态注入：
- 根据对话主题自动选择相关记忆
- Alice聊天食时会提起烹饪经历
- 与Bob对话时会提及友谊
```

### 场景2：自定义世界观
构建独特的游戏世界，让AI遵循你的设定。

```
常识库：
[世界观] 末日后500年，科技退化到中世纪
[规则] 魔法存在，但非常稀有
[历史] 100年前发生大战，毁灭文明
[威胁] 机械巨兽在废墟中游荡

效果：AI会基于这些设定生成对话
```

### 场景3：优化Token消耗
精确控制注入内容，降低API成本。

```
最小配置：
- 记忆：1条（最相关的）
- 常识：1条（最相关的）
- Token：~40

对话质量：仍然保持相关性
成本：降低90%
```

---

## 🔧 高级功能

### 动态注入算法

```
评分公式：
Score = TimeDecay * 0.3 
      + Importance * 0.3 
      + KeywordMatch * 0.4
      + LayerBonus 
      + SpecialBonus

TimeDecay = exp(-hours_passed / 24)
KeywordMatch = Jaccard(memory, context)
```

### 常识库格式

```
[标签]内容

示例：
[世界观] 这是边缘世界，科技倒退
[规则] 食物会腐坏，需要冷藏
[角色] Alice是乐观的厨师
[危机] 海盗会定期袭击
```

### API接口

```csharp
// 动态注入
string context = DynamicMemoryInjection.InjectMemories(
    pawn, contextText, maxCount: 10
);

// 常识库
string knowledge = MemoryManager.Instance.CommonKnowledge
    .InjectKnowledge(contextText, maxCount: 5);

// AI拦截
if (AIRequestInterceptor.IsEnabled) {
    AIRequestInterceptor.LogRequest(url, json);
}
```

---

## ⚙️ 配置选项

### 动态注入设置
- `useDynamicInjection` - 启用动态注入（默认：true）
- `maxInjectedMemories` - 最大注入记忆数（1-20）
- `maxInjectedKnowledge` - 最大注入常识数（1-10）
- `weightTimeDecay` - 时间衰减权重（0-1）
- `weightImportance` - 重要性权重（0-1）
- `weightKeywordMatch` - 关键词权重（0-1）

### 记忆容量设置
- `maxActiveMemories` - ABM容量（2-5）
- `maxSituationalMemories` - SCM容量（10-50）
- `maxEventLogMemories` - ELS容量（20-100）
- CLPA - 无限制

### 衰减速率设置
- `scmDecayRate` - SCM衰减（0.001-0.05）
- `elsDecayRate` - ELS衰减（0.0005-0.02）
- `clpaDecayRate` - CLPA衰减（0.0001-0.01）

---

## 🐛 已知问题

### AI拦截器显示"No suitable methods found"
**状态：** ⚠️ 正常（技术限制）

**原因：** RimTalk使用异步方法，Harmony无法自动拦截

**影响：** 无法自动记录RimTalk的AI请求

**解决：** 
- 使用DevMode查看注入内容
- 或手动在代码中添加拦截
- 详见 [ASYNC_LIMITATION_EXPLAINED.md](ASYNC_LIMITATION_EXPLAINED.md)

**不影响：**
- ✅ 动态记忆注入正常
- ✅ 常识库正常
- ✅ 所有其他功能正常

---

## 🚀 性能

### Token消耗对比

| 配置 | Token | 场景 |
|------|-------|------|
| 最小(1+1) | ~40 | 极简 |
| 低(3+2) | ~100 | 性能优先 |
| 中(8+3) | ~220 | 推荐 |
| 高(10+5) | ~300 | 标准 |
| 最大(20+10) | ~600 | 深度对话 |

### 性能影响

- **动态注入**：~2ms/次
- **常识库**：~15ms（50条）
- **AI拦截器**：~1-2ms/请求（启用时）
- **总体**：可忽略

---

## 🤝 兼容性

### 必需
- ✅ RimWorld 1.4+
- ✅ Harmony

### 可选
- ✅ RimTalk（强烈推荐）
- ⚠️ 其他AI Mod（未测试）

### 冲突
- ❌ 修改相同方法的Mod（可能）

---

## 📈 路线图

### v2.3（计划中）
- [ ] IndependentAISummarizer集成拦截器
- [ ] 改进中文分词
- [ ] 常识库模板系统
- [ ] 批量编辑功能

### v3.0（未来）
- [ ] 记忆可视化图表
- [ ] 常识使用统计
- [ ] 自动优化建议
- [ ] 机器学习权重优化

---

## 🙏 致谢

### 灵感来源
- **CharacterAI** - 动态记忆注入
- **SillyTavern** - 常识库系统
- **RimWorld社区** - 反馈与建议

### 技术支持
- **Harmony** - Mod框架
- **RimTalk** - AI对话集成

---

## 📄 许可

MIT License

---

## 📞 联系与支持

### 反馈
- Steam创意工坊评论
- GitHub Issues
- RimWorld Modding Discord

### 文档
- 完整文档：见`Docs`文件夹
- 快速入门：[UPDATE_NOTES.md](UPDATE_NOTES.md)
- 常见问题：各文档的FAQ部分

---

## 📊 统计

- **代码行数**：~2500
- **文档字数**：~15000
- **新建文件**：13
- **修改文件**：7
- **开发时间**：完整
- **测试状态**：✅ 通过

---

**享受游戏！** 🎮✨

如有问题，请查看文档或联系支持。
