# 缺失的英文翻译列表

## RimTalkSettings.cs 中的硬编码中文字符串

从 `Source/RimTalkSettings.cs` 文件中提取的所有硬编码中文字符串：

### 主要分类

#### 1. 常识库管理 (Knowledge Library Management)
- 常识库管理 → Knowledge Library Management
- 常识库可用于向AI注入世界观、背景知识等通用信息 → Knowledge library can be used to inject worldview, background knowledge and other common information into AI
- 打开常识库管理 → Open Knowledge Library Manager

#### 2. 动态注入系统 (Dynamic Injection System)
- 动态注入系统 → Dynamic Injection System
- 启用动态记忆注入（推荐） → Enable Dynamic Memory Injection (Recommended)
- 根据时间、重要性和关键词匹配动态选择最相关的记忆和常识 → Dynamically select the most relevant memories and knowledge based on time, importance and keyword matching
- 将使用静态注入（按层级顺序） → Will use static injection (by layer order)
- 最大注入记忆数 → Max Injected Memories
- 最大注入常识数 → Max Injected Knowledge
- 评分阈值（低于此分数不注入）→ Score Threshold (content below this score will not be injected)
- 记忆评分阈值 → Memory Score Threshold
- 常识评分阈值 → Knowledge Score Threshold

#### 3. 权重配置 (Weight Configuration)
- ?? 记忆权重 → ?? Memory Weights
- ?? 常识权重 → ?? Knowledge Weights
- 时间衰减 → Time Decay
- 重要性 → Importance
- 关键词匹配 → Keyword Match
- 标签匹配 → Tag Match
- 关键词匹配: 自动 → Keyword Match: Auto

#### 4. 四层记忆容量 (Four-Layer Memory Capacity)
- 四层记忆容量 → Four-Layer Memory Capacity
- ABM（超短期）: 6 条 (固定，不可调整) → ABM (Ultra-short): 6 entries (fixed, not adjustable)
- SCM（短期）→ SCM (Short-term)
- ELS（中期）→ ELS (Medium-term)
- CLPA（长期）: 无限制 → CLPA (Long-term): Unlimited
- 条 → entries

#### 5. 记忆衰减速率 (Memory Decay Rate)
- 记忆衰减速率 → Memory Decay Rates
- SCM（每小时）→ SCM (per hour)
- ELS（每小时）→ ELS (per hour)
- CLPA（每小时）→ CLPA (per hour)

#### 6. AI 自动总结 (AI Auto-Summarization)
- AI 自动总结 → AI Auto-Summarization
- 启用ELS总结（SCM → ELS）→ Enable ELS Summarization (SCM → ELS)
- 触发时间：每天 {0}:00（游戏时间）→ Trigger Time: Daily at {0}:00 (game time)
- 最大总结长度 → Max Summary Length
- 字 → characters
- 启用 CLPA 自动归档 → Enable CLPA Auto-Archive
- 归档间隔：每 {0} 天 → Archive Interval: Every {0} days

#### 7. AI API 配置 (AI API Configuration)
- AI API 配置 → AI API Configuration
- 优先使用 RimTalk 的 AI 配置 → Prefer RimTalk's AI Configuration
- 将尝试读取 RimTalk Mod 的 API 配置 → Will attempt to read RimTalk Mod's API configuration
- 如果 RimTalk 未安装或未配置，将使用下方的独立配置 → If RimTalk is not installed or configured, will use the independent configuration below
- 将使用下方的独立配置，不依赖 RimTalk → Will use the independent configuration below, independent of RimTalk
- === 独立 AI 配置 === → === Independent AI Configuration ===
- 提供商 → Provider
- API Key → API Key (keep as is)
- API URL → API URL (keep as is)
- 模型名称 → Model Name

#### 8. 记忆类型 (Memory Types)
- 记忆类型 → Memory Types
- 行动记忆（工作、战斗）→ Action Memory (work, combat)
- 对话记忆（RimTalk 对话）→ Conversation Memory (RimTalk dialogues)
- 自动生成新人/老人状态常识 → Auto-generate Newcomer/Veteran Status Knowledge
- 根据殖民者加入时间自动生成状态常识 → Automatically generate status knowledge based on colonist join time
- 如：Alice是3天前加入的新成员，对殖民地历史不了解 → Example: Alice is a new member who joined 3 days ago and doesn't know the colony's history
- 禁用后，新成员可能会错误地谈论不属于他们经历的事件 → When disabled, new members may incorrectly talk about events they didn't experience

#### 9. 调试工具 (Debug Tools)
- 调试工具 → Debug Tools
- 打开注入内容预览器 → Open Injection Content Previewer
- 实时查看将要注入给AI的记忆和常识（需进入游戏）→ Real-time preview of memories and knowledge to be injected to AI (requires entering game)

#### 10. 错误消息 (Error Messages)
- 需要进入游戏后才能管理常识库 → Must enter game to manage knowledge library
- 无法找到记忆管理器 → Cannot find memory manager

---

## 需要添加到 Languages/English/Keyed/MemoryPatch.xml

所有上述翻译键都需要添加到英文翻译文件中。

## 修复步骤

1. 在 `RimTalkSettings.cs` 中将所有硬编码中文字符串替换为 `.Translate()` 调用
2. 在 `Languages/English/Keyed/MemoryPatch.xml` 中添加对应的翻译键
3. 确保翻译键命名规范统一（例如：`RimTalk_Settings_XXX`）

## 预计影响

- 修复约40%的未翻译内容
- 所有Mod设置界面将完全支持英文
- 提升国际用户体验
