using HarmonyLib;
using RimTalk.MemoryPatch;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace RimTalk.Memory.Patches.RimChat
{

    // 捕获RimChat对话
    [HarmonyPatch("RimChat.Memory.RpgNpcDialogueArchiveManager", "RecordDiplomacySummary")]
    public static class RpgNpcDialogueArchiveManager_RecordDiplomacySummary_Patch
    {
        // 声明字段访问器
        private static Type dialogueMessageDataType;
        private static AccessTools.FieldRef<object, bool> isPlayerRef;
        private static AccessTools.FieldRef<object, string> messageRef;

        //声明方法访问器
        private static FastInvokeHandler isSystemMessageInvoker;

        private static bool IsEnable => RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? false;

        // 通过Prepare方法控制补丁启用，并初始化访问器
        static bool Prepare()
        {
            try
            {
                // 尝试获取ChatMessageData类
                dialogueMessageDataType = AccessTools.TypeByName("RimChat.Memory.DialogueMessageData");
                if (dialogueMessageDataType is null)
                {
                    Log.Message("[RimTalk.Memory.Patches.RimChat]: 无法找到DialogueMessageData类型，补丁将被禁用。");
                    return false;
                }

                // 获取成功，尝试获取方法
                var isSystemMessageMethod = AccessTools.Method(dialogueMessageDataType, "IsSystemMessage");
                if (isSystemMessageMethod is null)
                {
                    Log.Message("[RimTalk.Memory.Patches.RimChat]: 无法找到IsSystemMessage方法，补丁将被禁用。");
                    return false;
                }

                // 获取成功，初始化访问器
                isSystemMessageInvoker = MethodInvoker.GetHandler(isSystemMessageMethod);
                isPlayerRef = AccessTools.FieldRefAccess<bool>(dialogueMessageDataType, "isPlayer");
                messageRef = AccessTools.FieldRefAccess<string>(dialogueMessageDataType, "message");

                Log.Message("[RimTalk.Memory.Patches.RimChat]: 所有访问器初始化成功。");
                return true;
            }
            catch 
            {
                Log.Error("[RimTalk.Memory.Patches.RimChat]: 初始化RimChat补丁时出现异常");
                return false;
            }
        }

        // 补丁主体
        [HarmonyPrefix]
        static void Prefix(Pawn negotiator, Faction faction, IList allMessages)
        {
            // Log.Message("[RimTalk.Memory.Patches.RimChat]: RecordDiplomacySummary被调用，尝试捕获对话内容。");

            // 轮次记忆关闭时不启用
            if (!IsEnable) return;

            if (allMessages is null || allMessages.Count == 0) return;

            // 构建文本块
            StringBuilder sb = new();

            // 获取名字
            string playerName = negotiator?.LabelShort ?? "???";
            string factionName = faction?.Name ?? "???";

            // 开始构建
            foreach (var dialogueMessage in allMessages)
            {
                // 跳过系统消息和无效消息
                if (dialogueMessage is null || (bool)isSystemMessageInvoker(dialogueMessage)) continue;

                if (isPlayerRef(dialogueMessage))
                {
                    // 玩家发言
                    sb.Append(playerName).Append(": ").AppendLine(messageRef(dialogueMessage));
                }
                else
                {
                    // NPC派系发言
                    sb.Append(factionName).Append(": ").AppendLine(messageRef(dialogueMessage));
                }
            }
            // 若content将为空，则直接剪枝
            if (sb.Length == 0) return;

            // 取出最终字符串并剔除末尾多余的一个换行符
            string content = sb.ToString().TrimEnd();

            // 构建参与者集合
            // 其实可以把派系发言人从dialogueMessage里扒出来
            // 但向地图外的pawn添加轮次记忆感觉不是很安全，遂作罢
            HashSet<Pawn> pawns = [negotiator];

            // Log.Message($"[RimTalk.Memory.Patches.RimChat]: 成功捕获对话内容，长度{content.Length}，参与者{playerName}和{factionName}。");

            // 将数据传给RoundMemoryManager
            RoundMemoryManager.BuildRoundMemory(pawns, content);
        }
    }

}
