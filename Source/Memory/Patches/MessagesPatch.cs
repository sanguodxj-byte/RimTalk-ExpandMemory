using System;
using HarmonyLib;
using Verse;
using RimWorld;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// ? 已废弃：MessagesPatch
    /// 原因：PlayLog已经包含所有重要事件，不需要重复监听Message
    /// EventRecordKnowledgeGenerator现在只从PlayLog读取事件
    /// </summary>
    /*
    [HarmonyPatch(typeof(Messages))]
    [HarmonyPatch(nameof(Messages.Message))]
    [HarmonyPatch(new Type[] { typeof(string), typeof(MessageTypeDef), typeof(bool) })]
    public static class MessagesPatch
    {
        [HarmonyPostfix]
        public static void Postfix(string text, MessageTypeDef def, bool historical)
        {
            // 已废弃
        }
    }
    */
}
