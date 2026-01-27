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

    [HarmonyPatch(typeof(TalkService), "AddResponsesToHistory")]
    public static class TalkHistory_AddMessageHistory_Patch
    {
        [HarmonyPostfix]
        static void Postfix(List<TalkResponse> responses)
        {
            // 避免在游戏未加载时调用，这是有必要的，因为AddResponsesToHistory是一个被异步回调的方法，可能在退出存档时被调用
            // 但事实上，我自己的History构造函数足够健壮，可以处理这种情况
            // 但是，它的父类MemoryEntry让我不得不加这一步判断，增加高达*几纳秒*的性能开销
            if (Current.Game == null) return;

            // 创建 History 对象并通过 HistoryManager 统一添加（集中管理、保证上限）
            //Log.Message($"[RimTalkHistoryPlus] 成功捕获对话");
            var history = new History(responses);
            HistoryManager.AddHistory(history);
        }
    }
}
