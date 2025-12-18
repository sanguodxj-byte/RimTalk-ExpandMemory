# 已移除功能报告 (v3.3.20)

## 概述
本次更新移除了部分未被异步向量匹配逻辑使用的高级匹配设置，以简化配置并避免混淆。

## 移除的设置

### 1. 防误触领跑分 (Confidence Margin)
*   **位置**: `Source/RimTalkSettings.cs`
*   **描述**: 原计划用于比较第一名和第二名分数的差值，如果差值超过阈值则丢弃后续结果。
*   **原因**: 异步向量匹配逻辑 (`Patch_GenerateAndProcessTalkAsync.cs`) 未使用此参数。

### 2. 全局排除词 (Global Exclude Keywords)
*   **位置**: `Source/RimTalkSettings.cs`
*   **描述**: 原计划用于定义全局的排除关键词，如果出现这些词则不触发常识。
*   **原因**: 异步向量匹配逻辑未使用此参数，且功能与现有逻辑重叠或不再需要。

### 3. 常识条目排除词 (Entry Exclude Keywords)
*   **位置**: `Source/Memory/UI/Dialog_CommonKnowledge.cs`
*   **描述**: 单个常识条目的排除词设置。
*   **原因**: 简化常识库编辑界面，移除未使用的复杂匹配逻辑。

## 修改的文件

1.  **Source/RimTalkSettings.cs**
    *   删除了 `confidenceMargin` 字段、序列化代码及 UI 滑块。
    *   删除了 `globalExcludeKeywords` 字段、缓存逻辑、序列化代码及 UI 输入框。

2.  **Source/Memory/UI/Dialog_CommonKnowledge.cs**
    *   删除了 `editExcludeKeywords` 字段。
    *   删除了编辑面板中的排除词输入框。
    *   删除了详情面板中的排除词显示。
    *   更新了 `SaveEntry` 方法，不再处理排除词。

## 影响
*   设置界面更加简洁。
*   常识库编辑界面更加简洁。
*   向量匹配逻辑不受影响（因为原本就没用到这些参数）。
