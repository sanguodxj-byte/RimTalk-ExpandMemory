using RimTalk.Memory.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;

namespace RimTalk.Memory.AI.Client;

/// <summary>
/// 全局 client 池，生命周期跟随 AIService
/// </summary>
public class ClientPool : IAIClient
{
    // 池体
    private readonly List<IAIClient> _clients = new();

    // client 工厂
    private AIClientFactory _clientFactory = new();

    /// <summary>
    /// 以客户端池的方式获取 AI 响应，
    /// 使用方式和单个客户端无异
    /// </summary>
    /// <returns>
    /// 可能返回 ErrorPayload 或 null
    /// </returns>
    public async Task<Payload> GetChatCompletionAsync(string prompt)
    {
        Payload result = null;

        // 为了避免无限循环，设置最大尝试次数
        const int maxRetries = 100;
        for (int i = 0; i < maxRetries; i++)
        {
            // _clients.Count 的唯一减少点是 Clear()，它会在 UI 修改 ApiConfigs 后被调用
            // 仅在此时可能出现 i > _clients.Count 的情况，意味着当前 task 的 fallback 链被重置了
            if (i > _clients.Count)
            {
                Log.Message($"[RimTalk.Memory.AI.Client] ClientPool 在任务中重置，将重新遍历");
                i = -1;
                continue;
            }

            // 当 i == _clients.Count 时，尝试构建新的 client 并加入池中
            // 构建失败则 return
            if (i == _clients.Count)
            {
                if (_clientFactory.TryGetNewClient(out var newClient))
                {
                    _clients.Add(newClient);
                }
                else
                {
                    Log.Error($"[RimTalk.Memory.AI.Client] 没有更多有效的 AI 客户端可用，请求失败。已尝试 {_clients.Count} 个客户端。");
                    return result;
                }
            }

            // 指挥客户端运行请求
            result = await _clients[i].GetChatCompletionAsync(prompt);

            // 结果有效，返回
            if (result?.IsValid ?? false) return result;

            // 无效则记录并继续尝试下一个客户端
            Log.Warning($"[RimTalk.Memory.AI.Client] 第 {i} 个客户端响应失败");
        }

        // 理论上不会到达这里
        Log.Error("[RimTalk.Memory.AI.Client] ClientPool 循环异常");
        return null;
    }

    // AI 配置有效性检验
    // 简化版的 GetChatCompletionAsync，返回 bool
    public async Task<bool> ValidateAsync()
    {
        const int maxRetries = 100;
        for (int i = 0; i < maxRetries; i++)
        {
            if (i > _clients.Count)
            {
                Log.Message($"[RimTalk.Memory.AI.Client] ClientPool 在任务中重置，将重新遍历");
                i = -1;
                continue;
            }

            if (i == _clients.Count)
            {
                if (_clientFactory.TryGetNewClient(out var newClient))
                {
                    _clients.Add(newClient);
                }
                else return false;
            }

            // 循环内只向各客户端下发命令，自身不 log
            // 只要有一个客户端返回 true，就认为配置有效
            if (await _clients[i].ValidateAsync()) return true;
        }

        MessageUtil.MessageAndError("[RimTalk.Memory.AI.Client] 所有配置项均验证失败");
        return false;
    }


    public void Reset()
    {
        _clients.Clear();
        _clientFactory = new();
    }
}
