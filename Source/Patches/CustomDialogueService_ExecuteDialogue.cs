using HarmonyLib;
using RimTalk;
using RimTalk.Data;
using RimTalk.Service;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.Patches
{

    [HarmonyPatch(typeof(CustomDialogueService), "ExecuteDialogue")]
    public static class CustomDialogueService_ExecuteDialogue
    {
        [HarmonyPostfix]
        static void Postfix(Pawn initiator, string message)
        {
            var manager = RoundMemoryManager.Instance;
            if (manager == null) return;
            manager.Player = initiator;
            manager.PlayerDialogue = $"{initiator?.LabelShort}: {message}";
            Log.Message($"[轮次记忆] 成功捕获玩家发言"); // 别动，调试用，比原来的捕获日志频率还更低些。总之别删
        }
    }
}
