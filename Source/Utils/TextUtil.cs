using Verse;

namespace RimTalk.Memory.Utils
{
    public static class TextUtil
    {
        public static string Translate(this MemoryLayer? layer) => layer switch
        {
            null => "RimTalk.Memory.Utils.Layers".Translate(),
            _ => ((MemoryLayer)layer).Translate()
        };
        public static string Translate(this MemoryLayer layer) => Translator.Translate(layer switch
        {
            MemoryLayer.Active => "RimTalk.Memory.Utils.Layer_Active",
            MemoryLayer.Situational => "RimTalk.Memory.Utils.Layer_Situational",
            MemoryLayer.EventLog => "RimTalk.Memory.Utils.Layer_EventLog",
            MemoryLayer.Archive => "RimTalk.Memory.Utils.Layer_Archive",
            _ => "RimTalk.Memory.Utils.Layer_Default"
        });

        public static string Translate(this MemoryType? type) => type switch
        {
            null => "RimTalk.Memory.Utils.Types".Translate(),
            _ => ((MemoryType)type).Translate()
        };
        public static string Translate(this MemoryType type) => Translator.Translate(type switch
        {
            MemoryType.Conversation => "RimTalk_MindStream_Conversation",
            MemoryType.Action => "RimTalk_MindStream_Action",
            MemoryType.Summarization => "RimTalk_Archive_TypeSummary",
            MemoryType.Event => "RimTalk_Archive_TypeEvent",
            MemoryType.Emotion => "RimTalk_Archive_TypeEmotion",
            MemoryType.Relationship => "RimTalk_Archive_TypeRelationship",
            _ => "RimTalk_Archive_Memory"
        });
    }
}
