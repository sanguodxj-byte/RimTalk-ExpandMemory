using HarmonyLib;
using RimTalk.MemoryPatch;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace RimTalk.Memory.Patches.RimChat
{

    // 捕获RimChat RPG对话
    [HarmonyPatch("RimChat.Memory.RpgNpcDialogueArchiveManager", "FinalizeSession")]
    public static class RpgNpcDialogueArchiveManager_FinalizeSession_Patch
    {
        // 声明字段访问器
        private static Type chatMessageDataType;
        private static AccessTools.FieldRef<object, string> roleRef;
        private static AccessTools.FieldRef<object, string> contentRef;

        private static bool IsEnable => RimTalkMemoryPatchMod.Settings?.IsRoundMemoryActive ?? false;

        // 通过Prepare方法控制补丁启用，并初始化访问器
        static bool Prepare()
        {
            try
            {
                // 尝试获取ChatMessageData类
                chatMessageDataType = AccessTools.TypeByName("RimChat.AI.ChatMessageData");
                if (chatMessageDataType is null)
                {
                    Log.Message("[RimTalk.Memory.Patches.RimChat]: 无法找到ChatMessageData类型，补丁将被禁用。");
                    return false;
                }

                // 获取成功，初始化字段访问器
                roleRef = AccessTools.FieldRefAccess<string>(chatMessageDataType, "role");
                contentRef = AccessTools.FieldRefAccess<string>(chatMessageDataType, "content");
                Log.Message("[RimTalk.Memory.Patches.RimChat]: 字段访问器初始化成功。");
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
        static void Prefix(Pawn initiator, Pawn targetNpc, IList chatHistory)
        {
            // 轮次记忆关闭时不启用
            if (!IsEnable) return;

            if (chatHistory is null || chatHistory.Count == 0) return;

            // 构建文本块
            StringBuilder sb = new();

            // 获取名字
            string playerName = initiator?.LabelShort ?? "???";
            string npcName = targetNpc?.LabelShort ?? "???";

            // 开始构建
            foreach (var chatMessage in chatHistory)
            {
                if (chatMessage is null) continue;

                string role = roleRef(chatMessage);
                switch (role)
                {
                    case "user":
                        // 玩家发言
                        sb.Append(playerName).Append(": ").AppendLine(contentRef(chatMessage));
                        break;
                    case "assistant":
                        // NPC发言
                        sb.Append(npcName).Append(": ").AppendLine(contentRef(chatMessage));
                        break;
                    default:
                        // System或其他角色发言，不处理
                        break;
                }
            }
            // 若content将为空，则直接剪枝
            if (sb.Length == 0) return;

            // 取出最终字符串并剔除末尾多余的一个换行符
            string content = sb.ToString().TrimEnd();

            // 构建参与者集合
            HashSet<Pawn> pawns = [initiator, targetNpc];

            // 将数据传给RoundMemoryManager
            RoundMemoryManager.BuildRoundMemory(pawns, content);
        }
    }

}
