using RimTalk.Memory.AI.Client;
using RimTalk.Memory.Utils;
using RimTalk.MemoryPatch;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.AI;

/// <summary>
/// AI 服务编排层
/// </summary>
public class AIService : GameComponent
{
    // --- 静态字段 ---

    // 单例实例,持有服务队列和 client pool
    // 并负责控制它们的生命周期和 tick
    private static AIService _instance;


    // --- 实例字段 ---

    // 服务队列
    private readonly Queue<(string prompt, Action<string> callback, Action dispose)> _aiRequestQueue = new();

    // 队列限流(现实时间,10s 一发)
    private float _lastActiveTime;
    private const float RequestInterval = 10f;

    // client pool 实例，请求分发会托管给它
    private readonly ClientPool _clientPool = new();

    public AIService(Game game) : base()
    {
        _instance = this;
    }


    // --- 静态门户 ---

    // 入列
    public static void EnqueueAIRequest(string prompt, Action<string> callback, Action dispose = null) =>
        _instance?._aiRequestQueue.Enqueue((prompt, callback, dispose));

    // AI 配置项有效性校验（仅存档内：依赖 AIService GameComponent 实例）
    public static async Task<bool> ValidateAIConfigAsync() =>
        await (_instance?._clientPool.ValidateAsync() ?? Task.FromResult(false));

    // 外部调用，重置 client pool
    public static void ResetClientPool() => _instance?._clientPool.Reset();


    // --- 实例方法 ---

    // 出列
    public override void GameComponentUpdate() => ProcessAIRequestQueue();

    private void ProcessAIRequestQueue()
    {
        if (_aiRequestQueue.Count == 0) return;

        // 限流
        if (RealTime.LastRealTime - _lastActiveTime < RequestInterval) return;

        var task = _aiRequestQueue.Dequeue();
        ExecuteTask(task);

        _lastActiveTime = RealTime.LastRealTime;
    }

    // 执行 AI 请求任务
    private async void ExecuteTask((string prompt, Action<string> callback, Action dispose) task)
    {
        try
        {
            // 如果不想要 client pool 了，只需把 _clientPool 改为单个 client 实例即可
            IAIClient client = _clientPool;

            // 调用 client 获取 AI 响应
            var payLoad = await client.GetChatCompletionAsync(task.prompt);
            // --- 在这停顿（await） ---

            // payLoad 为空时，打印错误信息并返回
            if (payLoad is null)
            {
                MessageUtil.MessageAndError("[RimTalk.Memory.AI] 请求失败，Get a empty payLoad.");
                return;
            }

            // 这里可以打印 payLoad 信息，包括 Error 的 payLoad
            if (RimTalkMemoryPatchMod.Settings.EnableAILog)
                Log.Message(payLoad.ToString());

            // payLoad 无效时，打印错误信息并返回
            if (!payLoad.IsValid)
            {
                MessageUtil.MessageAndError("[RimTalk.Memory.AI] 请求失败，Get a error payLoad.");
                return;
            }

            // 所有校验通过，执行回调函数
            task.callback?.Invoke(payLoad.Response);
        }
        finally
        {
            // 执行清理函数
            task.dispose?.Invoke();
        }
    }
}
