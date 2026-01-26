using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimTalk.Memory;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// Hook RimTalk's PromptManager.BuildMessages to capture all conversation participants
    /// 在主线程中缓存参与者信息，供异步线程使用
    /// </summary>
    [HarmonyPatch]
    public static class Patch_PromptManagerBuildMessages
    {
        /// <summary>
        /// 参与者缓存（线程安全）
        /// Key: 参与者的 LabelShort（名字）
        /// Value: 缓存的参与者信息（ThingIds + Names）
        ///
        /// 策略改变：为每个参与者都建立缓存，这样任何一个说话者都能找到完整列表
        /// </summary>
        public static readonly ConcurrentDictionary<string, CachedParticipants> ParticipantsCache = new ConcurrentDictionary<string, CachedParticipants>();
        
        // 缓存的反射信息
        private static Type _talkRequestType;
        private static PropertyInfo _initiatorProperty;
        private static bool _reflectionInitialized = false;
        
        /// <summary>
        /// 动态查找 PromptManager.BuildMessages 方法
        /// </summary>
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            try
            {
                // 查找 RimTalk 程序集
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (rimTalkAssembly == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find RimTalk assembly for BuildMessages patch!");
                    return null;
                }
                
                // 查找 PromptManager 类型
                var promptManagerType = rimTalkAssembly.GetType("RimTalk.Prompt.PromptManager");
                if (promptManagerType == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find PromptManager type!");
                    return null;
                }
                
                // 查找 BuildMessages 方法
                var buildMessagesMethod = promptManagerType.GetMethod("BuildMessages", 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (buildMessagesMethod == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find BuildMessages method!");
                    return null;
                }
                
                // 缓存 TalkRequest 类型和 Initiator 属性
                _talkRequestType = rimTalkAssembly.GetType("RimTalk.Data.TalkRequest");
                if (_talkRequestType != null)
                {
                    _initiatorProperty = _talkRequestType.GetProperty("Initiator");
                }
                _reflectionInitialized = true;
                
                Log.Message("[RimTalk Memory] ✅ Successfully targeted PromptManager.BuildMessages!");
                return buildMessagesMethod;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in TargetMethod for BuildMessages: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Prefix: 在 BuildMessages 执行前缓存参与者信息
        /// 为每个参与者都建立缓存，这样任何一个说话者都能找到完整列表
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(object talkRequest, List<Pawn> pawns, string status)
        {
            try
            {
                if (pawns == null || pawns.Count == 0)
                    return;
                
                // ⭐ v4.2: 不再在这里调用 BeginConversationContext()
                // 因为 BuildMessages 内部可能会预调用模板解析
                // 改为在 InjectABM 内部基于时间戳自动管理上下文
                
                // 在主线程中提取所有参与者信息
                var cached = new CachedParticipants
                {
                    ThingIds = pawns.Select(p => p.ThingID).ToList(),
                    Names = pawns.Select(p => p.LabelShort).ToList()
                };
                
                // ⭐ 为每个参与者都建立缓存（用名字作为 key）
                // 这样无论 AI 返回的第一个说话者是谁，都能找到完整的参与者列表
                foreach (var pawn in pawns)
                {
                    ParticipantsCache[pawn.LabelShort] = cached;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] 📋 Cached {cached.Names.Count} participants for conversation: {string.Join(", ", cached.Names)}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in BuildMessages Prefix: {ex}");
            }
        }
        
        /// <summary>
        /// 使用反射获取 talkRequest.Initiator
        /// </summary>
        private static Pawn GetInitiator(object talkRequest)
        {
            if (talkRequest == null)
                return null;
            
            try
            {
                if (_reflectionInitialized && _initiatorProperty != null)
                {
                    return _initiatorProperty.GetValue(talkRequest) as Pawn;
                }
                
                // 回退：尝试直接获取
                var prop = talkRequest.GetType().GetProperty("Initiator");
                return prop?.GetValue(talkRequest) as Pawn;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 清理过期的缓存（可选，防止内存泄漏）
        /// </summary>
        public static void CleanupCache()
        {
            // 简单实现：如果缓存超过10个条目，清空
            // 正常情况下，异步完成后会 TryRemove，所以缓存应该很小
            if (ParticipantsCache.Count > 10)
            {
                ParticipantsCache.Clear();
                if (Prefs.DevMode)
                {
                    Log.Message("[RimTalk Memory] Cleaned up participants cache");
                }
            }
        }
    }
}