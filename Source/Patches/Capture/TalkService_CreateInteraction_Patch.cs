using HarmonyLib;
using RimTalk.Data;
using RimTalk.MemoryPatch;
using RimTalk.Service;
using RimTalk.Source.Data;

namespace RimTalk.Memory.Patches.Capture
{

    // 用于流式捕获发言，转换成原版数据结构传给 RoundMemoryManager
    [HarmonyPatch(typeof(TalkService), "CreateInteraction")]
    public static class TalkService_CreateInteraction_Patch
    {
        [HarmonyPostfix]
        static void Postfix(TalkResponse talk)
        {
            if ((!RimTalkMemoryPatchMod.Settings?.enableConversationMemory ?? true)
                || (!RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? true)
                || talk is null
                || ApiHistory.GetApiLog(talk.Id) is not { } apiLog
                || apiLog.Channel is Channel.User   // 由玩家输入直接产生的那条 response 不处理
                || apiLog.TalkRequest is not { } talkRequest)   // talkRequest 即当前 response 的唯一标识
                return;

            string name = talk.Name;

            // 转换数据并发送给 RoundMemoryManager
            RoundMemoryManager.StreamingBuildRoundMemory(
                talkRequest,
                $"{(string.IsNullOrWhiteSpace(name) ? "???" : name)}: {talk.Text}",
                talkRequest.Participants,
                isPlayerInitiate: talk.TalkType.IsFromUser());
        }
    }

}
