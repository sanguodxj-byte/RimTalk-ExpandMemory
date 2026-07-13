using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace RimTalk.Memory.AI
{
    /// <summary>
    /// AI 请求管理器 - 启动协程
    /// </summary>
    public class AIRequestManager : WorldComponent
    {
        // 单例模式，以控制生命周期
        public static AIRequestManager Instance { get; private set; }

        public AIRequestManager(World world) : base(world)
        {
            Instance = this;
        }

        private readonly Queue<(string prompt, Action<string> callback)> _aiRequestQueue = new();

        public static void EnqueueAIRequest(string prompt, Action<string> callback)
        {
            Instance._aiRequestQueue.Enqueue((prompt, callback));
        }


        private float _lastActiveTime;
        private const float RequestInterval = 10f; // 每 10 秒发送一个请求


        public override void WorldComponentUpdate()
        {
            ProcessAIRequestQueue();
        }

        private void ProcessAIRequestQueue()
        {
            if (_aiRequestQueue.Count == 0) return;

            if (RealTime.LastRealTime - _lastActiveTime < RequestInterval) return;

            var task = _aiRequestQueue.Dequeue();

            // 提交 LLM 请求...
            ExecuteTask(task);

            _lastActiveTime = RealTime.LastRealTime;
        }
        private async void ExecuteTask((string prompt, Action<string> callback) task)
        {
            string result = await IndependentAISummarizer.CallAIAsync(task.prompt);

            // 退出存档至主界面时，会阻断一切回调（暂行）
            if (Current.Game is null) return;

            task.callback?.Invoke(result);
        }

    }
}
