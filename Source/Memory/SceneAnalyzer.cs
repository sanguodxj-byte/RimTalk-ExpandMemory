using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 场景类型枚举
    /// </summary>
    public enum SceneType
    {
        Combat,         // 战斗/紧急（袭击、受伤、死亡）
        Social,         // 社交/情感（聊天、关系、心情）
        Work,           // 工作/日常（建造、种植、搬运）
        Medical,        // 医疗/健康（治疗、手术、疾病）
        Research,       // 研究/学习（科技、技能提升）
        Event,          // 特殊事件（婚礼、生日、仪式）
        Neutral         // 中性/未识别
    }
    
    /// <summary>
    /// 场景分析器 - 识别当前对话/查询的场景类型
    /// ? v3.3.11: 动态场景感知权重系统核心组件
    /// </summary>
    public static class SceneAnalyzer
    {
        // 场景关键词映射（中英文支持）
        private static readonly Dictionary<SceneType, List<string>> SceneKeywords = new Dictionary<SceneType, List<string>>
        {
            {
                SceneType.Combat, new List<string>
                {
                    // 战斗核心
                    "袭击", "raid", "攻击", "attack", "战斗", "combat", "fight",
                    "敌人", "enemy", "入侵", "invasion", "防御", "defense",
                    // 伤亡
                    "受伤", "injured", "伤势", "wound", "死亡", "death", "died", "killed",
                    "流血", "bleeding", "倒下", "downed", "昏迷", "unconscious",
                    // 武器/战术
                    "武器", "weapon", "枪", "gun", "射击", "shoot", "爆炸", "explosion",
                    "掩体", "cover", "撤退", "retreat", "增援", "reinforcement"
                }
            },
            {
                SceneType.Social, new List<string>
                {
                    // 对话/社交
                    "聊天", "chat", "talk", "对话", "conversation", "说", "said",
                    "告诉", "told", "询问", "asked", "回答", "replied",
                    // 关系
                    "喜欢", "like", "爱", "love", "讨厌", "hate", "朋友", "friend",
                    "恋人", "lover", "配偶", "spouse", "关系", "relationship",
                    "友谊", "friendship", "争吵", "argument", "和解", "reconcile",
                    // 情绪
                    "开心", "happy", "快乐", "joyful", "悲伤", "sad", "难过", "upset",
                    "愤怒", "angry", "生气", "mad", "焦虑", "anxious", "心情", "mood",
                    "感觉", "feel", "情绪", "emotion"
                }
            },
            {
                SceneType.Work, new List<string>
                {
                    // 建造/制造
                    "建造", "construct", "建筑", "building", "制作", "craft", "制造", "manufacture",
                    "修理", "repair", "拆除", "deconstruct",
                    // 农业
                    "种植", "plant", "收获", "harvest", "农作物", "crop", "田地", "field",
                    // 采矿/搬运
                    "采矿", "mining", "挖掘", "dig", "搬运", "haul", "运输", "transport",
                    "仓库", "storage", "清洁", "clean",
                    // 烹饪
                    "烹饪", "cook", "cooking", "做饭", "meal", "食物", "food"
                }
            },
            {
                SceneType.Medical, new List<string>
                {
                    // 治疗
                    "治疗", "treat", "医疗", "medical", "手术", "surgery", "操作", "operation",
                    "包扎", "bandage", "照顾", "tend", "护理", "care",
                    // 疾病/状态
                    "疾病", "disease", "illness", "感染", "infection", "发烧", "fever",
                    "疼痛", "pain", "残疾", "disability", "健康", "health",
                    "恢复", "recover", "痊愈", "heal", "康复", "rehabilitation"
                }
            },
            {
                SceneType.Research, new List<string>
                {
                    // 研究
                    "研究", "research", "科技", "technology", "发明", "invention",
                    "突破", "breakthrough", "发现", "discovery", "实验", "experiment",
                    // 学习/技能
                    "学习", "learn", "训练", "train", "练习", "practice",
                    "技能", "skill", "提升", "improve", "掌握", "master",
                    "知识", "knowledge", "教导", "teach"
                }
            },
            {
                SceneType.Event, new List<string>
                {
                    // 特殊事件
                    "婚礼", "wedding", "结婚", "marry", "订婚", "engaged",
                    "生日", "birthday", "庆祝", "celebrate", "派对", "party",
                    "葬礼", "funeral", "埋葬", "burial", "纪念", "memorial",
                    "仪式", "ceremony", "节日", "festival", "活动", "event"
                }
            }
        };
        
        // 场景权重（用于多场景混合时的优先级）
        private static readonly Dictionary<SceneType, float> ScenePriority = new Dictionary<SceneType, float>
        {
            { SceneType.Combat, 1.0f },      // 战斗最高优先级
            { SceneType.Medical, 0.9f },     // 医疗紧急度高
            { SceneType.Event, 0.85f },      // 特殊事件重要
            { SceneType.Social, 0.7f },      // 社交次之
            { SceneType.Research, 0.6f },    // 研究长期
            { SceneType.Work, 0.5f },        // 工作日常
            { SceneType.Neutral, 0.3f }      // 中性最低
        };
        
        /// <summary>
        /// 分析文本，识别场景类型（支持多场景混合）
        /// </summary>
        /// <param name="context">上下文文本（prompt/query）</param>
        /// <returns>主要场景类型和混合场景信息</returns>
        public static SceneAnalysisResult AnalyzeScene(string context)
        {
            if (string.IsNullOrEmpty(context))
            {
                return new SceneAnalysisResult
                {
                    PrimaryScene = SceneType.Neutral,
                    SceneScores = new Dictionary<SceneType, float> { { SceneType.Neutral, 1.0f } },
                    Confidence = 0f
                };
            }
            
            // 转换为小写，方便匹配
            string lowerContext = context.ToLower();
            
            // 计算每个场景的匹配分数
            var sceneScores = new Dictionary<SceneType, float>();
            
            foreach (var sceneKvp in SceneKeywords)
            {
                SceneType scene = sceneKvp.Key;
                List<string> keywords = sceneKvp.Value;
                
                // 统计关键词匹配数
                int matchCount = keywords.Count(kw => lowerContext.Contains(kw.ToLower()));
                
                // 计算归一化分数（0-1）
                float score = matchCount > 0 ? (float)matchCount / keywords.Count : 0f;
                
                // 应用场景优先级权重
                score *= ScenePriority[scene];
                
                if (score > 0)
                {
                    sceneScores[scene] = score;
                }
            }
            
            // 如果没有匹配到任何场景，返回中性
            if (sceneScores.Count == 0)
            {
                return new SceneAnalysisResult
                {
                    PrimaryScene = SceneType.Neutral,
                    SceneScores = new Dictionary<SceneType, float> { { SceneType.Neutral, 1.0f } },
                    Confidence = 0f
                };
            }
            
            // 归一化分数
            float totalScore = sceneScores.Values.Sum();
            var normalizedScores = sceneScores.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value / totalScore
            );
            
            // 确定主场景（分数最高）
            var primaryScene = normalizedScores.OrderByDescending(kvp => kvp.Value).First();
            
            // 计算置信度（主场景分数占比）
            float confidence = primaryScene.Value;
            
            return new SceneAnalysisResult
            {
                PrimaryScene = primaryScene.Key,
                SceneScores = normalizedScores,
                Confidence = confidence
            };
        }
        
        /// <summary>
        /// 根据场景类型获取动态权重配置
        /// </summary>
        public static DynamicWeights GetDynamicWeights(SceneType scene, float confidence = 1.0f)
        {
            var weights = new DynamicWeights();
            
            switch (scene)
            {
                case SceneType.Combat:
                    // 战斗场景：强调时效性和重要性
                    weights.TimeDecay = 0.8f;          // 极高衰减，只看最近
                    weights.Importance = 0.5f;         // 只关注大事
                    weights.KeywordMatch = 0.4f;       // 精准匹配
                    weights.RelationshipBonus = 0.1f;  // 关系不重要
                    weights.RecencyWindow = 15000;     // 只看最近6小时
                    break;
                
                case SceneType.Social:
                    // 社交场景：允许唤醒旧记忆，强调关系
                    weights.TimeDecay = 0.05f;         // 极低衰减，可以回忆往事
                    weights.Importance = 0.2f;         // 小事也能聊
                    weights.KeywordMatch = 0.25f;      // 宽松匹配
                    weights.RelationshipBonus = 0.6f;  // 大幅提升共同记忆
                    weights.RecencyWindow = 1800000;   // 可回溯30天
                    break;
                
                case SceneType.Work:
                    // 工作场景：平衡时效和相关性
                    weights.TimeDecay = 0.3f;
                    weights.Importance = 0.3f;
                    weights.KeywordMatch = 0.35f;
                    weights.RelationshipBonus = 0.15f;
                    weights.RecencyWindow = 180000;    // 7天内
                    break;
                
                case SceneType.Medical:
                    // 医疗场景：强调历史健康记录
                    weights.TimeDecay = 0.15f;         // 低衰减，医疗史重要
                    weights.Importance = 0.45f;
                    weights.KeywordMatch = 0.35f;
                    weights.RelationshipBonus = 0.2f;
                    weights.RecencyWindow = 420000;    // 14天内
                    break;
                
                case SceneType.Research:
                    // 研究场景：长期记忆，知识积累
                    weights.TimeDecay = 0.02f;         // 极低衰减
                    weights.Importance = 0.4f;
                    weights.KeywordMatch = 0.4f;
                    weights.RelationshipBonus = 0.1f;
                    weights.RecencyWindow = 3600000;   // 60天内
                    break;
                
                case SceneType.Event:
                    // 事件场景：强调特殊时刻
                    weights.TimeDecay = 0.1f;          // 低衰减，重要时刻永久记忆
                    weights.Importance = 0.5f;
                    weights.KeywordMatch = 0.3f;
                    weights.RelationshipBonus = 0.4f;
                    weights.RecencyWindow = 900000;    // 15天内
                    break;
                
                case SceneType.Neutral:
                default:
                    // 中性场景：默认平衡配置
                    weights.TimeDecay = 0.25f;
                    weights.Importance = 0.3f;
                    weights.KeywordMatch = 0.3f;
                    weights.RelationshipBonus = 0.25f;
                    weights.RecencyWindow = 240000;    // 10天内
                    break;
            }
            
            // 根据置信度调整权重（低置信度时回退到中性）
            if (confidence < 0.6f)
            {
                float neutralBlend = 1.0f - confidence;
                var neutralWeights = GetDynamicWeights(SceneType.Neutral, 1.0f);
                
                weights.TimeDecay = Lerp(weights.TimeDecay, neutralWeights.TimeDecay, neutralBlend);
                weights.Importance = Lerp(weights.Importance, neutralWeights.Importance, neutralBlend);
                weights.KeywordMatch = Lerp(weights.KeywordMatch, neutralWeights.KeywordMatch, neutralBlend);
                weights.RelationshipBonus = Lerp(weights.RelationshipBonus, neutralWeights.RelationshipBonus, neutralBlend);
            }
            
            return weights;
        }
        
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
        
        /// <summary>
        /// 获取场景的中文显示名称
        /// </summary>
        public static string GetSceneDisplayName(SceneType scene)
        {
            switch (scene)
            {
                case SceneType.Combat: return "战斗/紧急";
                case SceneType.Social: return "社交/情感";
                case SceneType.Work: return "工作/日常";
                case SceneType.Medical: return "医疗/健康";
                case SceneType.Research: return "研究/学习";
                case SceneType.Event: return "特殊事件";
                case SceneType.Neutral: return "中性/通用";
                default: return "未知";
            }
        }
    }
    
    /// <summary>
    /// 场景分析结果
    /// </summary>
    public class SceneAnalysisResult
    {
        public SceneType PrimaryScene { get; set; }                  // 主要场景
        public Dictionary<SceneType, float> SceneScores { get; set; } // 所有场景得分（归一化）
        public float Confidence { get; set; }                        // 识别置信度
        
        public override string ToString()
        {
            return $"{SceneAnalyzer.GetSceneDisplayName(PrimaryScene)} (置信度: {Confidence:P0})";
        }
    }
    
    /// <summary>
    /// 动态权重配置
    /// ? v3.3.11: 根据场景动态调整的记忆检索权重
    /// </summary>
    public class DynamicWeights
    {
        public float TimeDecay { get; set; }          // 时间衰减因子（越高越重视最近记忆）
        public float Importance { get; set; }         // 重要性权重
        public float KeywordMatch { get; set; }       // 关键词匹配权重
        public float RelationshipBonus { get; set; }  // 关系加成权重
        public int RecencyWindow { get; set; }        // 时间窗口（ticks，超过此时间的记忆大幅衰减）
        
        public override string ToString()
        {
            return $"TimeDecay:{TimeDecay:F2} Importance:{Importance:F2} Keyword:{KeywordMatch:F2} " +
                   $"Relationship:{RelationshipBonus:F2} Window:{RecencyWindow / 60000}days";
        }
    }
}
