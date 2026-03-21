using HarmonyLib;
using RimTalk.Data;
using RimTalk.MemoryPatch;
using RimTalk.Service;
using RimTalk.Source.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk.Memory.Patches
{

    // 捕获轮次记忆
    [HarmonyPatch(typeof(TalkService), "AddResponsesToHistory")]
    public static class TalkHistory_AddMessageHistory_Patch
    {
        // 通过设置控制启用
        private static bool IsEnabled => RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? false;

        // 正则清洗器
        private static readonly Regex RegexCleaner = new(@"</?(?:color[^>]*|b|i|size[^>]*)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [HarmonyPostfix]
        static void Postfix(List<TalkResponse> responses)
        {
            // 异步回调可能在存档退出后触发，此时 Game 为 null
            // 正常来说应该是谁调用，谁检查（MemoryEntry），但考虑到构造函数运行时已经new了对象
            // 所以这里还是决定提前检查一下
            if (!IsEnabled || Current.Game is null) return;

            // 将responses处理成原版就有的数据结构，再传给RoundMemoryManager
            if (responses is null || responses.Count == 0)
            {
                Log.Warning("[RimTalk.Memory.Patches] Attempted to Create RoundMemory with null.");
                return;
            }
            // 这里其实可以优化成一个foreach，但为了可读性，以及我觉得这几个LINQ非常的Coooool，就先这样吧
            string content = string.Join("\n", responses
                .Where(r => r is not null)
                .Select(r => (Name: CleanText(r.Name), Text: CleanText(r.Text)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .Select(x => $"{(string.IsNullOrWhiteSpace(x.Name) ? "???" : x.Name)}: {x.Text}")
            );
            // content是轮次记忆的必要元素，缺失时直接剪枝
            if (string.IsNullOrWhiteSpace(content))
            {
                Log.Warning("[RimTalk.Memory.Patches] Attempted to Create RoundMemory with empty content.");
                return;
            }
            var pawns = responses
                .Select(r => r?.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Select(n => Cache.GetByName(n)?.Pawn)
                .Where(p => p is not null)
                .ToHashSet();
            bool isPlayerInitiate = responses.FirstOrDefault(r => r is not null)?.TalkType == TalkType.User;

            // 将数据传给RoundMemoryManager
            RoundMemoryManager.BuildRoundMemory(pawns, content, isPlayerInitiate);
        }

        // 清洗文本
        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return RegexCleaner.Replace(text, string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        }
    }

}
