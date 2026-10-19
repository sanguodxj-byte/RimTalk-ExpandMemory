using System;
using System.Collections.Generic;

namespace RimTalk.Memory.AI;

public enum AIProvider
{
    Google,
    OpenAI,
    DeepSeek,
    Grok,
    GLM,
    GLMCoding,
    AlibabaIntl,
    AlibabaCN,
    Player2,
    Custom,
    // 新成员只允许追加到末尾：旧存档按既有序数值反序列化，插入会错位
    OpenRouter,
    Claude,
    Moonshot,
    OpenCodeGo
}

public static class AIProviderRegistry
{
    private static readonly Dictionary<AIProvider, string> _dictProviderToURL = new()
    {
        { AIProvider.Google, "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" },
        { AIProvider.OpenAI, "https://api.openai.com/v1/chat/completions" },
        { AIProvider.DeepSeek, "https://api.deepseek.com/v1/chat/completions" },
        { AIProvider.Grok, "https://api.x.ai/v1/chat/completions" },
        { AIProvider.GLM, "https://api.z.ai/api/paas/v4/chat/completions" },
        { AIProvider.GLMCoding, "https://api.z.ai/api/coding/paas/v4/chat/completions" },
        { AIProvider.AlibabaIntl, "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions" },
        { AIProvider.AlibabaCN, "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions" },
        { AIProvider.Player2, "https://api.player2.game/v1/chat/completions" },
        { AIProvider.OpenRouter, "https://openrouter.ai/api/v1/chat/completions" },
        { AIProvider.Claude, "https://api.anthropic.com/v1/chat/completions" },
        { AIProvider.Moonshot, "https://api.moonshot.ai/v1/chat/completions" },
        { AIProvider.OpenCodeGo, "https://opencode.ai/inference/go/openai/v1/chat/completions" }
    };

    // 个别端点要求的额外请求头（与 RimTalk 本体的 AIProviderRegistry 保持一致）
    private static readonly Dictionary<AIProvider, Dictionary<string, string>> _dictProviderToHeaders = new()
    {
        { AIProvider.OpenRouter, new()
        {
            { "HTTP-Referer", "https://github.com/sanguodxj-byte/RimTalk-ExpandMemory" },
            { "X-Title", "RimTalk-ExpandMemory" }
        } },
        { AIProvider.Claude, new(){ { "anthropic-version", "2023-06-01" } } },
        { AIProvider.OpenCodeGo, new(){ { "x-opencode-session", Guid.NewGuid().ToString() } } }
    };

    public static string GetEndpointUrl(this AIProvider p)
    {
        return _dictProviderToURL.TryGetValue(p, out var url) ? url : null;
    }

    public static Dictionary<string, string> GetExtraHeaders(this AIProvider p)
    {
        return _dictProviderToHeaders.TryGetValue(p, out var headers) ? headers : null;
    }
}
