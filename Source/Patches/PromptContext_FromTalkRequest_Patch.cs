using HarmonyLib;
using RimTalk.Data;
using RimTalk.Prompt;
using System.Collections.Generic;
using Verse;

namespace RimTalk.Memory.Patches
{

    // 填充 Participants
    // 注意！！！后续考虑将此逻辑直接并入 RimTalk 本体中！
    [HarmonyPatch(typeof(PromptContext), "FromTalkRequest")]
    public static class PromptContext_FromTalkRequest_Patch
    {
        [HarmonyPrefix]
        static void Prefix(TalkRequest request, List<Pawn> pawns)
        {
            request?.Participants = pawns;
        }
    }

}
