# 注入预览器增强功能 - 完整实现报告

## 📋 概述

本文档记录了对 `Dialog_InjectionPreview` 调试预览器的增强实现,主要目标是在预览器中显示常识的详细评分信息,包括匹配来源(关键词/向量)和未注入常识的查看功能。

**实现日期**: 2025-12-17  
**版本**: v3.3.20+  
**状态**: ✅ 已完成

---

## 🎯 功能目标

### 主要功能
1. ✅ 显示每条常识的详细评分分解
2. ✅ 区分关键词匹配和向量检索的常识
3. ✅ 显示手动优先级对评分的影响
4. ✅ 可选查看未被注入的常识及原因
5. ✅ 友好的失败原因说明

### 用户价值
- 帮助用户理解为什么某些常识被选中或被过滤
- 调试常识匹配问题
- 优化常识库配置
- 理解向量检索和关键词匹配的差异

---

## 🔧 技术实现

### 1. 数据结构增强 (`CommonKnowledgeLibrary.cs`)

#### 1.1 新增枚举类型

```csharp
/// <summary>
/// 匹配类型
/// </summary>
public enum KnowledgeMatchType
{
    Keyword,    // 关键词匹配
    Vector,     // 向量检索
    Mixed       // 混合
}
```

**位置**: `Source/Memory/CommonKnowledgeLibrary.cs` (第24-30行)

#### 1.2 增强 `KnowledgeScoreDetail` 类

```csharp
public class KnowledgeScoreDetail
{
    public CommonKnowledgeEntry Entry;
    public bool IsEnabled;
    public float TotalScore;
    
    // ⭐ 新增：详细分项
    public float BaseScore;       // 基础重要性
    public float ManualBonus;     // 手动加权 (Priority - 3) * 0.1
    public float MatchTypeScore;  // 匹配类型得分
    
    // ⭐ 新增：匹配来源
    public KnowledgeMatchType MatchType;
    
    // 保留原有字段（向后兼容）
    public float JaccardScore;
    public float TagScore;
    public float ImportanceScore;
    public int KeywordMatchCount;
    public List<string> MatchedKeywords = new List<string>();
    public List<string> MatchedTags = new List<string>();
    public string FailReason; // "Selected", "LowScore", "Excluded", "ConfidenceMargin", "ExceedMaxEntries"
}
```

**位置**: `Source/Memory/CommonKnowledgeLibrary.cs` (第1050-1073行)

**新增字段说明**:
- `BaseScore`: 常识的基础重要性分数 (0.0-1.0)
- `ManualBonus`: 手动优先级加成,公式: `(Priority - 3) * 0.1`
  - 1星: -0.2, 2星: -0.1, 3星: 0.0, 4星: +0.1, 5星: +0.2
- `MatchTypeScore`: 匹配类型得分,根据混合权重计算
- `MatchType`: 标识是关键词匹配还是向量检索
- `FailReason`: 失败原因标记

#### 1.3 修改 `InjectKnowledgeWithDetails` 方法

**核心改动**:

```csharp
// 在评分计算循环中
foreach (var entry in allMatchedEntries)
{
    float baseScore = entry.importance;
    float manualBonus = (entry.manualPriority - 3) * 0.1f;
    
    // 判断匹配类型
    bool isKeywordMatched = IsMatched(currentMatchText, entry);
    KnowledgeMatchType matchType = isKeywordMatched ? 
        KnowledgeMatchType.Keyword : KnowledgeMatchType.Vector;
    
    float matchTypeScore = 0f;
    if (settings.enableVectorEnhancement)
    {
        float keywordWeight = 1.0f - settings.hybridWeightBalance;
        float vectorWeight = settings.hybridWeightBalance;
        
        if (isKeywordMatched)
        {
            matchTypeScore = 1.0f * keywordWeight * 2f;
        }
        else
        {
            matchTypeScore = 0.8f * vectorWeight * 2f;
        }
        
        baseScore = (baseScore + manualBonus + matchTypeScore) / 2f;
    }
    else
    {
        baseScore += manualBonus;
    }
    
    // ⭐ 同时添加到 allScores（所有候选）
    allScores.Add(new KnowledgeScoreDetail
    {
        Entry = entry,
        IsEnabled = entry.isEnabled,
        TotalScore = baseScore,
        BaseScore = entry.importance,
        ManualBonus = manualBonus,
        MatchTypeScore = matchTypeScore,
        MatchType = matchType,
        MatchedTags = entry.GetTags(),
        FailReason = "Pending" // 稍后更新
    });
    
    scoredEntries.Add(new KnowledgeScore
    {
        Entry = entry,
        Score = baseScore
    });
}

// 排序后标记失败原因
scoredEntries.Sort((a, b) => b.Score.CompareTo(a.Score));

// 防误触领跑分
int cutoffIndex = scoredEntries.Count;
if (scoredEntries.Count >= 2 && settings.confidenceMargin > 0)
{
    float topScore = scoredEntries[0].Score;
    float secondScore = scoredEntries[1].Score;
    
    if (topScore - secondScore > settings.confidenceMargin)
    {
        cutoffIndex = 1;
        // 标记被 ConfidenceMargin 过滤的
        for (int i = 1; i < scoredEntries.Count; i++)
        {
            var detail = allScores.FirstOrDefault(d => d.Entry == scoredEntries[i].Entry);
            if (detail != null)
            {
                detail.FailReason = "ConfidenceMargin";
            }
        }
    }
}

// 限制数量，标记 Selected 和 ExceedMaxEntries
for (int i = 0; i < scoredEntries.Count; i++)
{
    var detail = allScores.FirstOrDefault(d => d.Entry == scoredEntries[i].Entry);
    if (detail != null)
    {
        if (i < maxEntries && i < cutoffIndex)
        {
            detail.FailReason = "Selected";
            scores.Add(scoredEntries[i]);
        }
        else if (detail.FailReason == "Pending")
        {
            detail.FailReason = "ExceedMaxEntries";
        }
    }
}
```

**位置**: `Source/Memory/CommonKnowledgeLibrary.cs` (第700-800行)

**失败原因标记系统**:
- `"Selected"` - ✅ 已选中注入
- `"ConfidenceMargin"` - 🎯 被领跑分过滤
- `"ExceedMaxEntries"` - 📊 超出数量限制
- `"Excluded"` - 🚫 被排除词过滤
- `"Pending"` - ⏳ 待处理(临时状态)

---

### 2. UI 增强 (`Dialog_InjectionPreview.cs`)

#### 2.1 新增状态变量

```csharp
// ⭐ 新增：注入预览增强
private bool showRejectedKnowledge = false; // 是否显示未注入的常识
private List<KnowledgeScoreDetail> cachedAllKnowledgeScores = null; // 缓存所有评分
```

**位置**: `Source/Memory/UI/Dialog_InjectionPreview.cs` (第20-22行)

#### 2.2 修改 `RefreshPreview` 方法

```csharp
// ⭐ 传递targetPawn参数,并获取所有评分详情
knowledgeInjection = library.InjectKnowledgeWithDetails(
    testContext,
    settings.maxInjectedKnowledge,
    out knowledgeScores,
    out allKnowledgeScores,  // ⭐ 获取所有评分
    out keywordInfo,
    selectedPawn,
    targetPawn
);

// ⭐ 缓存所有评分详情
cachedAllKnowledgeScores = allKnowledgeScores;
```

**位置**: `Source/Memory/UI/Dialog_InjectionPreview.cs` (第350-365行)

#### 2.3 添加切换按钮

```csharp
// 刷新按钮 + 切换按钮
Rect toggleButtonRect = new Rect(inRect.width - 230f, yPos, 110f, 35f);
string toggleLabel = showRejectedKnowledge ? "隐藏未注入" : "显示未注入";
if (Widgets.ButtonText(toggleButtonRect, toggleLabel))
{
    showRejectedKnowledge = !showRejectedKnowledge;
    RefreshPreview(); // 刷新显示
}

Rect refreshButtonRect = new Rect(inRect.width - 110f, yPos, 100f, 35f);
if (Widgets.ButtonText(refreshButtonRect, "刷新预览"))
{
    RefreshPreview();
}
```

**位置**: `Source/Memory/UI/Dialog_InjectionPreview.cs` (第85-98行)

**功能**: 允许用户切换显示/隐藏未注入的常识

#### 2.4 添加辅助方法

```csharp
/// <summary>
/// ⭐ 新增：获取失败原因标签
/// </summary>
private string GetFailReasonLabel(string failReason)
{
    switch (failReason)
    {
        case "Selected":
            return "✅ 已选中";
        case "LowScore":
            return "📉 分数过低";
        case "ConfidenceMargin":
            return "🎯 领跑分过滤";
        case "ExceedMaxEntries":
            return "📊 超出数量限制";
        case "Excluded":
            return "🚫 被排除词过滤";
        case "Pending":
            return "⏳ 待处理";
        default:
            return "❓ 未知";
    }
}
```

**位置**: `Source/Memory/UI/Dialog_InjectionPreview.cs` (第650-672行)

**功能**: 将失败原因代码转换为友好的中文标签

---

## 📊 数据流程图

```
用户输入上下文
    ↓
InjectKnowledgeWithDetails (增强版)
    ├─ 关键词匹配阶段
    │   └─ 标记 MatchType = Keyword
    ├─ 向量检索阶段 (如果启用)
    │   └─ 标记 MatchType = Vector
    ├─ 评分计算
    │   ├─ BaseScore (基础重要性)
    │   ├─ ManualBonus (手动加权)
    │   └─ MatchTypeScore (匹配类型得分)
    ├─ 排序和过滤
    │   ├─ ConfidenceMargin 过滤
    │   └─ MaxEntries 限制
    └─ 标记失败原因
        ├─ Selected
        ├─ ConfidenceMargin
        └─ ExceedMaxEntries
    ↓
返回: knowledgeScores + allKnowledgeScores
    ↓
缓存到: cachedAllKnowledgeScores
    ↓
UI 显示
    ├─ 已注入常识 (默认显示)
    │   ├─ 🔑 关键词匹配
    │   └─ 🧠 向量检索
    └─ 未注入常识 (可选显示)
        ├─ 🎯 领跑分过滤
        └─ 📊 超出数量限制
```

---

## 🎨 UI 展示效果

### 常识显示格式 (已注入)

```
🔑 [关键词] 总分: 0.650
    标签: [规则]
    ├─ 基础重要性: 0.50
    ├─ 手动加权: +0.10 (4星)
    └─ 匹配得分: 0.40
    内容: "回复控制在80字以内"

🧠 [向量] 总分: 0.520
    标签: [世界观]
    ├─ 基础重要性: 0.60
    ├─ 手动加权: 0.00 (3星)
    └─ 匹配得分: 0.32
    内容: "这是一个科幻世界..."
```

### 常识显示格式 (未注入)

```
🔑 [关键词] 总分: 0.450 | 原因: 📊 超出数量限制
    标签: [背景]
    内容: "殖民地建立于5500年..."

🧠 [向量] 总分: 0.380 | 原因: 🎯 领跑分过滤
    标签: [历史]
    内容: "古代文明的遗迹..."
```

---

## 🔍 评分系统详解

### 评分公式

#### 未启用向量增强
```
TotalScore = BaseScore + ManualBonus
```

#### 启用向量增强
```
KeywordWeight = 1.0 - HybridWeightBalance
VectorWeight = HybridWeightBalance

如果是关键词匹配:
    MatchTypeScore = 1.0 * KeywordWeight * 2.0
否则 (向量匹配):
    MatchTypeScore = 0.8 * VectorWeight * 2.0

TotalScore = (BaseScore + ManualBonus + MatchTypeScore) / 2.0
```

### 评分组成

| 组成部分 | 范围 | 说明 |
|---------|------|------|
| BaseScore | 0.0 - 1.0 | 常识的基础重要性 |
| ManualBonus | -0.2 - +0.2 | 手动优先级加成 |
| MatchTypeScore | 0.0 - 2.0 | 匹配类型得分 |
| TotalScore | 变化 | 最终总分 |

### 手动优先级影响

| 优先级 | ManualBonus | 说明 |
|--------|-------------|------|
| 1星 ⭐ | -0.2 | 降低优先级 |
| 2星 ⭐⭐ | -0.1 | 略微降低 |
| 3星 ⭐⭐⭐ | 0.0 | 默认(无影响) |
| 4星 ⭐⭐⭐⭐ | +0.1 | 略微提升 |
| 5星 ⭐⭐⭐⭐⭐ | +0.2 | 显著提升 |

---

## 🧪 测试场景

### 场景 1: 关键词匹配优先

**配置**:
- 混合权重平衡: 0.2 (关键词优先)
- 最大常识数: 5

**预期结果**:
- 关键词匹配的常识得分更高
- 显示 🔑 图标
- MatchTypeScore 较高

### 场景 2: 向量检索优先

**配置**:
- 混合权重平衡: 0.8 (语义优先)
- 最大常识数: 5

**预期结果**:
- 向量匹配的常识得分更高
- 显示 🧠 图标
- MatchTypeScore 较高

### 场景 3: 领跑分过滤

**配置**:
- 领跑分阈值: 0.3
- 第一名: 0.8, 第二名: 0.4

**预期结果**:
- 只选中第一名 (0.8 - 0.4 = 0.4 > 0.3)
- 第二名及以后标记为 "ConfidenceMargin"

### 场景 4: 数量限制

**配置**:
- 最大常识数: 3
- 候选常识: 10条

**预期结果**:
- 前3名标记为 "Selected"
- 第4-10名标记为 "ExceedMaxEntries"

---

## 📝 使用指南

### 查看已注入常识

1. 打开调试预览器
2. 选择殖民者
3. 输入上下文(可选)
4. 点击"刷新预览"
5. 查看"常识库注入详细分析"部分

### 查看未注入常识

1. 点击"显示未注入"按钮
2. 查看未注入常识列表
3. 查看失败原因标签
4. 根据原因调整配置

### 调试常识匹配

1. 检查匹配类型图标 (🔑/🧠)
2. 查看详细评分分解
3. 调整手动优先级
4. 调整混合权重平衡
5. 重新测试

---

## 🔧 配置建议

### 关键词匹配优先场景

适用于:
- 精确匹配需求
- 规则类常识
- 特定关键词触发

**推荐配置**:
- 混合权重平衡: 0.0 - 0.3
- 手动优先级: 4-5星

### 语义检索优先场景

适用于:
- 模糊匹配需求
- 背景知识
- 相关性检索

**推荐配置**:
- 混合权重平衡: 0.7 - 1.0
- 向量相似度阈值: 0.6 - 0.8

### 平衡模式

适用于:
- 一般场景
- 混合需求

**推荐配置**:
- 混合权重平衡: 0.4 - 0.6
- 手动优先级: 3星(默认)

---

## 🐛 已知问题

### 1. 向量相似度未显示

**问题**: 当前实现中,向量匹配的常识使用固定的 0.8 分数,而不是实际的相似度。

**原因**: 架构限制,`MatchKnowledgeByVector` 返回的相似度未传递到评分系统。

**解决方案**: 需要重构 `MatchKnowledgeByVector` 方法,返回 `(entry, similarity)` 元组。

### 2. 性能考虑

**问题**: 大量常识时,`allScores` 列表可能较大。

**影响**: 内存占用增加,但影响有限。

**优化**: 可以考虑只缓存前 N 条未注入常识。

---

## 📚 相关文档

- [INJECTION_PREVIEW_ENHANCEMENT.md](./INJECTION_PREVIEW_ENHANCEMENT.md) - 原始实现指南
- [KNOWLEDGE_MATCHING_UPDATE.md](./KNOWLEDGE_MATCHING_UPDATE.md) - 常识匹配更新
- [INTEGRATION_REPORT.md](./INTEGRATION_REPORT.md) - 集成报告

---

## 🎉 总结

本次增强实现为调试预览器添加了强大的常识评分分析功能,帮助用户:

1. ✅ 理解常识选择机制
2. ✅ 调试匹配问题
3. ✅ 优化常识库配置
4. ✅ 区分匹配来源
5. ✅ 查看未注入原因

所有核心功能已实现并测试通过,代码已集成到主分支。

---

**文档版本**: 1.0  
**最后更新**: 2025-12-17  
**维护者**: Cline AI Assistant
