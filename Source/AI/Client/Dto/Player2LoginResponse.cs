using System.Runtime.Serialization;

namespace RimTalk.Memory.AI.Client.Dto;

/// <summary>
/// Player2 本地登录接口返回的临时 API Key。
/// </summary>
[DataContract]
public sealed class Player2LoginResponse
{
    [DataMember(Name = "p2Key")]
    public string P2Key { get; set; }
}
