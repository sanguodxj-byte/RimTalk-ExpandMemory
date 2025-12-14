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
                
                // ? 尝试获取原始玩家输入（优先顺序）
                string playerInput = null;
                
                // 方法1: 尝试获取 UserPrompt（原始用户输入）
                var userPromptProperty = talkRequestType.GetProperty("UserPrompt");
                if (userPromptProperty != null)
                {
                    playerInput = userPromptProperty.GetValue(talkRequest) as string;
                    if (Prefs.DevMode && !string.IsNullOrEmpty(playerInput))
                    {
                        Log.Message($"[Player Dialogue] Found UserPrompt: {playerInput.Substring(0, Math.Min(30, playerInput.Length))}...");
                    }
                }
                
                // 方法2: 如果UserPrompt不存在，尝试 OriginalPrompt
                if (string.IsNullOrEmpty(playerInput))
                {
                    var originalPromptProperty = talkRequestType.GetProperty("OriginalPrompt");
                    if (originalPromptProperty != null)
                    {
                        playerInput = originalPromptProperty.GetValue(talkRequest) as string;
                        if (Prefs.DevMode && !string.IsNullOrEmpty(playerInput))
                        {
                            Log.Message($"[Player Dialogue] Found OriginalPrompt: {playerInput.Substring(0, Math.Min(30, playerInput.Length))}...");
                        }
                    }
                }
                
                // 方法3: 如果以上都不存在，使用 Prompt 但尝试清理
                if (string.IsNullOrEmpty(playerInput))
                {
                    var promptProperty = talkRequestType.GetProperty("Prompt");
                    if (promptProperty != null)
                    {
                        string fullPrompt = promptProperty.GetValue(talkRequest) as string;
                        if (!string.IsNullOrEmpty(fullPrompt))
                        {
                            // ? 尝试从完整prompt中提取原始用户输入
                            playerInput = ExtractUserInputFromPrompt(fullPrompt);
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[Player Dialogue] Extracted from Prompt: {playerInput?.Substring(0, Math.Min(30, playerInput?.Length ?? 0))}...");
                            }
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(playerInput))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[Player Dialogue] Failed to extract player input - all methods returned empty");
                    }
                    return;
                }
                
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
        /// ? 从完整的prompt中提取原始用户输入
        /// RimTalk的prompt格式通常是：[系统指令]\n\n[记忆]\n\n[常识]\n\n用户: {原始输入}
        /// </summary>
        private static string ExtractUserInputFromPrompt(string fullPrompt)
        {
            if (string.IsNullOrEmpty(fullPrompt))
                return null;
            
            // 尝试查找 "用户:" 或 "User:" 后的内容
            string[] userMarkers = { "用户:", "User:", "玩家:", "Player:" };
            
            foreach (var marker in userMarkers)
            {
                int markerIndex = fullPrompt.LastIndexOf(marker);
                if (markerIndex >= 0)
                {
                    // 提取标记后的内容
                    string afterMarker = fullPrompt.Substring(markerIndex + marker.Length).Trim();
                    
                    // 如果后面还有其他标记（如"助手:"），只取到该标记之前
                    string[] endMarkers = { "\n助手:", "\nAssistant:", "\n角色:", "\nCharacter:" };
                    foreach (var endMarker in endMarkers)
                    {
                        int endIndex = afterMarker.IndexOf(endMarker);
                        if (endIndex > 0)
                        {
                            afterMarker = afterMarker.Substring(0, endIndex).Trim();
                            break;
                        }
                    }
                    
                    return afterMarker;
                }
            }
            
            // 如果找不到标记，尝试提取最后一段非空文本（假设是用户输入）
            var lines = fullPrompt.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                // 获取最后一行
                string lastLine = lines[lines.Length - 1].Trim();
                
                // 如果最后一行不是系统指令（通常包含特定关键词），则认为是用户输入
                if (!lastLine.Contains("记忆") && !lastLine.Contains("常识") && 
                    !lastLine.Contains("Memory") && !lastLine.Contains("Knowledge") &&
                    !lastLine.Contains("系统") && !lastLine.Contains("System"))
                {
                    return lastLine;
                }
            }
            
            // 如果都失败了，返回null
            return null;
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
            
            // ? 获取玩家称呼（默认为"玩家"）
            string playerTitle = "玩家"; // 可以从RimTalk或其他设置中读取玩家自定义称呼
            
            // ? 简洁格式：(玩家称呼) 对 角色名 说: "内容"
            string formattedContent = $"({playerTitle}) 对 {pawn.LabelShort} 说: \"{playerInput}\"";
            
            // 创建记忆条目，直接放入 ELS
            var memory = new MemoryEntry(
                content: formattedContent,
                type: MemoryType.Conversation,
                layer: MemoryLayer.EventLog,  // 直接进入 ELS
                importance: 0.8f,  // 玩家输入重要性较高
                relatedPawn: playerTitle
            );
            
            // 添加标签
            memory.AddTag("玩家对话");
            memory.AddTag("直接记录");
            memory.AddTag("重要");
            
            // 提取关键词（只从玩家输入内容中提取）
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
