using System;

namespace RimTalk.Memory.AI
{
    /// <summary>
    /// Google Gemini API 请求格式 DTO 类
    /// </summary>
    [Serializable]
    public class GeminiRequest
    {
        public GeminiContent[] contents;
        public GeminiGenerationConfig generationConfig;
    }
    
    /// <summary>
    /// Gemini 内容块
    /// </summary>
    [Serializable]
    public class GeminiContent
    {
        public GeminiPart[] parts;
    }
    
    /// <summary>
    /// Gemini 内容部分
    /// </summary>
    [Serializable]
    public class GeminiPart
    {
        public string text;
    }
    
    /// <summary>
    /// Gemini 生成配置
    /// </summary>
    [Serializable]
    public class GeminiGenerationConfig
    {
        public float temperature;
        public int maxOutputTokens;
        public GeminiThinkingConfig thinkingConfig; // 可选
    }
    
    /// <summary>
    /// Gemini 思考配置（用于 flash 模型）
    /// </summary>
    [Serializable]
    public class GeminiThinkingConfig
    {
        public int thinkingBudget;
    }
}