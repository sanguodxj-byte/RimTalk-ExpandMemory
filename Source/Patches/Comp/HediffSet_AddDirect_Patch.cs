using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace RimTalk.Memory.Patches.Comp;

// 添加组件到拥有声连催化剂的 pawn
[HarmonyPatch(typeof(HediffSet), "AddDirect")]
public static class HediffSet_AddDirect_Patch
{
    // 创建全局共享的单例属性对象
    private static readonly CompProperties_PawnMemory _pawnMemoryProps = new();

    // 获取 rimtalk 声连催化剂 hediffdef。软依赖
    private static readonly HediffDef _vocalLinkDef = DefDatabase<HediffDef>.GetNamedSilentFail("VocalLinkImplant");

    private static readonly AccessTools.FieldRef<Pawn, List<ThingComp>> _compsRef = AccessTools.FieldRefAccess<Pawn, List<ThingComp>>("comps");

    // 若获取失败则跳过补丁
    [HarmonyPrepare]
    private static bool Prepare() => _vocalLinkDef is not null;

    [HarmonyPostfix]
    private static void Postfix(HediffSet __instance, Hediff hediff)
    {
        // 只参与添加声连催化剂 hediff 的情况
        if (hediff?.def != _vocalLinkDef
            || __instance.pawn is not { } pawn
            || pawn.TryGetComp<PawnMemoryComp>() is not null)
            return;

        // 注入并初始化组件
        var comp = new PawnMemoryComp { parent = pawn };
        _compsRef(pawn) ??= new();
        _compsRef(pawn).Add(comp);
        comp.Initialize(_pawnMemoryProps);
        comp.PostSpawnSetup(false);
    }
}
