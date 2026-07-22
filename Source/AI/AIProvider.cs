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
    Custom
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
        { AIProvider.AlibabaCN, "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions" }
    };

    public static string GetEndpointUrl(this AIProvider p)
    {
        return _dictProviderToURL.TryGetValue(p, out var url) ? url : null;
    }
}
