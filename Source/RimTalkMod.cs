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
            
            // ⭐ v3.3.2.5: 强制预注册关键类型，确保旧存档兼容性
            Memory.BackCompatibilityFix.ForceInitialize();
            
            // ⭐ 初始化提示词规范化器
            Memory.PromptNormalizer.UpdateRules(Settings.normalizationRules);
            
            var harmony = new Harmony("cj.rimtalk.expandmemory");
            harmony.PatchAll();
            Log.Message("[RimTalk-Expand Memory] Loaded successfully");
            
            if (Prefs.DevMode)
            {
                Log.Message($"[PromptNormalizer] Initialized with {Memory.PromptNormalizer.GetActiveRuleCount()} active rules");
            }
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

        public override void WriteSettings()
        {
            base.WriteSettings();
            
            // 重新加载提示词规范化规则
            Memory.PromptNormalizer.UpdateRules(Settings.normalizationRules);
        }
    }
}
