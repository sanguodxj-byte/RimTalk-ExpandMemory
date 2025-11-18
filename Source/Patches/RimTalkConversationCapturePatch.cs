using HarmonyLib;
using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimTalk.Memory;

namespace RimTalk.MemoryPatch.Patches
{
    /// <summary>
    /// Patch RimTalk's PlayLogEntry_RimTalkInteraction to capture conversations
    /// </summary>
    [HarmonyPatch]
    public static class RimTalkConversationCapturePatch
    {
        // 缓存已处理的对话，避免重复记录
        private static HashSet<string> processedConversations = new HashSet<string>();
        private static int lastCleanupTick = 0;
        private const int CleanupInterval = 2500; // 约1小时游戏时间
        
        // 目标方法：PlayLogEntry_RimTalkInteraction的构造函数
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase TargetMethod()
        {
            // 查找 RimTalk.PlayLogEntry_RimTalkInteraction 类
            var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "RimTalk");
            
            if (rimTalkAssembly == null)
            {
                Log.Warning("[RimTalk Memory] Cannot find RimTalk assembly!");
                return null;
            }
            
            var playLogType = rimTalkAssembly.GetType("RimTalk.PlayLogEntry_RimTalkInteraction");
            if (playLogType == null)
            {
                Log.Warning("[RimTalk Memory] Cannot find PlayLogEntry_RimTalkInteraction type!");
                return null;
            }
            
            // 获取所有构造函数，找到参数最多的那个
            var constructors = playLogType.GetConstructors();
            var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            
            if (constructor != null)
            {
                Log.Message("[RimTalk Memory] ✅ Successfully targeted PlayLogEntry_RimTalkInteraction constructor!");
            }
            else
            {
                Log.Warning("[RimTalk Memory] Cannot find constructor for PlayLogEntry_RimTalkInteraction!");
            }
            
            return constructor;
        }
        
        // Postfix：在构造函数执行后捕获对话
        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                // 使用反射获取字段
                var instanceType = __instance.GetType();
                
                var cachedStringField = instanceType.GetField("_cachedString", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (cachedStringField == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find _cachedString field!");
                    return;
                }
                
                var content = cachedStringField.GetValue(__instance) as string;
                
                if (string.IsNullOrEmpty(content))
                    return;
                
                // 检查是否是回复（RimTalk的回复通常没有明确的initiator/recipient区分）
                // 我们通过检查TalkService的当前状态来判断是否是主动对话
                var initiatorField = instanceType.BaseType?.GetField("initiator", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var recipientField = instanceType.BaseType?.GetField("recipient", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (initiatorField == null || recipientField == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find initiator/recipient fields!");
                    return;
                }
                
                var initiator = initiatorField.GetValue(__instance) as Pawn;
                var recipient = recipientField.GetValue(__instance) as Pawn;
                
                // RimTalk对于自言自语或单人对话，initiator和recipient是同一个pawn
                // 我们需要解析content来确定真正的说话者
                
                if (initiator == null)
                    return;
                
                // 清理旧的缓存（防止内存泄漏）
                if (Find.TickManager != null && Find.TickManager.TicksGame - lastCleanupTick > CleanupInterval)
                {
                    processedConversations.Clear();
                    lastCleanupTick = Find.TickManager.TicksGame;
                    if (Prefs.DevMode)
                        Log.Message("[RimTalk Memory] Cleaned conversation cache");
                }
                
                // 生成唯一ID进行去重
                // 使用更宽松的去重策略：同一个tick内，同一个发起者只记录一次
                int tick = Find.TickManager?.TicksGame ?? 0;
                int contentHash = content.GetHashCode();
                string initiatorId = initiator.ThingID;
                
                // 只用 tick + initiatorId + contentHash，不管recipient
                string conversationId = $"{tick}_{initiatorId}_{contentHash}";
                
                // 去重检查
                if (processedConversations.Contains(conversationId))
                {
                    if (Prefs.DevMode)
                        Log.Message($"[RimTalk Memory] ⏭️ Skipped duplicate: {conversationId}");
                    return;
                }
                
                // 标记为已处理
                processedConversations.Add(conversationId);
                
                Log.Message($"[RimTalk Memory] 📝 Captured: {initiator.LabelShort}: {content.Substring(0, Math.Min(50, content.Length))}...");
                
                // 调用记忆API记录对话
                // 注意：recipient可能是null或者是同一个pawn
                MemoryAIIntegration.RecordConversation(initiator, recipient == initiator ? null : recipient, content);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in RimTalkConversationCapturePatch: {ex}");
            }
        }
    }
}
