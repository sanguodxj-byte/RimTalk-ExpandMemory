using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// DEPRECATED: This patch has been replaced by EnhancedRimTalkIntegration
    /// Kept for reference but disabled to prevent conflicts
    /// </summary>
    // [StaticConstructorOnStartup]  // DISABLED - causes Sequence contains no elements error
    public static class RimTalkIntegrationPatch
    {
        // This integration method is deprecated
        // EnhancedRimTalkIntegration now handles all RimTalk conversation capture
        // using postfix patches that don't interfere with RimTalk's execution
        
        /*
        static RimTalkIntegrationPatch()
        {
            // DISABLED - This was causing errors in RimTalk.Service.TalkService.ConsumeTalk
            // The prefix injection was interfering with normal execution
            // EnhancedRimTalkIntegration uses postfix patches instead
        }
        */
    }
}
