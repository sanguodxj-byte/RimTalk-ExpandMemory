using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace RimTalk.Memory.Patches.Comp;

// 添加组件到拥有声连催化剂的 pawn
[HarmonyPatch(typeof(Pawn), "ExposeData")]
public static class Pawn_ExposeData_Patch
{
    // 创建全局共享的单例属性对象
    private static readonly CompProperties_PawnMemory _pawnMemoryProps = new();

    // 获取 rimtalk 声连催化剂 hediffdef。软依赖
    private static readonly HediffDef _vocalLinkDef = DefDatabase<HediffDef>.GetNamedSilentFail("VocalLinkImplant");

    // 若获取失败则跳过补丁
    [HarmonyPrepare]
    private static bool Prepare() => _vocalLinkDef is not null;

    [HarmonyPostfix]
    private static void Postfix(Pawn __instance, ref List<ThingComp> ___comps)
    {
        var scribeMode = Scribe.mode;

        // 只参与存档加载
        // 如果实例没有声连催化剂，则不干涉
        if (scribeMode is LoadSaveMode.Saving
            || (!__instance.health?.hediffSet?.HasHediff(_vocalLinkDef) ?? true))
            return;

        // 注入并初始化组件
        if (__instance.TryGetComp<PawnMemoryComp>() is not { } comp)
        {
            // 此处注入的实际为 FourLayerMemoryComp 的子类 PawnMemoryComp，属历史遗留问题，以后再处理
            comp = new PawnMemoryComp { parent = __instance };
            ___comps ??= new();
            ___comps.Add(comp);
            comp.Initialize(_pawnMemoryProps);
        }

        // 手动读档
        comp.PostExposeData();
    }
}
