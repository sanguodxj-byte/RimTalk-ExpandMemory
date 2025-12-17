using System;
using System.IO;
using System.Runtime.InteropServices;
using Verse;
using System.Linq;

namespace RimTalk.Memory.VectorDB
{
    /// <summary>
    /// 原生库加载器
    /// 在 Mod 初始化时自动加载 ONNX Runtime 原生 DLL
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NativeLoader
    {
        // Win32 API - LoadLibrary
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // Win32 API - GetLastError
        [DllImport("kernel32")]
        private static extern uint GetLastError();

        private static bool _isLoaded = false;

        static NativeLoader()
        {
            Preload();
            
            // 触发 VectorService 初始化和预热
            // 使用 Task.Run 确保不会阻塞主线程加载过程
            System.Threading.Tasks.Task.Run(() => {
                try 
                {
                    // 访问 Instance 属性会触发单例初始化，进而触发 Initialize 和 Warmup
                    var _ = VectorService.Instance;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandMemory] NativeLoader: Failed to trigger VectorService warmup: {ex}");
                }
            });
        }

        /// <summary>
        /// 显式预加载原生库
        /// </summary>
        public static void Preload()
        {
            if (_isLoaded) return;

            try
            {
                Log.Message("[RimTalk-ExpandMemory] NativeLoader: Starting native library loading...");

                // 检测操作系统
                string platform = GetPlatform();
                Log.Message($"[RimTalk-ExpandMemory] NativeLoader: Detected platform: {platform}");

                if (platform == "win-x64")
                {
                    LoadWindowsNativeLibrary();
                }
                else if (platform == "linux-x64")
                {
                    Log.Warning("[RimTalk-ExpandMemory] NativeLoader: Linux platform detected, but auto-loading not implemented yet.");
                }
                else if (platform == "osx-x64")
                {
                    Log.Warning("[RimTalk-ExpandMemory] NativeLoader: macOS platform detected, but auto-loading not implemented yet.");
                }
                else
                {
                    Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Unsupported platform: {platform}");
                }
                
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Fatal error during initialization: {ex}");
            }
        }

        /// <summary>
        /// 检测当前操作系统平台
        /// </summary>
        private static string GetPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx-x64";
            }
            else
            {
                return "unknown";
            }
        }

        /// <summary>
        /// 加载 Windows 平台的原生库
        /// </summary>
        private static void LoadWindowsNativeLibrary()
        {
            try
            {
                // 使用 PackageId 查找 Mod，更加稳健
                var currentMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageId.ToLower() == "cj.rimtalk.expandmemory");

                if (currentMod == null)
                {
                    // 尝试使用 Name 作为备选方案 (注意 About.xml 中的名称是 "RimTalk - Expand Memory")
                    currentMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.Name == "RimTalk - Expand Memory");
                }

                if (currentMod == null)
                {
                    Log.Error("[RimTalk-ExpandMemory] NativeLoader: Could not find self in mod list (cj.rimtalk.expandmemory).");
                    return;
                }

                string nativeDllPath = Path.Combine(
                    currentMod.RootDir,
                    "1.6",
                    "Native",
                    "win-x64",
                    "onnxruntime.dll"
                );

                Log.Message($"[RimTalk-ExpandMemory] NativeLoader: Attempting to load: {nativeDllPath}");

                if (!File.Exists(nativeDllPath))
                {
                    Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Native DLL not found at: {nativeDllPath}");
                    return;
                }

                IntPtr handle = LoadLibrary(nativeDllPath);

                if (handle == IntPtr.Zero)
                {
                    uint errorCode = GetLastError();
                    Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Failed to load native DLL. Win32 Error Code: {errorCode}");
                    Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Path: {nativeDllPath}");
                    
                    switch (errorCode)
                    {
                        case 126:
                            Log.Error("[RimTalk-ExpandMemory] NativeLoader: Error 126 - The specified module could not be found. Check if all dependencies are present.");
                            break;
                        case 193:
                            Log.Error("[RimTalk-ExpandMemory] NativeLoader: Error 193 - Not a valid Win32 application. Check if DLL architecture matches (x64).");
                            break;
                        default:
                            Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Unknown error. See https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes");
                            break;
                    }
                }
                else
                {
                    Log.Message($"[RimTalk-ExpandMemory] NativeLoader: Successfully loaded native DLL!");
                    Log.Message($"[RimTalk-ExpandMemory] NativeLoader: Handle: 0x{handle.ToInt64():X}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] NativeLoader: Exception during Windows native library loading: {ex}");
            }
        }
    }
}
