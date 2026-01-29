using HarmonyLib;
using RimTalk;
using RimTalk.Data;
using RimTalk.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.Patches
{

    [HarmonyPatch(typeof(TalkService), "AddResponsesToHistory")]
    public static class TalkHistory_AddMessageHistory_Patch
    {
        [HarmonyPostfix]
        static void Postfix(List<TalkResponse> responses)
        {
            // 异步回调可能在存档退出后触发，此时 Game 为 null
            if (Current.Game == null) return;

            // 捕获对话并存入回合记忆
            var roundMemory = new RoundMemory(responses);
            RoundMemoryManager.AddRoundMemory(roundMemory);
        }
    }
}
