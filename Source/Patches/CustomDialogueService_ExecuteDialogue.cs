using HarmonyLib;
using RimTalk.Service;
using Verse;

namespace RimTalk.Memory.Patches
{

    // 捕获玩家发言
    [HarmonyPatch(typeof(CustomDialogueService), "ExecuteDialogue")]
    public static class CustomDialogueService_ExecuteDialogue
    {
        [HarmonyPostfix]
        static void Postfix(Pawn initiator, string message)
        {
            RoundMemoryManager.CapturePlayerDialogue(initiator, message);
        }
    }

}
