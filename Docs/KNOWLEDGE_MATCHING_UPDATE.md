# 常识库匹配与管理功能更新 (v3.3.20)

本次更新旨在增强常识库的匹配精度和管理灵活性，引入了多项新功能和改进。

## 1. 核心数据结构变更 (`CommonKnowledgeEntry`)

在 `Source/Memory/CommonKnowledgeLibrary.cs` 中，`CommonKnowledgeEntry` 类新增了以下属性：

*   **`matchMode` (KeywordMatchMode)**: 关键词匹配模式。
    *   `Any`: 单词匹配，只要出现其中一个词就算。
    *   `All`: 组合匹配，必须同时出现所有标签词。
*   **`manualPriority` (int)**: 手动优先级，范围 1-5 星，默认 3 星。
    *   用于人为干预常识条目的最终得分。
*   **`excludeKeywords` (List<string>)**: 局部排除词。
    *   如果文本中包含这些词，则该条目绝对不会触发。

## 2. 全局设置变更 (`RimTalkSettings`)

在 `Source/RimTalkSettings.cs` 中，新增了以下高级匹配设置：

*   **`confidenceMargin` (float)**: 防误触领跑分 (0.0 - 0.2)。
    *   如果第一名候选条目的分数比第二名高出设定值，则视为“绝对胜出”，自动丢弃第二名及以后的所有条目。
*   **`hybridWeightBalance` (float)**: 混合检索权重 (0.0 - 1.0)。
    *   用于平衡关键词匹配和向量检索的权重。
    *   0.0 = 关键词优先 (Keywords)。
    *   1.0 = 语义优先 (Vector)。
*   **`globalExcludeKeywords` (string)**: 全局排除词。
    *   逗号分隔的关键词列表。如果文本中出现这些词，绝对不要触发任何常识。

## 3. 匹配逻辑改进 (`CommonKnowledgeLibrary`)

### 3.1 匹配流程更新 (`MatchKnowledgeByTags`)

*   **排除词检查**: 优先检查全局排除词和局部排除词，命中则跳过。
*   **匹配模式支持**:
    *   `Any`: 检查是否包含任意标签。
    *   `All`: 检查是否包含所有标签。
    *   `Exact`: 检查是否包含完整的标签字符串（去除首尾空格）。

### 3.2 评分与排序更新 (`InjectKnowledgeWithDetails`)

*   **基础分**: 使用条目的 `importance`。
*   **手动加权**: 根据 `manualPriority` 计算加成。
    *   公式: `(manualPriority - 3) * 0.1`。
    *   1星: -0.2, 3星: 0.0, 5星: +0.2。
*   **混合权重**:
    *   如果开启向量增强，根据 `hybridWeightBalance` 混合关键词匹配得分和向量相似度得分。
    *   关键词匹配得分权重: `1.0 - hybridWeightBalance`。
    *   向量匹配得分权重: `hybridWeightBalance`。
*   **排序**: 按最终计算出的 `Score` 降序排列。
*   **防误触过滤**:
    *   排序后，检查第一名和第二名的分数差。
    *   如果 `Score[0] - Score[1] > confidenceMargin`，则只保留第一名。

## 4. UI 更新 (`Dialog_CommonKnowledge`)

在 `Source/Memory/UI/Dialog_CommonKnowledge.cs` 中：

*   **详情面板**: 显示匹配模式、优先级和排除词。
*   **编辑面板**:
    *   新增匹配模式下拉菜单。
    *   新增优先级滑块 (1-5 星)。
    *   新增排除词输入框。
*   **设置窗口**:
    *   新增高级匹配设置区域，包含领跑分滑块、混合权重滑块和全局排除词输入框。

## 5. 序列化兼容性

*   所有新字段均已添加到 `ExposeData` 方法中。
*   使用 `Scribe_Values` 和 `Scribe_Collections` 进行序列化，确保存档兼容性。
*   旧存档加载时，新字段将使用默认值初始化。

---

**开发者注**:
这些更改主要集中在 `CommonKnowledgeLibrary.cs` 和 `RimTalkSettings.cs`，UI 部分仅涉及 `Dialog_CommonKnowledge.cs`。未修改任何底层数据库或第三方库依赖。
