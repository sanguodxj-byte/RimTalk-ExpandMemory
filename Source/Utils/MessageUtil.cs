using RimWorld;
using Verse;

namespace RimTalk.Memory.Utils;

public static class MessageUtil
{
    /// <summary>
    /// 这是一个原子化的 Message 操作，不应当自己封装线程保护逻辑
    /// 确保此方法在主线程调用，是调用方的责任
    /// </summary>
    public static void MessageAndError(string message)
    {
        Messages.Message(
            $"[RimTalk.Memory.AI] {message}",
            MessageTypeDefOf.RejectInput,
            historical: false
            );
        Log.Error($"[RimTalk.Memory.AI] {message}");
    }
}
