# 向量匹配上下文清理器 (Context Cleaner)

## 概述

为了提高向量检索的准确性，我们引入了 `ContextCleaner`，用于在进行向量匹配之前，从 RimTalk 复杂的上下文格式中提取核心语义内容。

## 问题背景

RimTalk 发送给 AI 的提示词包含大量结构化信息和环境噪音，例如：
- 时间、天气、地形等环境信息
- Pawn 的状态信息（年龄、ID、当前活动等）
- RimTalk 的格式标记（`starts conversation`, `short monologue` 等）

这些信息虽然对 AI 生成回复很有用，但对于**向量检索**来说是噪音，会稀释核心语义，导致检索结果不准确。特别是当玩家直接与小人对话时，我们希望向量检索主要关注玩家说的话。

## 解决方案

我们在 `Source/Memory/ContextCleaner.cs` 中实现了一个硬编码解析器，针对 RimTalk 的三种主要格式进行处理：

1. **玩家直接对话**
   - 格式：`秩序超凡智能() said to 'Renata: 你知道黄金色的巨树叫什么吗'.Generate...`
   - 处理：提取引号内的内容，如 `你知道黄金色的巨树叫什么吗`。

2. **小人自主对话 / 独白**
   - 格式：
     ```
     PawnName starts conversation, taking turns
     [随机信息，如 new good feeling: ...]
     PawnName(Age:...)
     ```
   - 处理：识别 `starts conversation` 或 `short monologue` 作为起始标记，识别 Pawn 状态行（`Name(Age:...)`）作为结束标记。提取两者之间的所有非噪音行，确保保留所有动态生成的随机信息。

3. **带事件的独白**
   - 格式：`... [Ongoing events] ... [Event list end] ...`
   - 处理：完整保留 `[Ongoing events]` 和 `[Event list end]` 之间的事件描述。

## 过滤规则

`ContextCleaner` 会自动过滤掉以下类型的行：
- 以 `Time:`, `Today:`, `Season:`, `Weather:`, `Location:`, `Terrain:`, `Wealth:`, `Nearby:` 开头的环境信息行
- 包含 Pawn 详细状态的行（如 `Pratt(Age:34;女性;ID:Colonist;人类) 闲逛中。`）
- RimTalk 的指令性文本（如 `starts conversation`, `short monologue`, `Generate dialogue starting after`）

## 回退机制

如果经过清理后，文本为空（意味着输入全是噪音），系统会自动回退使用原始文本进行向量匹配，以防止完全匹配失败。

## 代码位置

- 清理器实现：`Source/Memory/ContextCleaner.cs`
- 集成位置：`Source/Memory/CommonKnowledgeLibrary.cs` (在 `InjectKnowledgeWithDetails` 方法中)
