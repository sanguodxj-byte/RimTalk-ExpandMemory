using System;
using RimWorld;
using Verse;

namespace RimTalk.Memory.Utils;

public static class MessageUtil
{
    public static void MessageAndError(string message)
    {
        RunOnMainThread(() =>
        {
            Messages.Message(
                $"[RimTalk.Memory.AI] {message}",
                MessageTypeDefOf.RejectInput,
                historical: false
                );
        });
        Log.Error($"[RimTalk.Memory.AI] {message}");
    }

    public static void RunOnMainThread(Action action)
    {
        if (action == null) return;
        if (UnityData.IsInMainThread)
        {
            action();
        }
        else
        {
            LongEventHandler.QueueLongEvent(action, null, false, null, false, true);
        }
    }
}
