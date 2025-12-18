using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimTalk.Client;
using RimTalk.Client.Player2;
using RimTalk.Memory.VectorDB;
using RimTalk.Data;
using Verse;
using RimTalk.MemoryPatch;
using System.Linq;
using System.Text;

namespace RimTalk.Memory.Patches
{
    [HarmonyPatch(typeof(Player2Client), "GetChatCompletionAsync")]
    public static class Patch_Player2Client_GetChatCompletionAsync
    {
        private static readonly System.Threading.ThreadLocal<bool> _isInsidePatch = new System.Threading.ThreadLocal<bool>(() => false);

        static bool Prefix(Player2Client __instance, string instruction, List<(Role role, string message)> messages, ref Task<Payload> __result)
        {
            if (!RimTalkMemoryPatchMod.Settings.enableVectorEnhancement || _isInsidePatch.Value)
            {
                return true; // Skip patch if disabled or already inside
            }

            string userMessage = messages.LastOrDefault(m => m.role == Role.User).message;
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return true;
            }

            var tcs = new TaskCompletionSource<Payload>();
            __result = tcs.Task;

            Task.Run(async () =>
            {
                try
                {
                    var settings = RimTalkMemoryPatchMod.Settings;
                    var bestLores = await VectorService.Instance.FindBestLoreIdsAsync(userMessage, settings.maxVectorResults, settings.vectorSimilarityThreshold).ConfigureAwait(false);

                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        try
                        {
                            if (bestLores.Any())
                            {
                                var memoryManager = Find.World.GetComponent<MemoryManager>();
                                if (memoryManager != null)
                                {
                                    StringBuilder loreBuilder = new StringBuilder();
                                    loreBuilder.AppendLine("[Context from World Knowledge]:");
                                    foreach (var loreInfo in bestLores)
                                    {
                                        var entry = memoryManager.CommonKnowledge.Entries.FirstOrDefault(e => e.id == loreInfo.id);
                                        if (entry != null)
                                        {
                                            loreBuilder.AppendLine($"- {entry.content} (Similarity: {loreInfo.similarity:P1})");
                                        }
                                    }
                                    messages.Insert(0, (Role.User, loreBuilder.ToString()));
                                }
                            }

                            CallOriginalMethod(__instance, instruction, messages).ContinueWith(task =>
                            {
                                if (task.IsFaulted) tcs.SetException(task.Exception);
                                else if (task.IsCanceled) tcs.SetCanceled();
                                else tcs.SetResult(task.Result);
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                        catch (Exception ex) { tcs.SetException(ex); }
                    });
                }
                catch (Exception ex)
                {
                    LongEventHandler.ExecuteWhenFinished(() => tcs.SetException(ex));
                }
            });

            return false;
        }

        private static Task<Payload> CallOriginalMethod(Player2Client instance, string instruction, List<(Role role, string message)> messages)
        {
            try
            {
                _isInsidePatch.Value = true;
                MethodInfo originalMethod = typeof(Player2Client).GetMethod("GetChatCompletionAsync", BindingFlags.Public | BindingFlags.Instance);
                var result = (Task<Payload>)originalMethod.Invoke(instance, new object[] { instruction, messages });
                result.ContinueWith(_ => _isInsidePatch.Value = false);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] Patch_Player2Client: Error calling original method: {ex}");
                _isInsidePatch.Value = false;
                throw;
            }
        }
    }
}
