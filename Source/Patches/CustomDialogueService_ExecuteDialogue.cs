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
            Log.Message($"[RoundMemory] 成功捕获玩家发言"); 
        }
    }
}
