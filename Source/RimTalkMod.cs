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
            // Use different Harmony ID to avoid conflicts with original RimTalk
            var harmony = new Harmony("cj.rimtalk.memorypatch");
            harmony.PatchAll();
            Log.Message("[RimTalk Memory Patch] Memory system enhancement loaded successfully.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimTalk_MemoryPatchSettings".Translate();
        }
    }
}
