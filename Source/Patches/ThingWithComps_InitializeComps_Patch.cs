using HarmonyLib;
using Verse;
using System.Collections.Generic;

namespace RimTalk.Memory.Patches
{

    // 添加组件到类人生物
    [HarmonyPatch(typeof(ThingWithComps), "InitializeComps")]
    public static class ThingWithComps_InitializeComps_Patch
    {
        // 创建全局共享的单例属性对象
        private static readonly CompProperties_PawnMemory _pawnMemoryProps = new CompProperties_PawnMemory();

        [HarmonyPostfix]
        public static void Postfix(ThingWithComps __instance, ref List<ThingComp> ___comps)
        {
            // 如果实例不是类人生物，则不添加
            if (__instance is not Pawn pawn || (!pawn.RaceProps?.Humanlike ?? true)) return;

            // 此处注入的实际为 FourLayerMemoryComp 的子类 PawnMemoryComp，属历史遗留问题，以后再处理
            if (pawn.GetComp<PawnMemoryComp>() is null)
            {
                var comp = new PawnMemoryComp();
                comp.parent = __instance;

                ___comps ??= new();
                ___comps.Add(comp);
                comp.Initialize(_pawnMemoryProps);
            }
        }
    }

}
