using HarmonyLib;
using RimWorld;
using Verse;
using RimTalk.Memory;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using RimTalk.MemoryPatch;

namespace RimTalk.Patches
{
    // ===== 已移除的功能 =====
    
    // InteractionMemoryPatch - 已完全移除
    // 原因：
    // 1. 互动记忆只有类型标签，无具体内容，价值低
    // 2. RimTalk对话记忆已完全覆盖社交信息
    // 3. 实现复杂，易出bug（如重复记录问题）
    // 4. 不符合用户期望（用户需要的是对话内容，不是互动类型）
    
    // ThoughtMemoryPatch - 已移除
    // 原因：版本依赖性高，不可靠
    
    // InspectTabsPatch - 已移除
    // 原因：改用底部菜单按钮
    // 记忆访问现在通过 MainButtonDef 和 MainTabWindow_Memory 实现
}
