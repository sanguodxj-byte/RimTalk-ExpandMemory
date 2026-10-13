using HarmonyLib;
using RimTalk.Memory.AI;

[HarmonyPatch(typeof(RimTalk.RimTalk), nameof(RimTalk.RimTalk.Reset))]
public static class RimTalk_Reset_Patch
{
    // RimTalk 软重置（即修改 API 配置时）重置记忆拓展客户端池
    private static void Postfix(bool soft)
    {
        if (soft) AIService.ResetClientPool();
    }
}
