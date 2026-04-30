using System;

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
        [Obsolete("互动记忆已废弃，保留此枚举值仅为兼容旧存档")]
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

}
