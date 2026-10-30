using System;
using System.Collections.Concurrent;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalk.Memory.Utils;

[HarmonyPatch(typeof(Root), nameof(Root.Update))]
public static class MessageUtil
{
    private static readonly ConcurrentQueue<Action> pendingActions = new();

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
            pendingActions.Enqueue(action);
        }
    }

    [HarmonyPostfix]
    private static void DrainPendingActions()
    {
        if (!UnityData.IsInMainThread || LongEventHandler.AnyEventNowOrWaiting) return;

        int count = pendingActions.Count;
        for (int i = 0; i < count && !LongEventHandler.AnyEventNowOrWaiting; i++)
        {
            if (!pendingActions.TryDequeue(out var action)) break;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
    }
}
