using RimTalk;
using RimTalk.API;
using RimTalk.Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalkHistoryPlus
{
    [StaticConstructorOnStartup]
    // 注册变量到 RimTalkPromptAPI
    // 这里用来注册隐藏变量，不会自动创建entry
    public static class RimTalkHistoryPlusApiAdapter
    {
        private const string ModId = "RimTalkHistoryPlus";
        private const string EntryName = "近期对话历史";

        static RimTalkHistoryPlusApiAdapter()
        {
            LongEventHandler.ExecuteWhenFinished(Initialize);
        }

        private static void Initialize()
        {
            try
            {
                Log.Message("[RimTalkHistoryPlus] Preparing to register API variables...");
                if (typeof(RimTalkPromptAPI) == null) return; //这个分支永远不会被执行，用于在 RimTalkPromptAPI 不存在时抛出错误

                // 1. 变量注册：将 History 注入函数注册到 RimTalk 的 Prompt API
                RimTalkPromptAPI.RegisterContextVariable(
                    ModId,
                    variableName: "HistoryPlus",
                    HistoryManager.InjectHistory,
                    description: "HistoryPlus");

                Log.Message("[RimTalkHistoryPlus] 变量注册成功");
                /*
                // 2. 更新时清理旧条目
                if (RimTalkHistoryPlusMod.Settings?.UpdateAvailable_20260126 ?? false)
                {
                    Log.Message("[RimTalkHistoryPlus] 检测到更新，清理旧条目...");
                    int remove = RimTalkPromptAPI.RemovePromptEntriesByModId(ModId);
                    Log.Message($"[RimTalkHistoryPlus] 移除{remove}条entry");
                    RimTalkHistoryPlusMod.Settings.UpdateAvailable_20260126 = false;
                    RimTalkHistoryPlusMod.Settings.Write();
                }

                // 3. 创建 PromptEntry 对象
                // 参数说明见下方详解
                var newEntry = RimTalkPromptAPI.CreatePromptEntry(
                    name: EntryName,
                    content: "---\n\n# Chat History\n{{HistoryPlus}}\n{{# 本条目会自动添加并重置，自定义请**无效化**本条目并在其他条目中使用变量{{HistoryPlus}}",
                    role: PromptRole.System,          // System(系统指令), User(用户输入), Assistant(AI回复)
                    position: PromptPosition.Relative, // Relative(相对位置), InChat(聊天记录中)
                    sourceModId: ModId            // 标记来源，方便管理和移除
                );

                // 4. 将条目添加到当前激活的预设中
                bool success = RimTalkPromptAPI.InsertPromptEntryAfterName(newEntry, "Pawn Profiles");
                if (success)
                {
                    Log.Message($"[RimTalkHistoryPlus] Successfully injected prompt entry");
                }
                else
                {
                    Log.Warning($"[RimTalkHistoryPlus] Failed to inject prompt entry (可能已存在).");
                }
                */
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkHistoryPlus] 变量注册失败: {ex.Message}");
                return;
            }

            // 修改 rimtalk 配置项
            try 
            {
                Settings.Get().Context.ConversationHistoryCount = 0;
                Log.Message($"[RimTalkHistoryPlus] RimTalk 配置修改成功");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkHistoryPlus] RimTalk 配置修改失败: {ex.Message}");
                return;
            }

        }
    }
}
