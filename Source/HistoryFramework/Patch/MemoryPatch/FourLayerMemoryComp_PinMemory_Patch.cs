using HarmonyLib;
using RimTalk;
using RimTalk.Data;
using RimTalk.Memory;
using RimTalk.Memory.UI;
using RimTalk.Service;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static RimWorld.ColonistBar;

namespace RimTalkHistoryPlus
{
    // 这原本是一个patch，现在已接入源码，暂时先独立一个静态类，效果没问题的话再直接并入源码
    // [HarmonyPatch(typeof(FourLayerMemoryComp), "PinMemory")]
    [StaticConstructorOnStartup]
    public static class FourLayerMemoryComp_PinMemory_Patch
    {
        // 反射获取私有方法 FindMemoryById
        // static MethodInfo _findMemoryById = AccessTools.Method(typeof(FourLayerMemoryComp), "FindMemoryById");
        // static Func<FourLayerMemoryComp, string, MemoryEntry> FindMemoryById = AccessTools.MethodDelegate<Func<FourLayerMemoryComp, string, MemoryEntry>>(_findMemoryById);
        // 反射获取私有字段
        static AccessTools.FieldRef<MainTabWindow_Memory, int> LastMemoryCount;
        static AccessTools.FieldRef<MainTabWindow_Memory, bool> FiltersDirty;
        static FourLayerMemoryComp_PinMemory_Patch()
        {
            // 此时 RimTalk 的 DLL 肯定已经加载了，放心反射
            LastMemoryCount = AccessTools.FieldRefAccess<MainTabWindow_Memory, int>("lastMemoryCount");
            FiltersDirty = AccessTools.FieldRefAccess<MainTabWindow_Memory, bool>("filtersDirty");

            Log.Message("[RimTalkHistoryPlus] 反射字段初始化完成");
        }
        // 获取 Memory 窗口实例
        static MainTabWindow_Memory GetMemoryWindowInstance()
        {
            return Find.WindowStack.Windows
                .OfType<MainTabWindow_Memory>()
                .FirstOrDefault();
        }

        // [HarmonyPrefix]
        public static bool Prefix(FourLayerMemoryComp __instance, History history, string memoryId)
        {
            if (__instance == null)
            {
                Log.Error("[RimTalkHistoryPlus] 固定记忆时未找到实例");
                return true;
            }

            Log.Message("[RimTalkHistoryPlus] FourLayerMemoryComp.PinMemory: Pinning History");

            // 是 History 类型，则创建一个新的 MemoryEntry 对象复制 History
            var newMemory = new MemoryEntry(
            content: string.Empty,
            type: MemoryType.Conversation,
            layer: MemoryLayer.Situational,
            importance: 0.5f
            )
            {
                content = history.content,
                timestamp = history.timestamp,
                relatedPawnId = history.relatedPawnId,
                relatedPawnName = history.relatedPawnName,
                location = history.location,
                tags = new(history.tags ?? Enumerable.Empty<string>()),
                keywords = new(history.keywords ?? Enumerable.Empty<string>()),
                isUserEdited = true,
                isPinned = true,
                notes = history.notes,
                aiCacheKey = history.aiCacheKey,
            };
            __instance.SituationalMemories?.Add(newMemory);
            __instance.DeleteMemory(memoryId);
            Log.Message("[RimTalkHistoryPlus] FourLayerMemoryComp.PinMemory: Pinned History as MemoryEntry");

            history.isPinned = false; // 由于UI bug，这里强制回正一下
            // 刷新UI
            var memoryWindow = GetMemoryWindowInstance();
            if (memoryWindow == null) return false;
            LastMemoryCount(memoryWindow) = -1;
            FiltersDirty(memoryWindow) = true;
            Log.Message("[RimTalkHistoryPlus] FourLayerMemoryComp.PinMemory: Refreshed Memory Window UI");

            return false;
        }
    }
}
