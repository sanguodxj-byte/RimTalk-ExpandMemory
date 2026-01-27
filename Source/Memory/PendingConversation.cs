using System.Collections.Generic;

namespace RimTalk.Memory
{
    /// <summary>
    /// 单条对话行（用于异步线程安全传递）
    /// </summary>
    public readonly struct DialogueLine
    {
        /// <summary>
        /// 说话者名字
        /// </summary>
        public readonly string SpeakerName;
        
        /// <summary>
        /// 对话内容
        /// </summary>
        public readonly string Text;
        
        public DialogueLine(string speakerName, string text)
        {
            SpeakerName = speakerName ?? "???";
            Text = text ?? "";
        }
    }
    
    /// <summary>
    /// 待处理的对话记录
    /// 用于从异步线程传递到主线程
    /// </summary>
    public class PendingConversation
    {
        /// <summary>
        /// 所有参与者的 ThingID（用于在主线程查找 Pawn）
        /// </summary>
        public List<string> ParticipantThingIds { get; set; }
        
        /// <summary>
        /// 所有参与者的名字（主线程缓存，用于格式化对话头部）
        /// </summary>
        public List<string> ParticipantNames { get; set; }
        
        /// <summary>
        /// 原始对话行（异步线程提取的 speaker + text）
        /// </summary>
        public List<DialogueLine> RawDialogue { get; set; }
        
        /// <summary>
        /// 对话发生的游戏 Tick
        /// </summary>
        public int Timestamp { get; set; }
        
        /// <summary>
        /// ⭐ 对话唯一ID（用于跨Pawn去重）
        /// </summary>
        public string ConversationId { get; set; }
        
        public PendingConversation()
        {
            ParticipantThingIds = new List<string>();
            ParticipantNames = new List<string>();
            RawDialogue = new List<DialogueLine>();
            ConversationId = "conv-" + System.Guid.NewGuid().ToString("N").Substring(0, 12);
        }
    }
    
    /// <summary>
    /// 缓存的参与者信息（主线程 BuildMessages 时缓存）
    /// </summary>
    public class CachedParticipants
    {
        /// <summary>
        /// 所有参与者的 ThingID
        /// </summary>
        public List<string> ThingIds { get; set; }
        
        /// <summary>
        /// 所有参与者的名字
        /// </summary>
        public List<string> Names { get; set; }
        
        public CachedParticipants()
        {
            ThingIds = new List<string>();
            Names = new List<string>();
        }
    }
}