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

    [DataMember(Name = "deviceCode", EmitDefaultValue = false)]
    private string camelDeviceCode;

    [DataMember(Name = "userCode", EmitDefaultValue = false)]
    private string camelUserCode;

    [DataMember(Name = "verificationUri", EmitDefaultValue = false)]
    private string camelVerificationUri;

    [DataMember(Name = "verificationUriComplete", EmitDefaultValue = false)]
    private string camelVerificationUriComplete;

    [DataMember(Name = "expiresIn", EmitDefaultValue = false)]
    private int? camelExpiresIn;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (string.IsNullOrEmpty(DeviceCode)) DeviceCode = camelDeviceCode;
        if (string.IsNullOrEmpty(UserCode)) UserCode = camelUserCode;
        if (string.IsNullOrEmpty(VerificationUri)) VerificationUri = camelVerificationUri;
        if (string.IsNullOrEmpty(VerificationUriComplete)) VerificationUriComplete = camelVerificationUriComplete;
        if (ExpiresIn <= 0 && camelExpiresIn.HasValue) ExpiresIn = camelExpiresIn.Value;

        camelDeviceCode = null;
        camelUserCode = null;
        camelVerificationUri = null;
        camelVerificationUriComplete = null;
        camelExpiresIn = null;
    }
}

[DataContract]
public class Player2TokenResponse
{
    [DataMember(Name = "p2Key")]
    public string P2Key { get; set; }
}
