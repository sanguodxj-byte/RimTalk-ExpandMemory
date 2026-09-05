using RimTalk.Memory.AI.Client.Dto;
using RimTalk.Memory.Utils;
using RimTalk.MemoryPatch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Verse;

namespace RimTalk.Memory.AI.Client;

/// <summary>
/// Player2 客户端。
/// 与 OpenAI 共享 chat completion 的响应结构，但本地客户端需要先登录，
/// 请求还需要专用请求头，且由 Player2 自己选择模型，不发送 model 字段。
/// </summary>
public sealed class Player2Client : IAIClient
{
    // --- Player2 connection constants ---
    private const string GameClientId = "01a05876-8d6b-7376-92ef-0e51b45d130c";

    // --- Runtime connection state ---
    private string _apiKey;
    private string _baseUrl;

    public Player2Client() { }

    // ====================================================================
    // IAIClient
    // ====================================================================

    /// <summary>
    /// 获取一次性的 Player2 chat completion。
    /// </summary>
    public async Task<Payload> GetChatCompletionAsync(string prompt)
    {
        // Player2 使用 OpenAI 风格 messages，但不发送 model。
        string json = JsonUtil.SerializeToJson(new Player2Request
        {
            Messages = [new() { Role = "user", Content = prompt ?? string.Empty }],
            MaxTokens = RimTalkMemoryPatchMod.Settings.SummaryMaxTokens
        });

        // 初始化 Payload；IsValid 只有解析到有效响应后才会设为 true。
        var payload = new Payload { Model = "Player2", Request = json };

        try
        {
            // 首次请求时尝试本地登录；Player2 不接受配置中的 Key 或 URL。
            await EnsureLocalConnectionAsync();
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Player2 local app is unavailable.");

            string responseText = await SendRequestAsync(json);
            payload.URL = $"{_baseUrl}/chat/completions";

            if (!JsonUtil.TryDeserializeFromJson<OpenAIResponse>(responseText, out var response, out _)
                || response?.Choices is not { Count: > 0 }
                || response.Choices[0]?.Message?.Content is not { } content)
            {
                payload.ErrorMessage = "Player2 response could not be parsed.";
                payload.Response = responseText;
                return payload;
            }

            payload.IsValid = true;
            payload.Response = content;
            payload.TokenCount = response.Usage?.TotalTokens;
            return payload;
        }
        catch (Exception ex)
        {
            payload.URL = $"{_baseUrl}/chat/completions";
            payload.ErrorMessage = ex.Message;
            return payload;
        }
    }

    /// <summary>
    /// 使用最小 chat completion 验证 Player2 连接。
    /// </summary>
    public async Task<bool> ValidateAsync()
    {
        try
        {
            var result = await GetChatCompletionAsync("ping");
            return result.IsValid;
        }
        catch
        {
            return false;
        }
    }

    // ====================================================================
    // Connection and request transport
    // ====================================================================

    private Task<string> SendRequestAsync(string json) =>
        SendRequestToUrlAsync($"{_baseUrl}/chat/completions", json, _apiKey);

    private async Task EnsureLocalConnectionAsync()
    {
        if (!string.IsNullOrEmpty(_apiKey)) return;

        var local = await TryGetLocalConnectionAsync();
        if (local != null)
        {
            _apiKey = local.Value.Key;
            _baseUrl = local.Value.BaseUrl;
        }
    }

    private static async Task<string> SendRequestToUrlAsync(string url, string json, string apiKey)
    {
        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        request.SetRequestHeader("player2-game-key", GameClientId);
        request.timeout = 120;

        await SendWebRequestAsync(request);
        if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
        {
            string error = request.downloadHandler.text;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? request.error : error);
        }

        return request.downloadHandler.text;
    }

    // ====================================================================
    // Local endpoint discovery and authentication
    // ====================================================================

    private static async Task<(string Key, string BaseUrl)?> TryGetLocalConnectionAsync()
    {
        foreach (string baseUrl in GetCandidateBaseUrls())
        {
            try
            {
                using var health = UnityWebRequest.Get($"{baseUrl}/health");
                health.timeout = 2;
                await SendWebRequestAsync(health);
                if (health.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    continue;

                using var login = new UnityWebRequest($"{baseUrl}/login/web/{GameClientId}", "POST");
                login.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                login.downloadHandler = new DownloadHandlerBuffer();
                login.SetRequestHeader("Content-Type", "application/json");
                login.timeout = 3;
                await SendWebRequestAsync(login);
                if (login.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    continue;

                if (JsonUtil.TryDeserializeFromJson<Player2LoginResponse>(login.downloadHandler.text, out var response, out _)
                    && !string.IsNullOrWhiteSpace(response?.P2Key))
                {
                    Log.Message($"[RimTalk.Memory.AI] Player2 local app detected at {baseUrl}.");
                    return (response.P2Key, baseUrl);
                }
            }
            catch
            {
                // Try the next local endpoint.
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateBaseUrls()
    {
        var urls = new List<string>();
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "game.player2.client", "api.port");
            if (File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out int port) && port > 0)
            {
                urls.Add($"http://127.0.0.1:{port}/v1");
                urls.Add($"http://localhost:{port}/v1");
            }
        }
        catch { }

        urls.Add("http://127.0.0.1:4315/v1");
        urls.Add("http://127.0.0.1:4316/v1");
        urls.Add("http://localhost:4315/v1");
        urls.Add("http://localhost:4316/v1");
        return urls;
    }

    private static Task SendWebRequestAsync(UnityWebRequest request)
    {
        var completion = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => completion.TrySetResult(true);
        return completion.Task;
    }
}
