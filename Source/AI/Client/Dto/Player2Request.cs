using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RimTalk.Memory.AI.Client.Dto;

/// <summary>
/// Player2 本地客户端的请求体。
/// 与 OpenAIRequest 的主要区别是 Player2 不需要 model 字段。
/// </summary>
[DataContract]
public sealed class Player2Request
{
    [DataMember(Name = "messages")]
    public List<Player2Message> Messages { get; set; } = new();

    [DataMember(Name = "max_tokens", EmitDefaultValue = false)]
    public int? MaxTokens { get; set; }
}

[DataContract]
public sealed class Player2Message
{
    [DataMember(Name = "role")]
    public string Role { get; set; }

    [DataMember(Name = "content")]
    public string Content { get; set; }
}
