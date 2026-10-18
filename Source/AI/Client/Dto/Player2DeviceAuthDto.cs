using System.Runtime.Serialization;

namespace RimTalk.Memory.AI.Client.Dto;

[DataContract]
public class Player2DeviceCodeResponse
{
    [DataMember(Name = "device_code")]
    public string DeviceCode { get; set; }

    [DataMember(Name = "user_code")]
    public string UserCode { get; set; }

    [DataMember(Name = "verification_uri")]
    public string VerificationUri { get; set; }

    [DataMember(Name = "verification_uri_complete")]
    public string VerificationUriComplete { get; set; }

    [DataMember(Name = "expires_in")]
    public int ExpiresIn { get; set; }

    [DataMember(Name = "interval")]
    public int Interval { get; set; } = 5;
}

[DataContract]
public class Player2TokenResponse
{
    [DataMember(Name = "p2Key")]
    public string P2Key { get; set; }
}
