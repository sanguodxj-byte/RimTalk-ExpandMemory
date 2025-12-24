using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 记忆层级
    /// </summary>
    public enum MemoryLayer
    {
        Active,         // 超短期记忆 (Active Buffer Memory)
        Situational,    // 短期记忆 (Situational Context Memory)
        EventLog,       // 中期记忆 (Event Log Summary)
        Archive         // 长期记忆 (Colony Lore & Persona Archive)
    }

    /// <summary>
    /// 记忆类型
    /// </summary>
    public enum MemoryType
    {
        Conversation,   // 对话（RimTalk生成的完整对话内容）
        [System.Obsolete("互动记忆已废弃，保留此枚举值仅为兼容旧存档")]
        Interaction,    // 互动（已废弃 - 无具体内容，已被Conversation替代）
        Action,         // 行动（工作、战斗等）
        Observation,    // 观察（未实现）
        Event,          // 事件
        Emotion,        // 情绪
        Relationship,   // 关系
        Internal        // ⭐ v3.3.2: 内部上下文（数据库查询结果，不显示给用户）
    }

    /// <summary>
    /// 常用标签（中文）
    /// </summary>
    public static class MemoryTags
    {
        // 情绪标签
        public const string 开心 = "开心";
        public const string 悲伤 = "悲伤";
        public const string 愤怒 = "愤怒";
        public const string 焦虑 = "焦虑";
        public const string 平静 = "平静";
        
        // 事件标签
        public const string 战斗 = "战斗";
        public const string 袭击 = "袭击";
        public const string 受伤 = "受伤";
        public const string 死亡 = "死亡";
        public const string 完成任务 = "完成任务";
        
        // 社交标签
        public const string 闲聊 = "闲聊";
        public const string 深谈 = "深谈";
        public const string 争吵 = "争吵";
        public const string 表白 = "表白";
        public const string 友好 = "友好";
        public const string 敌对 = "敌对";
        
        // 工作标签
        public const string 烹饪 = "烹饪";
        public const string 建造 = "建造";
        public const string 种植 = "种植";
        public const string 采矿 = "采矿";
        public const string 研究 = "研究";
        public const string 医疗 = "医疗";
        
        // 特殊标签
        public const string 重要 = "重要";
        public const string 紧急 = "紧急";
        public const string 深度归档 = "深度归档";
        public const string 用户编辑 = "用户编辑";
    }

    /// <summary>
    /// 新的记忆条目 - 支持标签化和编辑
    /// </summary>
    public class MemoryEntry : IExposable
    {
        // 基础信息
        public string id;                   // 唯一ID
        public string content;              // 内容
        public MemoryType type;             // 类型
        public MemoryLayer layer;           // 层级
        public int timestamp;               // 时间戳
        
        // 重要性和活跃度
        public float importance;            // 重要性 (0-1)
        public float activity;              // 活跃度 (随时间衰减)
        
        // 关联信息
        public string relatedPawnId;        // 相关小人ID
        public string relatedPawnName;      // 相关小人名字
        public string location;             // 地点
        public List<string> tags;           // 标签（中文）
        public List<string> keywords;       // 关键词
        
        // 元数据
        public bool isUserEdited;           // 是否被用户编辑过
        public bool isPinned;               // 是否固定（不会被删除）
        public string notes;                // 用户备注
        public string aiCacheKey;           // AI总结的缓存键

        public MemoryEntry()
        {
            id = "mem-" + System.Guid.NewGuid().ToString("N").Substring(0, 12);
            timestamp = Find.TickManager.TicksGame;
            tags = new List<string>();
            keywords = new List<string>();
            activity = 1f;
        }

        public MemoryEntry(string content, MemoryType type, MemoryLayer layer, float importance = 1f, string relatedPawn = null)
            : this()
        {
            this.content = content;
            this.type = type;
            this.layer = layer;
            this.importance = importance;
            this.relatedPawnName = relatedPawn;
            
            // 自动添加类型标签
            AddTypeTag();
        }

        /// <summary>
        /// 根据类型自动添加标签
        /// </summary>
        private void AddTypeTag()
        {
            switch (type)
            {
                case MemoryType.Conversation:
                    AddTag("对话");
                    break;
                case MemoryType.Action:
                    AddTag("行动");
                    break;
                case MemoryType.Observation:
                    AddTag("观察");
                    break;
                case MemoryType.Event:
                    AddTag("事件");
                    break;
                case MemoryType.Emotion:
                    AddTag("情绪");
                    break;
                case MemoryType.Relationship:
                    AddTag("关系");
                    break;
                case MemoryType.Internal:
                    AddTag("内部上下文"); // ⭐ v3.3.2: 自动标记
                    break;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref content, "content");
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref layer, "layer");
            Scribe_Values.Look(ref timestamp, "timestamp");
            Scribe_Values.Look(ref importance, "importance");
            Scribe_Values.Look(ref activity, "activity");
            Scribe_Values.Look(ref relatedPawnId, "relatedPawnId");
            Scribe_Values.Look(ref relatedPawnName, "relatedPawnName");
            Scribe_Values.Look(ref location, "location");
            Scribe_Collections.Look(ref tags, "tags", LookMode.Value);
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);
            Scribe_Values.Look(ref isUserEdited, "isUserEdited");
            Scribe_Values.Look(ref isPinned, "isPinned", false);
            Scribe_Values.Look(ref notes, "notes");
            Scribe_Values.Look(ref aiCacheKey, "aiCacheKey");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tags == null) tags = new List<string>();
                if (keywords == null) keywords = new List<string>();
            }
        }

        /// <summary>
        /// 获取记忆年龄（游戏tick）
        /// </summary>
        public int Age => Find.TickManager.TicksGame - timestamp;

        /// <summary>
        /// 获取记忆年龄描述（完全口语化）
        /// ⭐ v3.3.2: 模糊时间感知，更自然
        /// </summary>
        public string TimeAgoString
        {
            get
            {
                int age = Age;
                
                // 超短期（<1小时 = <2500 ticks）
                if (age < 2500) return "刚才";
                
                // 短期（1-6小时）
                if (age < 15000) return "不久前";
                
                // 当天（6-24小时）
                if (age < 60000) return "今天";
                
                // 昨天
                if (age < 120000) return "昨天";
                
                // 前天
                if (age < 180000) return "前天";
                
                // 前几天（3-7天）
                if (age < 420000) return "前几天";
                
                // 上周（7-15天）
                if (age < 900000) return "上周";
                
                // 最近（15-30天）
                if (age < 1800000) return "最近";
                
                // 之前（30天-1年）
                if (age < 3600000) return "之前";
                
                // 很久以前（>1年）
                return "很久以前";
            }
        }

        /// <summary>
        /// 获取层级名称（中文）
        /// </summary>
        public string LayerName
        {
            get
            {
                switch (layer)
                {
                    case MemoryLayer.Active: return "超短期";
                    case MemoryLayer.Situational: return "短期";
                    case MemoryLayer.EventLog: return "中期";
                    case MemoryLayer.Archive: return "长期";
                    default: return "未知";
                }
            }
        }

        /// <summary>
        /// 获取类型名称（中文）
        /// </summary>
        public string TypeName
        {
            get
            {
                switch (type)
                {
                    case MemoryType.Conversation: return "对话";
                    case MemoryType.Action: return "行动";
                    case MemoryType.Observation: return "观察";
                    case MemoryType.Event: return "事件";
                    case MemoryType.Emotion: return "情绪";
                    case MemoryType.Relationship: return "关系";
                    case MemoryType.Internal: return "内部"; // ⭐ v3.3.2: 内部上下文
                    default: return "未知";
                }
            }
        }

        /// <summary>
        /// 衰减活跃度
        /// </summary>
        public void Decay(float rate)
        {
            if (!isPinned && !isUserEdited)
            {
                activity *= (1f - rate);
            }
        }

        /// <summary>
        /// 计算检索分数（用于相关性排序）
        /// </summary>
        public float CalculateRetrievalScore(string context, List<string> contextKeywords)
        {
            float score = 0f;

            // 时间因子（越新越好）
            float timeFactor = UnityEngine.Mathf.Exp(-(float)Age / 60000f);
            score += timeFactor * 0.3f;

            // 重要性因子
            score += importance * 0.3f;

            // 活跃度因子
            score += activity * 0.2f;

            // 相关性因子（关键词匹配）
            if (contextKeywords != null && contextKeywords.Count > 0)
            {
                int matchCount = keywords.Intersect(contextKeywords).Count();
                float relevance = (float)matchCount / UnityEngine.Mathf.Max(keywords.Count, contextKeywords.Count);
                score += relevance * 0.2f;
            }

            // 固定/编辑过的记忆优先级更高
            if (isPinned) score += 0.3f;
            if (isUserEdited) score += 0.2f;

            return score;
        }

        /// <summary>
        /// 添加标签（中文）
        /// </summary>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !tags.Contains(tag))
            {
                tags.Add(tag);
            }
        }

        /// <summary>
        /// 添加关键词
        /// </summary>
        public void AddKeyword(string keyword)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && !keywords.Contains(keyword))
            {
                keywords.Add(keyword);
            }
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public void RemoveTag(string tag)
        {
            tags.Remove(tag);
        }

        /// <summary>
        /// 获取显示用的简短描述
        /// </summary>
        public string GetShortDescription()
        {
            string prefix = $"[{LayerName}] {TypeName}";
            string timeStr = TimeAgoString;
            string contentPreview = content.Length > 40 ? content.Substring(0, 40) + "..." : content;
            
            return $"{prefix} · {timeStr} · {contentPreview}";
        }
    }

    /// <summary>
    /// 记忆查询参数
    /// </summary>
    public class MemoryQuery
    {
        public MemoryLayer? layer;
        public MemoryType? type;
        public string relatedPawn;
        public List<string> tags;
        public List<string> keywords;
        public int maxCount = 10;
        public bool includeContext = true;

        public MemoryQuery()
        {
            tags = new List<string>();
            keywords = new List<string>();
        }
    }
}
