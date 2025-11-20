using Verse;
using UnityEngine;
using HarmonyLib;

namespace RimTalk.MemoryPatch
{
    public class RimTalkMemoryPatchMod : Mod
    {
        public static RimTalkMemoryPatchSettings Settings;

        public RimTalkMemoryPatchMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimTalkMemoryPatchSettings>();
            var harmony = new Harmony("cj.rimtalk.expandmemory");
            harmony.PatchAll();
            Log.Message("[RimTalk-Expand Memory] Loaded successfully");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimTalk-Expand Memory";
        }
    }
}
