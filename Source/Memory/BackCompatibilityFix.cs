using System;
using Verse;
using RimWorld.Planet;

namespace RimTalk.Memory
{
    /// <summary>
    /// 向后兼容性修复 - 确保WorldComponent类型在游戏启动时注册
    /// v3.3.2.5+: 修复旧存档加载"Could not find class"错误
    /// ⚠️ v3.4.7: 简化实现，只强制触发类型初始化
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BackCompatibilityFix
    {
        static BackCompatibilityFix()
        {
            ForceInitialize();
        }
        
        /// <summary>
        /// ⚠️ v3.4.7: 强制初始化 - 确保类型被.NET运行时识别
        /// </summary>
        public static void ForceInitialize()
        {
            try
            {
                // ⭐ 强制触发WorldComponent子类的静态构造函数
                // 这确保RimWorld在解析存档时能找到这些类型
                
                var memoryManagerType = typeof(MemoryManager);
                var aiRequestManagerType = typeof(AI.AIRequestManager);
                
                // 触发静态初始化
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(memoryManagerType.TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(aiRequestManagerType.TypeHandle);
                
                Log.Message($"[RimTalk BackCompat] ✅ Types pre-initialized:");
                Log.Message($"  - {memoryManagerType.FullName}");
                Log.Message($"  - {aiRequestManagerType.FullName}");
                
                // ⚠️ 移除运行时验证 - 在游戏启动时 Current.Game 为 null
                // 类型注册成功就足够了，不需要验证组件实例
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk BackCompat] ❌ Initialization failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
