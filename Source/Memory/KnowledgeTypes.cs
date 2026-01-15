using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 关键词匹配模式
    /// </summary>
    public enum KeywordMatchMode
    {
        Any,    // 单词匹配：只要出现其中一个词就算
        All     // 组合匹配：必须同时出现所有标签
    }

    /// <summary>
    /// 匹配类型
    /// </summary>
    public enum KnowledgeMatchType
    {
        Keyword,    // 关键词匹配
        Vector,     // 向量检索
        Mixed       // 混合
    }

    /// <summary>
    /// 常识条目
    /// </summary>
    public class CommonKnowledgeEntry : IExposable
    {
        public string id;
        public string tag;          // 标签（支持多个，用逗号分隔）
        public string content;      // 内容（用于注入）
        public float importance;    // 重要性
        public List<string> keywords; // 关键词（可选，用户手动设置，不导出导入）
        public bool isEnabled;      // 是否启用
        public bool isUserEdited;   // 是否被用户编辑过（用于保护手动修改）
        
        // 目标Pawn限制（用于角色专属常识）
        public int targetPawnId = -1;  // -1表示全局，否则只对特定Pawn有效
        
        // 创建时间戳和原始事件文本（用于动态更新时间前缀）
        public int creationTick = -1;       // -1表示永久，>=0表示创建时的游戏tick
        public string originalEventText = "";  // 保存不带时间前缀的原始事件文本

        // 匹配控制属性
        public KeywordMatchMode matchMode = KeywordMatchMode.Any; // 关键词匹配模式（默认Any）
        
        private List<string> cachedTags; // 缓存分割后的标签列表

        /// <summary>
        /// 清除标签缓存（在修改tag后必须调用）
        /// </summary>
        public void InvalidateCache()
        {
            cachedTags = null;
        }

        public CommonKnowledgeEntry()
        {
            id = "ck-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            keywords = new List<string>();
            isEnabled = true;
            importance = 0.5f;
            targetPawnId = -1; // 默认全局
            creationTick = -1; // 默认永久
            originalEventText = "";
            matchMode = KeywordMatchMode.Any;
        }

        public CommonKnowledgeEntry(string tag, string content) : this()
        {
            this.tag = tag;
            this.content = content;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref tag, "tag");
            Scribe_Values.Look(ref content, "content");
            Scribe_Values.Look(ref importance, "importance", 0.5f);
            Scribe_Values.Look(ref isEnabled, "isEnabled", true);
            Scribe_Values.Look(ref isUserEdited, "isUserEdited", false);
            Scribe_Values.Look(ref targetPawnId, "targetPawnId", -1);
            Scribe_Values.Look(ref creationTick, "creationTick", -1);
            Scribe_Values.Look(ref originalEventText, "originalEventText", "");
            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);
            
            Scribe_Values.Look(ref matchMode, "matchMode", KeywordMatchMode.Any);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (keywords == null) keywords = new List<string>();
                cachedTags = null; // 清除缓存，强制重新解析
                
                // 兼容旧存档 - 如果没有originalEventText，从content中提取
                if (string.IsNullOrEmpty(originalEventText) && !string.IsNullOrEmpty(content))
                {
                    originalEventText = RemoveTimePrefix(content);
                }
            }
        }
        
        private static string RemoveTimePrefix(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            string[] timePrefixes = { "今天", "1天前", "2天前", "3天前", "4天前", "5天前", "6天前", 
                                     "约3天前", "约4天前", "约5天前", "约6天前", "约7天前" };
            
            foreach (var prefix in timePrefixes)
            {
                if (text.StartsWith(prefix))
                {
                    return text.Substring(prefix.Length);
                }
            }
            
            return text;
        }
        
        public void UpdateEventTimePrefix(int currentTick)
        {
            if (creationTick < 0 || string.IsNullOrEmpty(originalEventText))
                return;
            
            if (isUserEdited)
                return;
            
            int ticksElapsed = currentTick - creationTick;
            int daysElapsed = ticksElapsed / GenDate.TicksPerDay;
            
            string timePrefix = "";
            if (daysElapsed < 1)
            {
                timePrefix = "今天";
            }
            else if (daysElapsed == 1)
            {
                timePrefix = "1天前";
            }
            else if (daysElapsed == 2)
            {
                timePrefix = "2天前";
            }
            else if (daysElapsed < 7)
            {
                timePrefix = $"约{daysElapsed}天前";
            }
            else
            {
                timePrefix = "约7天前";
            }
            
            content = timePrefix + originalEventText;
        }

        public List<string> GetTags()
        {
            if (cachedTags != null)
                return cachedTags;
            
            if (string.IsNullOrEmpty(tag))
            {
                cachedTags = new List<string>();
                return cachedTags;
            }
            
            cachedTags = tag.Split(new[] { ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            
            return cachedTags;
        }

        public string FormatForExport()
        {
            // 获取扩展属性
            bool canBeExtracted = ExtendedKnowledgeEntry.CanBeExtracted(this);
            bool canBeMatched = ExtendedKnowledgeEntry.CanBeMatched(this);
            
            return $"[{tag}|{importance:F2}|{matchMode}|{canBeExtracted}|{canBeMatched}]{content}";
        }

        public override string ToString()
        {
            return FormatForExport();
        }
        
        public bool IsRuleKnowledge()
        {
            if (string.IsNullOrEmpty(tag))
                return false;
            
            string lowerTag = tag.ToLower();
            return lowerTag.Contains("规则") || 
                   lowerTag.Contains("instructions") || 
                   lowerTag.Contains("rule");
        }
    }

    /// <summary>
    /// 常识评分结果
    /// </summary>
    public class KnowledgeScore
    {
        public CommonKnowledgeEntry Entry;
        public float Score;
    }
    
    /// <summary>
    /// 常识评分详情（用于调试和UI展示）
    /// </summary>
    public class KnowledgeScoreDetail
    {
        public CommonKnowledgeEntry Entry;
        public bool IsEnabled;
        public float TotalScore;
        
        public float BaseScore;
        public float ManualBonus;
        public float MatchTypeScore;
        
        public KnowledgeMatchType MatchType;
        
        public float JaccardScore;
        public float TagScore;
        public float ImportanceScore;
        public int KeywordMatchCount;
        public List<string> MatchedKeywords = new List<string>();
        public List<string> MatchedTags = new List<string>();
        public string FailReason;
    }
    
    /// <summary>
    /// 关键词提取信息
    /// </summary>
    public class KeywordExtractionInfo
    {
        public List<string> ContextKeywords = new List<string>();
        public int TotalKeywords;
        public int PawnKeywordsCount;
        public PawnKeywordInfo PawnInfo;
    }
    
    /// <summary>
    /// Pawn关键词信息
    /// </summary>
    public class PawnKeywordInfo
    {
        public string PawnName;
        public List<string> NameKeywords = new List<string>();
        public List<string> AgeKeywords = new List<string>();
        public List<string> GenderKeywords = new List<string>();
        public List<string> RaceKeywords = new List<string>();
        public List<string> IdentityKeywords = new List<string>();  // 身份（殖民者/囚犯/奴隶/访客）
        public List<string> TraitKeywords = new List<string>();
        public List<string> SkillKeywords = new List<string>();
        public List<string> SkillLevelKeywords = new List<string>();
        public List<string> HealthKeywords = new List<string>();
        public List<string> RelationshipKeywords = new List<string>();
        public List<string> BackstoryKeywords = new List<string>();
        public List<string> ChildhoodKeywords = new List<string>();
        public int TotalCount;
    }
}