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

namespace RimTalkHistoryPlus
{

    [HarmonyPatch(typeof(CustomDialogueService), "ExecuteDialogue")]
    public static class CustomDialogueService_ExecuteDialogue
    {
        [HarmonyPostfix]
        static void Postfix(Pawn initiator, string message)
        {
            HistoryManager.Instance?.PlayerDialogue = $"{initiator?.LabelShort}: {message}";
            Log.Message($"[RimTalkHistoryPlus] 成功捕获玩家发言");
        }
    }
}
