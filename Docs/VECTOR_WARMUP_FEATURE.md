# 向量模型预热功能 (Vector Model Warmup)

## 概述
为了解决首次使用向量服务时可能出现的卡顿问题（由于模型加载和 JIT 编译），我们引入了自动预热机制。

## 实现细节

### 1. 自动预热 (VectorService.cs)
在 `VectorService` 初始化完成后，会自动启动一个后台任务进行预热。

```csharp
private void Initialize()
{
    // ... 模型加载 ...
    
    // 自动预热
    Warmup();
}

private void Warmup()
{
    Task.Run(() =>
    {
        try
        {
            // 随便用一个空字符跑一次，强迫模型加载进内存，并完成 JIT 编译
            ComputeEmbedding("warmup");
            Log.Message("[RimTalk-ExpandMemory] VectorService: Warmup complete.");
        }
        catch (Exception ex)
        {
            Log.Warning($"[RimTalk-ExpandMemory] VectorService: Warmup failed: {ex}");
        }
    });
}
```

### 2. 启动时触发 (NativeLoader.cs)
为了确保预热在游戏启动阶段就发生，而不是等到玩家第一次使用功能时，我们在 `NativeLoader` 的静态构造函数中触发了 `VectorService` 的初始化。

```csharp
[StaticConstructorOnStartup]
public static class NativeLoader
{
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
}
```

## 效果
- **无感加载**：模型加载和预热过程完全在后台线程进行，不会延长游戏启动时间或造成界面卡顿。
- **即时响应**：当玩家第一次打开知识库或发送消息时，向量服务已经准备就绪，消除了首次调用的延迟。
