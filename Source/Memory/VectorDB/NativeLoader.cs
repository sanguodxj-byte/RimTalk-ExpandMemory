using System;
using Verse;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 初始化加载器
    /// 在 Mod 初始化时触发 VectorService 初始化
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NativeLoader
    {
        static NativeLoader()
        {
            // 触发 VectorService 初始化
            // 使用 Task.Run 确保不会阻塞主线程加载过程
            System.Threading.Tasks.Task.Run(() => {
                try 
                {
                    // 访问 Instance 属性会触发单例初始化
                    var _ = VectorService.Instance;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] NativeLoader: Failed to trigger VectorService initialization: {ex}");
                }
            });
        }

        public static void Preload()
        {
            // No-op for cloud version
        }
    }
}
