using HarmonyLib;
using RimTalk.Data;
using RimTalk.MemoryPatch;
using RimTalk.Service;
using Verse;

namespace RimTalk.Memory.Patches.Capture
{

    // 此管线仅用于最低限度保留旧功能，未来不再维护
    /// <summary>
    /// 捕获单条对话，为说话者和听者分别记录短期记忆
    /// 当 RoundMemory 模式启用时，此 Patch 自行禁用，由 TalkService_CreateInteraction_Patch 接管
    /// </summary>
    [HarmonyPatch(typeof(TalkService), "CreateInteraction")]
    public static class RimTalkConversationCapturePatch
    {
        [HarmonyPostfix]
        static void Postfix(Pawn pawn, TalkResponse talk)
        {
            if (// 配置项
                (!RimTalkMemoryPatchMod.Settings?.enableConversationMemory ?? true)
                || (RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? true)

                // 校验 talk 有效性
                || talk is null
                || talk.Text is not { } text
                || string.IsNullOrEmpty(text)

                // 校验 pawn 有效性
                || pawn is null
                || pawn.TryGetComp<FourLayerMemoryComp>() is not { } speakerComp)
                return;

            // 获取听者 Pawn，并判断是否为独白
            Pawn target = talk.GetTarget();
            bool isMonologue = target is null || target == pawn;

            // 为 speaker 记录记忆
            speakerComp.ActiveMemories.Add(
                new MemoryEntry(
                    $"Said to {(isMonologue ? "self" : target.LabelShort)}: {text}",
                    MemoryType.Conversation,
                    MemoryLayer.Active
                    )
                );

            // 为 listener 记录记忆
            if (isMonologue || target.TryGetComp<FourLayerMemoryComp>() is not { } listenerComp) return;

            listenerComp.ActiveMemories.Add(
                new MemoryEntry(
                    $"{pawn.LabelShort} said: {text}",
                    MemoryType.Conversation,
                    MemoryLayer.Active
                    )
                );
        }
    }

}
