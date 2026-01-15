using System;

namespace RimTalk.Memory.AI
{
    /// <summary>
    /// OpenAI API 请求格式 DTO 类
    /// 兼容 OpenAI、DeepSeek、Player2 等使用 OpenAI 格式的 API
    /// </summary>
    [Serializable]
    public class OpenAIRequest
    {
        public string model;
        public OpenAIMessage[] messages;
        public float temperature;
        public int max_tokens;
        public bool enable_prompt_cache; // DeepSeek
    }
    
    /// <summary>
    /// OpenAI 消息格式
    /// </summary>
    [Serializable]
    public class OpenAIMessage
    {
        public string role;
        public string content;
        public CacheControl cache_control; // OpenAI Prompt Caching
        public bool cache; // DeepSeek cache
    }
    
    /// <summary>
    /// OpenAI 缓存控制
    /// </summary>
    [Serializable]
    public class CacheControl
    {
        public string type;
    }
}