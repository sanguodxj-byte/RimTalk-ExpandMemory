using System;
using Verse;

namespace RimTalk.Memory.AI;

public class ApiConfig : IExposable
{
    public bool IsEnabled = true;
    public AIProvider Provider = AIProvider.Player2;
    public string ApiKey;
    public string CustomUrl;
    public string CustomModelName;

    public string URL => !string.IsNullOrWhiteSpace(CustomUrl) ? CustomUrl : Provider.GetEndpointUrl();

    public bool IsValid =>
        Provider is AIProvider.Player2
        || (!string.IsNullOrWhiteSpace(CustomModelName) && Uri.IsWellFormedUriString(URL, UriKind.Absolute));

    public void ExposeData()
    {
        Scribe_Values.Look(ref IsEnabled, "IsEnabled", true);
        Scribe_Values.Look(ref Provider, "Provider");
        Scribe_Values.Look(ref ApiKey, "ApiKey");
        Scribe_Values.Look(ref CustomUrl, "CustomUrl");
        Scribe_Values.Look(ref CustomModelName, "CustomModelName");
    }
}
