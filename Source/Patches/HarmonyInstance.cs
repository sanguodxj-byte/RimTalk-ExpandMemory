using Verse;
using HarmonyLib;
using System.Reflection;

namespace RimTalk.Memory.Patches;

//Setting the Harmony instance
[StaticConstructorOnStartup]
public class Main
{
    static Main()
    {
        var harmony = new Harmony("cj.rimtalk.expandmemory");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}
