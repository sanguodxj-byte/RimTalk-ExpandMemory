using System;
using Verse;
using RimWorld.Planet;

namespace RimTalk.Memory
{
    /// <summary>
    /// 向后兼容性修复 - 确保WorldComponent类型在游戏启动时注册
    /// v3.3.2.5+: 修复旧存档加载"Could not find class"错误
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BackCompatibilityFix
    {
        static BackCompatibilityFix()
        {
            ForceInitialize();
        }
        
        /// <summary>
        /// ? 强制初始化 - 由Mod构造函数调用
        /// </summary>
        public static void ForceInitialize()
        {
            try
            {
                // ? 强制触发WorldComponent子类的静态构造函数
                // 这确保RimWorld在解析存档时能找到这些类型
                
                var memoryManagerType = typeof(MemoryManager);
                var aiRequestManagerType = typeof(AI.AIRequestManager);
                var mainTabWindowType = typeof(UI.MainTabWindow_Memory);
                
                // 触发静态初始化
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(memoryManagerType.TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(aiRequestManagerType.TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(mainTabWindowType.TypeHandle);
                
                Log.Message($"[RimTalk BackCompat] ? Types pre-initialized:");
                Log.Message($"  - {memoryManagerType.FullName}");
                Log.Message($"  - {aiRequestManagerType.FullName}");
                Log.Message($"  - {mainTabWindowType.FullName}");
                
                // ? 验证类型可以被反射查找
                var world = Current.Game?.World;
                if (world != null)
                {
                    // 尝试获取组件，如果不存在会自动创建
                    var memoryManager = world.GetComponent<MemoryManager>();
                    var aiRequestManager = world.GetComponent<AI.AIRequestManager>();
                    
                    if (memoryManager != null && aiRequestManager != null)
                    {
                        Log.Message($"[RimTalk BackCompat] ? All WorldComponents successfully registered");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk BackCompat] ? Initialization failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
