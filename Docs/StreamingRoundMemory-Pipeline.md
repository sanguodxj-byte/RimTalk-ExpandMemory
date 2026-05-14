# 流式 RoundMemory 捕获管线

## 概述

在现有批量 RoundMemory 管线（`TalkHistory_AddMessageHistory_Patch` → `AddResponsesToHistory` Postfix）之外，**新增**一条独立的流式捕获管线。核心思想：pawn 每**说出口**一句话，立刻追加到当前会话的 RoundMemory 中，而非等到整轮对话结束后批量构建。

## 架构

```
                     PromptContext_FromTalkRequest_Patch (Prefix)
                         │ request.Participants = pawns  ← {{pawns}} 集合桥接
                         ▼
GenerateTalk → pawns列表 → LLM流式 → responses入队
                                         │
                           DisplayTalk(0.5s)
                               │
                      CreateInteraction(pawn, talk)  ← 唯一 Hook
                               │
               CreateInteraction_StreamingRoundMemory_Patch (Postfix)
                               │
                     ApiHistory.GetApiLog(talk.Id).TalkRequest
                               │
                     RoundMemoryManager.StreamingBuildRoundMemory(...)
```

## Hook 点

**主 Hook**: `TalkService.CreateInteraction(Pawn pawn, TalkResponse talk)` — 在对话气泡**实际显示**时触发。所有被忽略/跳过/未通过 Display gate 的 response 均不触发。

**辅助 Hook**: `PromptContext.FromTalkRequest(TalkRequest, List<Pawn>)` — 在 prompt 构建阶段将 `{{pawns}}` 的原始 pawn 集合写入 `talkRequest.Participants`，使流式管线能获取到 LLM 实际感知的参与者列表。

### 为什么是 CreateInteraction

```
DisplayTalk (每0.5s)
  ├─ GATE 1: 父消息被忽略? 或 pawn 无法展示? → IgnoreTalkResponse (不入 CreateInteraction)
  ├─ GATE 2: pawn 处于危险? → IgnoreAllTalkResponses (不入 CreateInteraction)
  ├─ GATE 3: 回复间隔未到? → continue (稍后重试)
  └─ 全部通过 → CreateInteraction ★ 只有这里才"说出"
```

## 会话隔离

`ConditionalWeakTable<object, RoundMemory>` 以 `TalkRequest` 引用作为弱引用 key。key 声明为泛型 `<T> where T : class` 是为了让 `RoundMemoryManager` 不直接依赖 RimTalk 类型。

同一轮 `GenerateTalk` 的所有 response 通过 `ApiHistory.GetApiLog(talk.Id)?.TalkRequest` 解析到同一个引用，天然隔离不同轮次/并发会话。

**弱引用的优势**：当 `TalkRequest` 被 GC 回收（所有 response 消费完毕后无其他强引用），对应的 `RoundMemory` 条目自动移除，无需手动清理。

## 状态机

```
CreateInteraction → patch Postfix:
  │
  ├─ talk.Id → ApiHistory.GetApiLog → .TalkRequest → talkRequest
  │
  ├─ talkRequest.Recipient.IsPlayer() → isUserInitiate
  │
  ├─ RoundMemoryManager.StreamingBuildRoundMemory<T>(talkRequest, content, participants, isUserInitiate)
  │   │
  │   ├─ _dictToRoundMemory.TryGetValue(talkRequest, out roundMemory)?
  │   │   │
  │   │   ├─ NO → 新 session:
  │   │   │   ├─ new RoundMemory(participants, userContent)
  │   │   │   │     Content = "[对话参与者: Arrow, Bob]" (或含用户台词)
  │   │   │   ├─ _roundMemories.Add(rm)          // 加入全局缓冲
  │   │   │   ├─ 各pawn.ActiveMemories.Add(rm)    // 分发到个人
  │   │   │   └─ _dictToRoundMemory.AddOrUpdate(tr, rm)
  │   │   │
  │   │   └─ YES → 已有 session:
  │   │         └─ (跳过创建)
  │   │
  │   └─ roundMemory.AppendLine(content)  // 追加 "$name: $text"
```

## 玩家发言注入

玩家自己输入的发言不经过 `CreateInteraction`（由 `CustomDialogueService.ExecuteDialogue` 直接发 overlay），因此通过 `CapturePlayerDialogue` Postfix 捕获到 `_playerDialogue`，在流式管线创建新 RoundMemory 时作为构造器的 initial content 注入。

`isUserInitiate` 通过 `talkRequest.Recipient.IsPlayer()` 判断，精确区分"玩家本人发起"和"用户指挥 colonist 代发"两种场景，避免后者的消息重复。

## 对象释放

| 对象 | 释放策略 |
|------|----------|
| `TalkRequest`（dict key） | `ConditionalWeakTable` 弱引用：GC 自动移除，无需手动清理 |
| `RoundMemory` | 不释放，由 `_roundMemories` 环形缓冲 + pawn ABM 共同持有 |
| `ApiLog` | 仅瞬态查询，不持有 |

兜底：`FinalizeInit()` 读档时可重建 `_dictToRoundMemory`。

## 线程安全

`CreateInteraction` 在 `DisplayTalk()` → 主线程 game tick 链路上触发。`_dictToRoundMemory` 的读写均在主线程，无锁。`ConditionalWeakTable` 内部保证线程安全。

## 与 RimTalk 的解耦

`RoundMemoryManager` 零引用 RimTalk 类型。所有 RimTalk API 调用集中在两个 patch 中：

| 层 | 文件 | RimTalk 依赖 |
|----|------|-------------|
| 核心 | `RoundMemoryManager.cs` | **零** |
| 桥接 | `CreateInteraction_StreamingRoundMemory_Patch.cs` | `ApiHistory`, `TalkResponse`, `RimTalkMemoryPatchMod` |
| 桥接 | `PromptContext_FromTalkRequest_Patch.cs` | `PromptContext`, `TalkRequest` |

## 文件清单

| 文件 | 职责 |
|------|------|
| `Source/Memory/RoundMemory/RoundMemory.cs` | 新增 `AppendLine()`、构造器支持 null content；移除 `MaxContentLength` 截断；`GetParticipants()` 更名 `GetParticipantsRoster()` |
| `Source/Memory/RoundMemory/RoundMemoryManager.cs` | 新增 `_dictToRoundMemory`（`ConditionalWeakTable`）、`StreamingBuildRoundMemory<T>()`、`AddRoundMemory()`、`GetPlayerDialogue()`；移除 `_playerPawn`、`MaxContentLength`；`BuildRoundMemory` 简化签名 |
| `Source/Patches/CreateInteraction_StreamingRoundMemory_Patch.cs` | Harmony Postfix：清洗文本 → `talkRequest.Recipient.IsPlayer()` 判断用户发起 → 委托 `StreamingBuildRoundMemory` |
| `Source/Patches/PromptContext_FromTalkRequest_Patch.cs` | Harmony Prefix：填充 `talkRequest.Participants` |
| `Source/Patches/TalkHistory_AddMessageHistory_Patch.cs` | 旧批处理管线（已注释） |

## 关键外部 API 引用

| API | 位置 | 用途 |
|-----|------|------|
| `ApiHistory.GetApiLog(Guid)` → `ApiLog` | `RimTalk.Data` | TalkResponse.Id → TalkRequest |
| `ApiLog.TalkRequest` | `RimTalk.Data` | Session key |
| `TalkRequest.Participants` | `RimTalk.Data` | 初始参与者集合（由 Prefix 填充） |
| `TalkRequest.Recipient.IsPlayer()` | RimWorld | 判断是否玩家本人发起 |
| `TalkService.CreateInteraction` | `RimTalk.Service` | 主 Hook 目标 |
| `PromptContext.FromTalkRequest` | `RimTalk.Prompt` | 辅助 Hook 目标 |
