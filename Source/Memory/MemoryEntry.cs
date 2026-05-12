using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Memory
{

    /// <summary>
    /// 记忆条目
    /// 本质是超长期储存单元，可能出现跨存档甚至跨版本的情况
    /// 因此其字段应当尽可能由基本类型构成
    /// </summary>
    public class MemoryEntry : IExposable
    {
        // 基本信息
        public string Id;                   // 唯一ID
        // 更应当存储 AbsTick，无奈屎山已经堆起来了
        public int GameTick = -1;           // 时间戳（单位 tick）
        public string Content;              // 内容

        // 分类
        public MemoryType Type;             // 类型
        public MemoryLayer Layer;           // 层级

        // 重要性和活跃度
        public float Importance = -1;       // 重要性 (0-1)
        public float Activity = -1;         // 活跃度 (随时间衰减)

        // 关联信息
        public string relatedPawnId;        // 相关小人ID
        public string relatedPawnName;      // 相关小人名字
        public string location;             // 地点
        public List<string> tags = new();   // 标签（中文）
        public List<string> keywords = new();   // 关键词

        // 元数据
        public bool IsUserEdited = false;   // 是否被用户编辑过
        public bool IsPinned = false;       // 是否固定（不会被删除）
        public bool IsSummarized = false;   // 是否已被AI总结过，新生成的记忆默认为false
        public string Notes;                // 用户备注

        /// <summary>
        /// 获取层级名称（中文）
        /// </summary>
        public string LayerName => Layer switch
        {
            MemoryLayer.Active => "超短期",
            MemoryLayer.Situational => "短期",
            MemoryLayer.EventLog => "中期",
            MemoryLayer.Archive => "长期",
            _ => "未知"
        };

        /// <summary>
        /// 获取类型名称（中文）
        /// </summary>
        public string TypeName => Type switch
        {
            MemoryType.Conversation => "对话",
            MemoryType.Action => "行动",
            MemoryType.Observation => "观察",
            MemoryType.Event => "事件",
            MemoryType.Emotion => "情绪",
            MemoryType.Relationship => "关系",
            MemoryType.Internal => "内部",
            _ => "未知"
        };

        /// <summary>
        /// 是否可以被总结
        /// </summary>
        public virtual bool CanBeSummarized => !IsSummarized;

        /// <summary>
        /// 获取记忆年龄描述
        /// 根据年龄大小返回相对描述（如“刚刚”、“一天前”）或具体日期（如“5501年春1日”）
        /// </summary>
        public string AgeString => (Find.TickManager?.TicksGame - GameTick) switch
        {
            null or < 0 => "异常时间",
            < GenDate.TicksPerHour => "刚刚",
            < GenDate.TicksPerHour * 6 => "几小时前",
            < GenDate.TicksPerDay => "一天内",
            < GenDate.TicksPerDay * 2 => "一天前",
            < GenDate.TicksPerDay * 3 => "前天",
            < GenDate.TicksPerDay * 7 => "前几天",
            < GenDate.TicksPerDay * 14 => "上周",
            _ => GenDate.DateFullStringAt(GenDate.TickGameToAbs(GameTick), Vector2.zero)
        };

        public MemoryEntry() { }

        public MemoryEntry(string content, MemoryType type, MemoryLayer layer, float importance = 1f, string relatedPawn = null)
        {
            Id = "mem-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            GameTick = Find.TickManager?.TicksGame ?? -1;
            Content = content;

            Type = type;
            Layer = layer;

            Activity = 1f;
            Importance = importance;
            relatedPawnName = relatedPawn;

            // 自动添加类型标签
            AddTypeTag();
        }
        private void AddTypeTag()
        {
            AddTag(Type switch
            {
                MemoryType.Conversation => "对话",
                MemoryType.Action => "行动",
                MemoryType.Observation => "观察",
                MemoryType.Event => "事件",
                MemoryType.Emotion => "情绪",
                MemoryType.Relationship => "关系",
                MemoryType.Internal => "内部上下文",
                _ => null
            });
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref GameTick, "timestamp", -1);
            Scribe_Values.Look(ref Content, "content");

            Scribe_Values.Look(ref Type, "type");
            Scribe_Values.Look(ref Layer, "layer");

            Scribe_Values.Look(ref Importance, "importance", -1);
            Scribe_Values.Look(ref Activity, "activity", -1);

            Scribe_Values.Look(ref relatedPawnId, "relatedPawnId");
            Scribe_Values.Look(ref relatedPawnName, "relatedPawnName");
            Scribe_Values.Look(ref location, "location");
            Scribe_Collections.Look(ref tags, "tags", LookMode.Value);
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);

            Scribe_Values.Look(ref IsUserEdited, "isUserEdited", false);
            Scribe_Values.Look(ref IsPinned, "isPinned", false);
            Scribe_Values.Look(ref IsSummarized, "IsSummarized", true); // 旧存档中的记忆默认为true以向后兼容
            Scribe_Values.Look(ref Notes, "notes");

            // 集合型字段应当在读档后进行防空处理
            tags ??= new();
            keywords ??= new();
        }

        /// <summary>
        /// 添加标签（中文）
        /// </summary>
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags.Contains(tag)) return;

            tags.Add(tag);
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public void RemoveTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;

            tags.Remove(tag);
        }

        /// <summary>
        /// 添加关键词
        /// </summary>
        public void AddKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) || keywords.Contains(keyword)) return;

            keywords.Add(keyword);
        }

        /// <summary>
        /// 移除关键词
        /// </summary>
        public void RemoveKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;

            keywords.Remove(keyword);
        }

        /// <summary>
        /// 衰减活跃度
        /// </summary>
        public void Decay(float rate)
        {
            if (IsPinned) return; // 固定的记忆不衰减

            Activity *= (1f - rate);
        }

        /// <summary>
        /// 计算检索分数（用于相关性排序）
        /// </summary>
        public float CalculateRetrievalScore(string context, List<string> contextKeywords)
        {
            float score = 0f;

            // 时间因子（越新越好）
            float timeFactor = (float)Math.Exp(-(float)(Find.TickManager.TicksGame - GameTick) / GenDate.TicksPerDay);
            score += timeFactor * 0.3f;

            // 重要性因子
            score += Importance * 0.3f;

            // 活跃度因子
            score += Activity * 0.2f;

            // 相关性因子（关键词匹配）
            if (contextKeywords != null && contextKeywords.Count > 0)
            {
                int matchCount = 0;
                foreach (var kw in keywords)
                {
                    if (contextKeywords.Contains(kw)) matchCount++;
                }
                float relevance = (float)matchCount / Math.Max(keywords.Count, contextKeywords.Count);
                score += relevance * 0.2f;
            }

            // 固定/编辑过的记忆优先级更高
            if (IsPinned) score += 0.3f;
            if (IsUserEdited) score += 0.2f;

            return score;
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
