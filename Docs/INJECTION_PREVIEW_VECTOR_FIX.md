# 注入预览器向量显示修复

## 问题
注入预览器 (`Dialog_InjectionPreview`) 无法显示向量匹配的常识条目。这是因为为了避免游戏主线程卡顿，向量匹配逻辑已从 `CommonKnowledgeLibrary.InjectKnowledgeWithDetails` 移至后台线程补丁 (`Patch_GenerateAndProcessTalkAsync`) 中。然而，预览器运行在主线程上，且原本的调用路径不再包含向量搜索，导致调试时无法看到向量匹配结果。

## 解决方案
在 `Dialog_InjectionPreview` 的 `RefreshPreview` 方法中手动实现同步向量匹配逻辑。

### 更改详情
1.  **文件**: `Source/Memory/UI/Dialog_InjectionPreview.cs`
2.  **方法**: `RefreshPreview`
3.  **逻辑**:
    *   添加了对 `settings.enableVectorEnhancement`（启用向量增强）和非空上下文的检查。
    *   使用 `ContextCleaner.CleanForVectorMatching` 清理输入上下文，去除噪音。
    *   调用 `VectorService.Instance.FindBestLoreIds`（同步版本）获取向量匹配结果。
    *   将向量结果追加到 `knowledgeInjection` 文本中，并添加 "## World Knowledge (Vector Enhanced)" 标题。
    *   更新 `allKnowledgeScores` 列表以包含向量匹配项，确保它们出现在详细评分列表中。
    *   将向量匹配项标记为 `FailReason = "Selected (Vector)"` 和 `MatchType = KnowledgeMatchType.Vector`（如果已通过关键词匹配则为 `Mixed`）。

## 验证步骤
1.  在游戏中打开“注入预览器”窗口。
2.  在“上下文输入”框中输入文本。
3.  点击“刷新预览”。
4.  验证：
    *   “完整的 JSON 请求结构”部分是否包含 "## World Knowledge (Vector Enhanced)" 块（如果有向量匹配）。
    *   “常识库注入详细分析”部分是否列出了带有 "🧠 [向量]" 或 "🧠 [混合]" 图标的条目。
    *   “注入统计”部分是否正确统计了向量注入的条目。

## 技术细节
*   **命名空间**: 添加了 `using RimTalk.Memory.VectorDB;` 以访问 `VectorService`。
*   **同步执行**: 预览器使用的是同步的 `FindBestLoreIds` 方法。虽然这可能会在生成预览时导致 UI 线程轻微停顿，但对于调试工具来说是可以接受的，并且避免了在 Unity/RimWorld UI 中处理复杂的异步回调。
