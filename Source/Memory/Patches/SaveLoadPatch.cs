using HarmonyLib;
using Verse;
using RimTalk.Memory;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// 确保扩展属性能够被保存和加载
    /// 注意：此 Patch 必须保留，因为 Game.ExposeData() 是 RimWorld 核心方法，无法直接修改
    /// RemoveEntry 和 Clear 的清理逻辑已集成到 CommonKnowledgeLibrary.cs 源代码中
    /// </summary>
    [HarmonyPatch(typeof(Game), "ExposeData")]
    public static class GameExposeDataPatch
    {
        static void Postfix()
        {
            ExtendedKnowledgeEntry.ExposeData();
        }
    }
}
