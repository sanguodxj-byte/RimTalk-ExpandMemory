using HarmonyLib;
using RimTalk.Prompt;

namespace RimTalk.Memory.Patches
{
    
    // 通过 patch rimtalk 在合适的时点重置去重缓存

    // patch prompt 构建
    [HarmonyPatch(typeof(PromptManager), "BuildMessagesFromPreset")]
    public static class PromptManager_BuildMessagesFromPreset_Patch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            RoundMemoryManager.ResetDuplicateCache();
        }
    }

    // patch 预设界面中的“预览”
    [HarmonyPatch(typeof(PresetPreviewGenerator), "GeneratePreview")]
    public static class PresetPreviewGenerator_GeneratePreview_Patch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            RoundMemoryManager.ResetDuplicateCache();
        }
    }
}
