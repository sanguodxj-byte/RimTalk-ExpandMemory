using HarmonyLib;
using Verse;
using System.Collections.Generic;

namespace RimTalk.Memory.Patches.Comp;

// 添加组件到类人生物
[HarmonyPatch(typeof(ThingWithComps), "InitializeComps")]
public static class ThingWithComps_InitializeComps_Patch
{
    // 创建全局共享的单例属性对象
    private static readonly CompProperties_PawnMemory _pawnMemoryProps = new();

    [HarmonyPostfix]
    private static void Postfix(ThingWithComps __instance, ref List<ThingComp> ___comps)
    {
        // 如果实例不是类人生物，或者已经有组件，则不添加
        if (__instance is not Pawn { RaceProps.Humanlike: true } || __instance.TryGetComp<PawnMemoryComp>() is not null) return;

        // 注入并初始化组件
        // 此处注入的实际为 FourLayerMemoryComp 的子类 PawnMemoryComp，属历史遗留问题，以后再处理
        var comp = new PawnMemoryComp { parent = __instance };
        ___comps ??= new();
        ___comps.Add(comp);
        comp.Initialize(_pawnMemoryProps);
    }
}
