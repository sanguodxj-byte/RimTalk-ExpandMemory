# 评分系统更新报告：向量相似度加权与领跑分移除

## 1. 问题背景

用户反馈现有的评分系统存在严重问题：
1.  **关键词匹配权重过高**：关键词匹配固定得分为 100 分，而向量匹配固定为 90 分。
2.  **领跑分机制误杀**：由于分差较大（10 分），触发了“防误触领跑分”（Confidence Margin）机制，导致即使向量匹配结果非常相关（如相似度 0.9），也会被系统判定为“不如关键词匹配可靠”而被过滤。
3.  **向量相似度未利用**：之前的评分逻辑完全忽略了向量匹配的实际相似度，所有向量匹配都获得相同的 90 分。

## 2. 实施的解决方案

### 2.1 引入相似度加权与混合权重平衡
重构了评分公式，使向量匹配的得分直接取决于其相似度，并受 `hybridWeightBalance` 设置影响。

*   **混合权重系数** (`balance` = `hybridWeightBalance`, 0.0-1.0):
    *   **关键词优先 (balance < 0.5)**:
        *   `KeywordWeight` = `1.0 + (0.5 - balance)` (1.0 ~ 1.5)
        *   `VectorWeight` = `0.5 + balance` (0.5 ~ 1.0)
    *   **向量优先 (balance >= 0.5)**:
        *   `KeywordWeight` = `1.0 - (balance - 0.5)` (1.0 ~ 0.5)
        *   `VectorWeight` = `1.0 + (balance - 0.5)` (1.0 ~ 1.5)

*   **新评分公式**：
    *   **关键词匹配**：`100 * KeywordWeight + Importance`
    *   **向量匹配**：`100 * Similarity * VectorWeight + Importance`

**效果 (当 balance = 0.5 时)**：
*   `KeywordWeight` = 1.0, `VectorWeight` = 1.0
*   如果向量相似度为 **1.0**，得分 = **100**（与关键词匹配平起平坐）。
*   如果向量相似度为 **0.9**，得分 = **90**。

这确保了高质量的向量匹配结果能够获得应有的高分，同时赋予用户通过滑块微调两者权重的能力。

### 2.2 移除领跑分机制
彻底移除了 `ConfidenceMargin`（防误触领跑分）的过滤逻辑。

**原因**：
*   该机制初衷是防止低置信度结果干扰高置信度结果。
*   但在实际应用中，它导致了不同匹配类型之间的恶性竞争（关键词匹配总是“领跑”向量匹配）。
*   移除后，所有通过阈值的匹配结果都将根据其实际得分进行排序和保留，不再进行额外的差值过滤。

## 3. 代码变更摘要

### `Source/Memory/CommonKnowledgeLibrary.cs`

1.  **`MatchKnowledgeByVector`**：
    *   返回类型从 `List<CommonKnowledgeEntry>` 改为 `List<(CommonKnowledgeEntry, float)>`。
    *   现在向上层传递实际的相似度值。

2.  **`InjectKnowledgeWithDetails`**：
    *   接收并存储向量相似度。
    *   引入 `settings.hybridWeightBalance` 计算动态权重。
    *   更新评分逻辑：`matchTypeScore = 100f * similarity * vectorWeight`。
    *   注释掉 `ConfidenceMargin` 相关代码块。

## 4. 预期结果

*   **向量匹配复活**：相关的向量匹配结果（如“黄金树”）将不再被关键词匹配（如“Bean”）过滤掉。
*   **用户可控**：用户可以通过设置中的“混合检索权重”滑块来偏好关键词匹配或向量匹配。
*   **排序更合理**：结果将严格按照（相似度 * 权重 + 重要性）排序，真正反映内容的相关性。
