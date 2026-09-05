using RimTalk.Memory.AI.Client;
using RimTalk.Memory.AI.Integration;
using RimTalk.MemoryPatch;
using System.Collections.Generic;

namespace RimTalk.Memory.AI;

/// <summary>
/// 客户端工厂
/// </summary>
public class AIClientFactory
{
    // 配置项索引，指向已尝试过的最后一个有效 ApiConfig 索引
    private int _configIndex = -1;

    // RimTalk 的 ApiConfig 列表的深拷贝
    private List<ApiConfig> _rimTalkApiConfigsCache;
    private List<ApiConfig> RimTalkApiConfigs
    {
        get
        {
            _rimTalkApiConfigsCache ??= RimTalkApiConfigGetter.GetRimTalkApiConfigs();
            return _rimTalkApiConfigsCache;
        }
    }

    // 尝试新建 client
    public bool TryGetNewClient(out IAIClient client)
    {
        client = null;

        // 获取 configs
        var settings = RimTalkMemoryPatchMod.Settings;
        var apiConfigs = settings.UseRimTalkAIConfig ? RimTalkApiConfigs : settings.ApiConfigs;
        if (apiConfigs is null) return false;

        // 从上次尝试过的索引的下一位开始，寻找下一个有效的 ApiConfig
        for (_configIndex += 1; _configIndex < apiConfigs.Count; _configIndex++)
        {
            var apiConfig = apiConfigs[_configIndex];

            if (apiConfig is not { IsEnabled: true, IsValid: true }) continue;

            // 创建 client
            client = BuildClient(apiConfig);
            return true;
        }

        // 没有找到有效的 ApiConfig，创建失败
        return false;
    }

    // 根据不同的 provider 来决定创建不同的 client
    private IAIClient BuildClient(ApiConfig config) =>
        config.Provider is AIProvider.Player2 ? new Player2Client() : new OpenAIClient(config);
}
