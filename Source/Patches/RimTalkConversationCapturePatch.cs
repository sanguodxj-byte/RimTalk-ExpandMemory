using HarmonyLib;
using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimTalk.Memory;
using RimWorld;

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
        public static System.Reflection.MethodBase TargetMethod()
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
            
            // 直接查找特定签名的构造函数
            var constructor = playLogType.GetConstructor(new Type[] {
                typeof(InteractionDef),
                typeof(Pawn),
                typeof(Pawn),
                typeof(List<RulePackDef>)
            });
            
            if (constructor != null)
            {
                Log.Message("[RimTalk Memory] ✅ Successfully targeted PlayLogEntry_RimTalkInteraction constructor!");
            }
            else
            {
                // 如果找不到特定签名的构造函数，尝试回退到查找参数最多的构造函数
                Log.Warning("[RimTalk Memory] Cannot find exact constructor for PlayLogEntry_RimTalkInteraction, falling back to parameter count.");
                var constructors = playLogType.GetConstructors();
                constructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            }
            
            return constructor;
        }
        
        // Postfix：在构造函数执行后捕获对话
        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            try
            {
                // 使用反射获取字段
                var instanceType = __instance.GetType();
                
                // _cachedString 是 private 的
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
                
                // 尝试通过 Property 获取 Initiator 和 Recipient (Public properties)
                var initiatorProp = instanceType.GetProperty("Initiator");
                var recipientProp = instanceType.GetProperty("Recipient");

                Pawn initiator = null;
                Pawn recipient = null;

                if (initiatorProp != null && recipientProp != null)
                {
                    initiator = initiatorProp.GetValue(__instance) as Pawn;
                    recipient = recipientProp.GetValue(__instance) as Pawn;
                }
                else
                {
                    // Fallback to fields if properties are missing (unlikely based on source)
                     var initiatorField = instanceType.BaseType?.GetField("initiator", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var recipientField = instanceType.BaseType?.GetField("recipient", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (initiatorField != null) initiator = initiatorField.GetValue(__instance) as Pawn;
                    if (recipientField != null) recipient = recipientField.GetValue(__instance) as Pawn;
                }
                
                if (initiator == null)
                {
                    // Log.Warning("[RimTalk Memory] Cannot resolve initiator!"); // Reduce noise
                    return;
                }
                
                // 清理旧的缓存（防止内存泄漏）
                if (Find.TickManager != null && Find.TickManager.TicksGame - lastCleanupTick > CleanupInterval)
                {
                    processedConversations.Clear();
                    lastCleanupTick = Find.TickManager.TicksGame;
                    if (Prefs.DevMode)
                        Log.Message("[RimTalk Memory] Cleaned conversation cache");
                }
                
                // 生成唯一ID进行去重
                // 改进的去重策略：包含双方参与者信息
                int tick = Find.TickManager?.TicksGame ?? 0;
                int contentHash = content.GetHashCode();
                string initiatorId = initiator.ThingID;
                string recipientId = recipient != null ? recipient.ThingID : "null";
                
                // 包含双方参与者的完整信息，避免重复记录
                string conversationId = $"{tick}_{initiatorId}_{recipientId}_{contentHash}";
                
                // 去重检查
                if (processedConversations.Contains(conversationId))
                {
                    return;
                }
                
                // 标记为已处理
                processedConversations.Add(conversationId);
                
                string recipientLabel = recipient != null && recipient != initiator ? recipient.LabelShort : "self";
                Log.Message($"[RimTalk Memory] 📝 Captured: {initiator.LabelShort} -> {recipientLabel}: {content.Substring(0, Math.Min(50, content.Length))}...");
                
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
