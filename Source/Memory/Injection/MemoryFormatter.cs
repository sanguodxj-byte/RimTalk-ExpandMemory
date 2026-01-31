using System.Collections.Generic;
using System.Text;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Injection
{
    /// <summary>
    /// 记忆格式化器
    /// 职责：统一编号 + 格式化输出
    /// </summary>
    public static class MemoryFormatter
    {
        /// <summary>
        /// 格式化记忆列表
        /// </summary>
        /// <param name="memories">记忆列表</param>
        /// <param name="startIndex">起始序号（默认1）</param>
        /// <returns>格式化的文本</returns>
        public static string Format(List<MemoryEntry> memories, int startIndex = 1)
        {
            if (memories == null || memories.Count == 0)
                return string.Empty;
            
            var sb = new StringBuilder();
            int index = startIndex;
            
            foreach (var memory in memories)
            {
                string typeTag = GetMemoryTypeTag(memory.type);
                string timeStr = GetTimeString(memory);
                
                // 格式：序号. [类型] 内容 (时间)
                sb.AppendLine($"{index}. [{typeTag}] {memory.content} ({timeStr})");
                index++;
            }
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 格式化单条记忆
        /// </summary>
        public static string FormatSingle(MemoryEntry memory, int index)
        {
            if (memory == null)
                return string.Empty;
            
            string typeTag = GetMemoryTypeTag(memory.type);
            string timeStr = GetTimeString(memory);
            
            return $"{index}. [{typeTag}] {memory.content} ({timeStr})";
        }
        
        /// <summary>
        /// 获取时间字符串
        /// ABM/SCM 使用模糊时间，ELS/CLPA 使用游戏日期
        /// </summary>
        private static string GetTimeString(MemoryEntry memory)
        {
            if (memory.layer == MemoryLayer.Active || memory.layer == MemoryLayer.Situational)
                return memory.TimeAgoString;
            else
                return memory.GameDateString;
        }
        
        /// <summary>
        /// 获取记忆类型标签
        /// </summary>
        public static string GetMemoryTypeTag(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Conversation:
                    return "Conversation";
                case MemoryType.Action:
                    return "Action";
                case MemoryType.Observation:
                    return "Observation";
                case MemoryType.Event:
                    return "Event";
                case MemoryType.Emotion:
                    return "Emotion";
                case MemoryType.Relationship:
                    return "Relationship";
                default:
                    return "Memory";
            }
        }
    }
}