using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RimTalk.Patches
{
    /// <summary>
    /// Patch to inject PawnMemoryComp into all humanlike pawns
    /// </summary>
    [HarmonyPatch(typeof(ThingWithComps), "InitializeComps")]
    public static class InjectMemoryCompPatch
    {
        // 使用反射访问 AllComps 的支持字段
        private static readonly FieldInfo allCompsField = AccessTools.Field(typeof(ThingWithComps), "comps");
        
        [HarmonyPostfix]
        public static void Postfix(ThingWithComps __instance)
        {
            if (__instance is Pawn pawn && pawn.RaceProps?.Humanlike == true)
            {
                // Check if comp already exists
                if (pawn.GetComp<Memory.PawnMemoryComp>() == null)
                {
                    // Add the comp
                    var comp = new Memory.PawnMemoryComp();
                    comp.parent = pawn;
                    
                    // 使用反射访问内部的 comps 字段
                    var compsList = allCompsField?.GetValue(pawn) as List<ThingComp>;
                    if (compsList == null)
                    {
                        compsList = new List<ThingComp>();
                        allCompsField?.SetValue(pawn, compsList);
                    }
                    
                    compsList.Add(comp);
                    comp.Initialize(new Memory.CompProperties_PawnMemory());
                }
            }
        }
    }
}
