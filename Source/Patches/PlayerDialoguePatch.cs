using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;
using RimTalk.Memory;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// 捕获玩家对角色说的话，直接存入ELS记忆
    /// Captures player dialogue to characters and stores directly into ELS memory
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PlayerDialoguePatch
    {
        // 缓存已处理的对话，避免重复记录
        private static HashSet<string> processedDialogues = new HashSet<string>();
        private static int lastCleanupTick = 0;
        private const int CleanupInterval = 2500; // 约1小时游戏时间

        static PlayerDialoguePatch()
        {
            try
            {
                var harmony = new Harmony("rimtalk.memory.playerdialogue");
                
                // 查找 RimTalk 程序集
                Assembly rimTalkAssembly = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "RimTalk")
                    {
                        rimTalkAssembly = assembly;
                        break;
                    }
                }
                
                if (rimTalkAssembly == null)
                {
                    Log.Warning("[Player Dialogue Patch] RimTalk not found");
                    return;
                }
                
                // 尝试补丁 TalkService.GenerateTalk 来捕获玩家输入
                bool patched = PatchGenerateTalk(harmony, rimTalkAssembly);
                
                if (patched)
                {
                    Log.Message("[Player Dialogue Patch] Successfully patched GenerateTalk for player input capture");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Player Dialogue Patch] Failed to initialize: {ex}");
            }
        }
        
        private static bool PatchGenerateTalk(Harmony harmony, Assembly assembly)
        {
            try
            {
                var talkServiceType = assembly.GetType("RimTalk.Service.TalkService");
                if (talkServiceType == null) return false;
                
                var generateTalkMethod = talkServiceType.GetMethod("GenerateTalk", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (generateTalkMethod == null) return false;
                
                var postfixMethod = typeof(PlayerDialoguePatch).GetMethod(
                    nameof(GenerateTalk_Postfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                harmony.Patch(generateTalkMethod, postfix: new HarmonyMethod(postfixMethod));
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[Player Dialogue Patch] Failed to patch GenerateTalk: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Postfix 在 GenerateTalk 后捕获玩家输入
        /// </summary>
        private static void GenerateTalk_Postfix(object talkRequest)
        {
            try
            {
                if (talkRequest == null)
                    return;
                
                var talkRequestType = talkRequest.GetType();
                
                // 获取 TalkType（判断是否是玩家对话）
                var talkTypeProperty = talkRequestType.GetProperty("TalkType");
                if (talkTypeProperty == null)
                    return;
                
                var talkTypeValue = talkTypeProperty.GetValue(talkRequest);
                if (talkTypeValue == null)
                    return;
                
                // 检查是否是 User 或 Other 类型（玩家输入）
                string talkTypeName = talkTypeValue.ToString();
                bool isPlayerInput = talkTypeName == "User" || talkTypeName == "Other";
                
                if (!isPlayerInput)
                    return;
                
                // 获取 Initiator (接收玩家对话的角色)
                var initiatorProperty = talkRequestType.GetProperty("Initiator");
                if (initiatorProperty == null)
                    return;
                
                Pawn targetPawn = initiatorProperty.GetValue(talkRequest) as Pawn;
                if (targetPawn == null)
                    return;
                
                // 获取 Prompt (玩家输入的内容)
                var promptProperty = talkRequestType.GetProperty("Prompt");
                if (promptProperty == null)
                    return;
                
                string playerInput = promptProperty.GetValue(talkRequest) as string;
                if (string.IsNullOrEmpty(playerInput))
                    return;
                
                // 清理旧缓存
                if (Find.TickManager != null && Find.TickManager.TicksGame - lastCleanupTick > CleanupInterval)
                {
                    processedDialogues.Clear();
                    lastCleanupTick = Find.TickManager.TicksGame;
                }
                
                // 生成唯一ID进行去重
                int tick = Find.TickManager?.TicksGame ?? 0;
                int contentHash = playerInput.GetHashCode();
                string pawnId = targetPawn.ThingID;
                string dialogueId = $"{tick}_{pawnId}_{contentHash}";
                
                if (processedDialogues.Contains(dialogueId))
                {
                    return;
                }
                
                processedDialogues.Add(dialogueId);
                
                // 直接存入 ELS 记忆（不经过 ABM->SCM 流程）
                RecordPlayerDialogueToELS(targetPawn, playerInput);
                
                Log.Message($"[Player Dialogue] ? Recorded player input to {targetPawn.LabelShort}: {playerInput.Substring(0, Math.Min(50, playerInput.Length))}...");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Player Dialogue Patch] Error in GenerateTalk_Postfix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 将玩家对话直接记录到ELS层
        /// </summary>
        private static void RecordPlayerDialogueToELS(Pawn pawn, string playerInput)
        {
            if (pawn == null || string.IsNullOrEmpty(playerInput))
                return;
            
            var memoryComp = pawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null)
                return;
            
            // 创建记忆条目，直接放入 ELS
            var memory = new MemoryEntry(
                content: $"玩家对我说: {playerInput}",
                type: MemoryType.Conversation,
                layer: MemoryLayer.EventLog,  // 直接进入 ELS
                importance: 0.8f,  // 玩家输入重要性较高
                relatedPawn: "玩家"
            );
            
            // 添加标签
            memory.AddTag("玩家对话");
            memory.AddTag("直接记录");
            memory.AddTag("重要");
            
            // 提取关键词
            ExtractKeywords(memory, playerInput);
            
            // 直接插入到 ELS 层（跳过 ABM 和 SCM）
            memoryComp.EventLogMemories.Insert(0, memory);
            
            // ? 使用反射调用私有的 TrimEventLog 方法来管理ELS容量
            // 这样可以统一使用FourLayerMemoryComp中的25%归档逻辑
            var trimMethod = typeof(FourLayerMemoryComp).GetMethod("TrimEventLog", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (trimMethod != null)
            {
                trimMethod.Invoke(memoryComp, null);
            }
        }
        
        /// <summary>
        /// 提取关键词
        /// </summary>
        private static void ExtractKeywords(MemoryEntry memory, string content)
        {
            if (string.IsNullOrEmpty(content))
                return;

            var words = content
                .Split(new[] { ' ', '，', '。', '、', '；', '：', '-', '×', '?', '？', '!', '！' }, 
                       StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1)
                .Distinct()
                .Take(15);

            foreach (var word in words)
            {
                memory.AddKeyword(word);
            }
        }
    }
}
