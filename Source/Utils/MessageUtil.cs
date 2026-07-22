using RimWorld;
using Verse;

namespace RimTalk.Memory.Utils;

public static class MessageUtil
{
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
