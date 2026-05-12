using System;
using System.Collections.Generic;
using RimWorld;
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
        public string id;                   // 唯一ID
        public int timestamp = -1;          // 时间戳
        public string content;              // 内容

        // 分类
        public MemoryType type;             // 类型
        public MemoryLayer layer;           // 层级

        // 重要性和活跃度
        public float importance = -1;       // 重要性 (0-1)
        public float activity = -1;         // 活跃度 (随时间衰减)

        // 关联信息
        public string relatedPawnId;        // 相关小人ID
        public string relatedPawnName;      // 相关小人名字
        public string location;             // 地点
        public List<string> tags = new();   // 标签（中文）
        public List<string> keywords = new();   // 关键词

        // 元数据
        public bool isUserEdited = false;   // 是否被用户编辑过
        public bool isPinned = false;       // 是否固定（不会被删除）
        public bool IsSummarized = false;   // 是否已被AI总结过，新生成的记忆默认为false
        public string notes;                // 用户备注

        /// <summary>
        /// 获取层级名称（中文）
        /// </summary>
        public string LayerName => layer switch
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
        public string TypeName => type switch
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
        /// 获取记忆年龄（单位tick）（仅限主线程访问）
        /// </summary>
        public int Age => Find.TickManager.TicksGame - timestamp;

        /// <summary>
        /// 获取记忆的游戏日期时间戳（精确到日期）
        /// 格式：5220年春12日
        /// </summary>
        public string GameDateString
        {
            get
            {
                try
                {
                    // 获取年份
                    int year = GenDate.Year(timestamp, 0);

                    // 获取日期（0-59，每年60天）
                    int dayOfYear = GenDate.DayOfYear(timestamp, 0);

                    // RimWorld 使用 Quadrum（季度）：0=春, 1=夏, 2=秋, 3=冬
                    // 每个季度15天
                    int quadrumIndex = dayOfYear / 15; // 0-3
                    int dayOfQuadrum = (dayOfYear % 15) + 1; // 1-15

                    string[] quadrumNames = { "春", "夏", "秋", "冬" };
                    string quadrumName = quadrumNames[quadrumIndex % 4];

                    return $"{year}年{quadrumName}{dayOfQuadrum}日";
                }
                catch (Exception ex)
                {
                    // 如果计算失败，记录错误并返回模糊时间
                    Log.Error($"[RimTalk Memory] Failed to generate GameDateString for timestamp {timestamp}: {ex.Message}");
                    return TimeAgoString;
                }
            }
        }

        /// <summary>
        /// 获取记忆年龄描述（完全口语化）
        /// 模糊时间感知，更自然
        /// </summary>
        public string TimeAgoString
        {
            get
            {
                int age = Age;

                // 超短期（<1小时 = <2500 ticks）
                if (age < GenDate.TicksPerHour) return "刚才";

                // 短期（1-6小时）
                if (age < GenDate.TicksPerHour * 6) return "不久前";

                // 当天（6-24小时）
                if (age < GenDate.TicksPerDay) return "今天";

                // 昨天
                if (age < GenDate.TicksPerDay * 2) return "昨天";

                // 前天
                if (age < GenDate.TicksPerDay * 3) return "前天";

                // 前几天（3-7天）
                if (age < GenDate.TicksPerDay * 7) return "前几天";

                // 上周（7-15天）
                if (age < GenDate.TicksPerDay * 15) return "上周";

                // 最近（15-30天）
                if (age < GenDate.TicksPerDay * 30) return "最近";

                // 之前（30天-1年）
                if (age < GenDate.TicksPerYear) return "之前";

                // 很久以前（>1年）
                return "很久以前";
            }
        }

        public MemoryEntry() { }

        public MemoryEntry(string content, MemoryType type, MemoryLayer layer, float importance = 1f, string relatedPawn = null)
        {
            id = "mem-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            timestamp = Find.TickManager?.TicksGame ?? -1;
            this.content = content;

            this.type = type;
            this.layer = layer;

            activity = 1f;
            this.importance = importance;

            relatedPawnName = relatedPawn;

            // 自动添加类型标签
            AddTypeTag();
        }
        private void AddTypeTag()
        {
            AddTag(type switch
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
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref timestamp, "timestamp", -1);
            Scribe_Values.Look(ref content, "content");

            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref layer, "layer");

            Scribe_Values.Look(ref importance, "importance", -1);
            Scribe_Values.Look(ref activity, "activity", -1);

            Scribe_Values.Look(ref relatedPawnId, "relatedPawnId");
            Scribe_Values.Look(ref relatedPawnName, "relatedPawnName");
            Scribe_Values.Look(ref location, "location");
            Scribe_Collections.Look(ref tags, "tags", LookMode.Value);
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);

            Scribe_Values.Look(ref isUserEdited, "isUserEdited", false);
            Scribe_Values.Look(ref isPinned, "isPinned", false);
            Scribe_Values.Look(ref IsSummarized, "IsSummarized", true); // 旧存档中的记忆默认为true以向后兼容
            Scribe_Values.Look(ref notes, "notes");

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
            if (isPinned) return; // 固定的记忆不衰减

            activity *= (1f - rate);
        }

        /// <summary>
        /// 计算检索分数（用于相关性排序）
        /// </summary>
        public float CalculateRetrievalScore(string context, List<string> contextKeywords)
        {
            float score = 0f;

            // 时间因子（越新越好）
            float timeFactor = (float)Math.Exp(-(float)Age / GenDate.TicksPerDay);
            score += timeFactor * 0.3f;

            // 重要性因子
            score += importance * 0.3f;

            // 活跃度因子
            score += activity * 0.2f;

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
            if (isPinned) score += 0.3f;
            if (isUserEdited) score += 0.2f;

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
