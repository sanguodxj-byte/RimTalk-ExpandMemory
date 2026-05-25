using HarmonyLib;
using RimTalk.Data;
using RimTalk.MemoryPatch;
using RimTalk.Service;
using RimTalk.Source.Data;
using System.Text.RegularExpressions;

namespace RimTalk.Memory.Patches.Capture
{

    // 用于流式捕获发言，转换成原版数据结构传给 RoundMemoryManager
    [HarmonyPatch(typeof(TalkService), "CreateInteraction")]
    public static class TalkService_CreateInteraction_Patch
    {
        // 通过设置控制启用
        private static bool IsEnabled => RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? false;

        // 正则清洗器
        private static readonly Regex RegexCleaner = new(@"</?(?:color[^>]*|b|i|size[^>]*)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [HarmonyPostfix]
        static void Postfix(TalkResponse talk)
        {
            if (!IsEnabled
                || talk is null
                || ApiHistory.GetApiLog(talk.Id) is not { } apiLog
                || apiLog.Channel is Channel.User   // 由玩家输入直接产生的那条 response 不处理
                || apiLog.TalkRequest is not { } talkRequest)   // talkRequest 即当前 response 的唯一标识
                return;

            // 构建 content
            string name = talk.Name;
            string content = $"{(string.IsNullOrWhiteSpace(name) ? "???" : name)}: {CleanText(talk.Text)}";

            // 判断是否为玩家发起
            bool isPlayerInitiate = talk.TalkType.IsFromUser();

            // 将转换好的数据发送给 RoundMemoryManager
            RoundMemoryManager.StreamingBuildRoundMemory(talkRequest, content, talkRequest.Participants, isPlayerInitiate);
        }

        // 清洗文本
        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return RegexCleaner.Replace(text, string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        }
    }

}
