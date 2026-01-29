/*
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimTalk.Memory;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// Hook RimTalk's TalkService.AddResponsesToHistory to capture complete conversations
    /// 在异步线程中捕获完整对话，入队等待主线程处理
    /// </summary>
    [HarmonyPatch]
    public static class Patch_AddResponsesToHistory
    {
        /// <summary>
        /// 待处理的对话队列（线程安全）
        /// 异步线程入队，主线程出队处理
        /// </summary>
        public static readonly ConcurrentQueue<PendingConversation> ConversationQueue = new ConcurrentQueue<PendingConversation>();
        
        // 缓存的反射信息
        private static PropertyInfo _nameProperty;
        private static PropertyInfo _textProperty;
        private static MethodInfo _getByNameMethod;
        private static PropertyInfo _pawnProperty;
        private static bool _reflectionInitialized = false;
        
        /// <summary>
        /// 动态查找 TalkService.AddResponsesToHistory 方法
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
                    Log.Warning("[RimTalk Memory] Cannot find RimTalk assembly for AddResponsesToHistory patch!");
                    return null;
                }
                
                // 查找 TalkService 类型
                var talkServiceType = rimTalkAssembly.GetType("RimTalk.Service.TalkService");
                if (talkServiceType == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find TalkService type!");
                    return null;
                }
                
                // 查找 AddResponsesToHistory 方法（private static）
                var addResponsesToHistoryMethod = talkServiceType.GetMethod("AddResponsesToHistory",
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (addResponsesToHistoryMethod == null)
                {
                    Log.Warning("[RimTalk Memory] Cannot find AddResponsesToHistory method!");
                    return null;
                }
                
                // 缓存 TalkResponse 类型的属性
                var talkResponseType = rimTalkAssembly.GetType("RimTalk.Data.TalkResponse");
                if (talkResponseType != null)
                {
                    _nameProperty = talkResponseType.GetProperty("Name");
                    _textProperty = talkResponseType.GetProperty("Text");
                }
                
                // 缓存 Cache.GetByName 方法
                var cacheType = rimTalkAssembly.GetType("RimTalk.Data.Cache");
                if (cacheType != null)
                {
                    _getByNameMethod = cacheType.GetMethod("GetByName", BindingFlags.Public | BindingFlags.Static);
                }
                
                // 缓存 PawnState.Pawn 属性
                var pawnStateType = rimTalkAssembly.GetType("RimTalk.Data.PawnState");
                if (pawnStateType != null)
                {
                    _pawnProperty = pawnStateType.GetProperty("Pawn");
                }
                
                _reflectionInitialized = true;
                
                Log.Message("[RimTalk Memory] ✅ Successfully targeted TalkService.AddResponsesToHistory!");
                return addResponsesToHistoryMethod;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in TargetMethod for AddResponsesToHistory: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Postfix: 在对话完成后捕获并入队
        /// 注意：此方法在异步线程中执行！
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(object responses, string prompt)
        {
            try
            {
                // 1. 提取原始对话行
                var rawLines = ExtractDialogueLines(responses);
                if (rawLines.Count == 0)
                    return;
                
                // 2. 用第一个说话者的名字作为 key 查找缓存
                string speakerName = rawLines[0].SpeakerName;
                
                // 3. 从缓存获取参与者信息（用名字作为 key）
                if (!Patch_PromptManagerBuildMessages.ParticipantsCache.TryRemove(speakerName, out var cached))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[RimTalk Memory] No cached participants for speaker: {speakerName}");
                    }
                    return;
                }
                
                // ⭐ 清理其他参与者的缓存（因为这轮对话已经处理了）
                foreach (var name in cached.Names)
                {
                    if (name != speakerName)
                    {
                        Patch_PromptManagerBuildMessages.ParticipantsCache.TryRemove(name, out _);
                    }
                }
                
                // 4. 获取当前游戏时间（异步安全）
                int timestamp = GetCurrentTick();
                
                // 5. 创建待处理对话并入队
                var pending = new PendingConversation
                {
                    ParticipantThingIds = cached.ThingIds,
                    ParticipantNames = cached.Names,
                    RawDialogue = rawLines,
                    Timestamp = timestamp
                };
                
                ConversationQueue.Enqueue(pending);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk Memory] 📝 Enqueued conversation: {rawLines.Count} lines, {cached.Names.Count} participants");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error in AddResponsesToHistory Postfix: {ex}");
            }
        }
        
        /// <summary>
        /// 从 responses 中提取对话行
        /// </summary>
        private static List<DialogueLine> ExtractDialogueLines(object responses)
        {
            var lines = new List<DialogueLine>();
            
            if (responses == null)
                return lines;
            
            try
            {
                var list = responses as IList;
                if (list == null || list.Count == 0)
                    return lines;
                
                foreach (var response in list)
                {
                    if (response == null)
                        continue;
                    
                    string name = null;
                    string text = null;
                    
                    // 使用缓存的反射信息
                    if (_reflectionInitialized && _nameProperty != null && _textProperty != null)
                    {
                        name = _nameProperty.GetValue(response) as string;
                        text = _textProperty.GetValue(response) as string;
                    }
                    else
                    {
                        // 回退：动态反射
                        var type = response.GetType();
                        name = type.GetProperty("Name")?.GetValue(response) as string;
                        text = type.GetProperty("Text")?.GetValue(response) as string;
                    }
                    
                    if (!string.IsNullOrEmpty(text))
                    {
                        lines.Add(new DialogueLine(name, text));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Memory] Error extracting dialogue lines: {ex}");
            }
            
            return lines;
        }
        
        /// <summary>
        /// 通过说话者名字获取其 ThingID
        /// 使用 RimTalk 的 Cache.GetByName 方法
        /// </summary>
        private static string GetInitiatorThingId(string pawnName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return null;
            
            try
            {
                // 使用缓存的反射信息
                if (_reflectionInitialized && _getByNameMethod != null && _pawnProperty != null)
                {
                    var pawnState = _getByNameMethod.Invoke(null, new object[] { pawnName });
                    if (pawnState == null)
                        return null;
                    
                    var pawn = _pawnProperty.GetValue(pawnState) as Pawn;
                    return pawn?.ThingID;
                }
                
                // 回退：动态反射
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (rimTalkAssembly == null)
                    return null;
                
                var cacheType = rimTalkAssembly.GetType("RimTalk.Data.Cache");
                var getByName = cacheType?.GetMethod("GetByName", BindingFlags.Public | BindingFlags.Static);
                
                if (getByName == null)
                    return null;
                
                var state = getByName.Invoke(null, new object[] { pawnName });
                if (state == null)
                    return null;
                
                var pawnProp = state.GetType().GetProperty("Pawn");
                var p = pawnProp?.GetValue(state) as Pawn;
                
                return p?.ThingID;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 获取当前游戏时间（异步安全）
        /// </summary>
        private static int GetCurrentTick()
        {
            try
            {
                // 注意：在异步线程中访问 Find.TickManager 可能有线程安全问题
                // 但由于只是读取一个 int 值，通常是安全的
                return Find.TickManager?.TicksGame ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
*/