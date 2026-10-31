using RimTalk.Memory.AI.Client.Dto;
using RimTalk.Memory.Utils;
using RimTalk.MemoryPatch;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace RimTalk.Memory.AI.Integration;

/// <summary>
/// 负责 Player2 网页端授权的独立辅助类，
/// 严格使用本模组专属 GameClientId（01a05876-8d6b-7376-92ef-0e51b45d130c），
/// 确保授权生成的 Key 与分成 100% 绑定至本模组专属账户。
/// </summary>
public static class Player2AuthHelper
{
    public const string GameClientId = "01a05876-8d6b-7376-92ef-0e51b45d130c";
    private const string DeviceNewEndpoint = "https://api.player2.game/v1/login/device/new";
    private const string DeviceTokenEndpoint = "https://api.player2.game/v1/login/device/token";
    private const string FallbackPlayer2Website = "https://player2.game";

    private static CancellationTokenSource _authCts;

    public static bool IsAuthenticating { get; private set; }

    public static string ApprovalUrl { get; private set; }

    public static void StartAuth(Action<string> onKeyReceived = null)
    {
        if (IsAuthenticating)
        {
            if (!string.IsNullOrEmpty(ApprovalUrl))
            {
                Application.OpenURL(ApprovalUrl);
            }
            return;
        }

        Cancel();
        _authCts = new CancellationTokenSource();
        IsAuthenticating = true;

        Task.Run(async () =>
        {
            try
            {
                await RunDeviceAuthFlowAsync(_authCts.Token, onKeyReceived);
            }
            catch (OperationCanceledException)
            {
                Log.Message("[RimTalk.Memory.AI] Player2 网页端授权已取消。");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk.Memory.AI] Player2 网页端授权失败: {ex.Message}");
                ShowMainThreadMessage("获取 Player2 API 密钥失败: " + ex.Message, MessageTypeDefOf.RejectInput);
            }
            finally
            {
                IsAuthenticating = false;
                ApprovalUrl = null;
            }
        });
    }

    public static void Cancel()
    {
        if (_authCts != null)
        {
            try
            {
                _authCts.Cancel();
                _authCts.Dispose();
            }
            catch { }
            _authCts = null;
        }
        IsAuthenticating = false;
        ApprovalUrl = null;
    }

    public static void ReopenApprovalPage()
    {
        try
        {
            string url = ApprovalUrl;
            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
                return;
            }
        }
        catch { }
        Application.OpenURL(FallbackPlayer2Website);
    }

    public static void OpenWebsite()
    {
        Application.OpenURL(FallbackPlayer2Website);
    }

    private static async Task RunDeviceAuthFlowAsync(CancellationToken cancellationToken, Action<string> onKeyReceived = null)
    {
        Log.Message("[RimTalk.Memory.AI] 正在请求 Player2 设备授权码 (专属 ClientId)...");

        string newReqBody = $"{{\"client_id\":\"{GameClientId}\"}}";
        var (codeStatus, codeText) = await SendPostAsync(DeviceNewEndpoint, newReqBody, 10);
        if (codeStatus != 200 || string.IsNullOrEmpty(codeText))
        {
            throw new Exception($"获取设备码失败 (HTTP {codeStatus})");
        }

        if (!JsonUtil.TryDeserializeFromJson<Player2DeviceCodeResponse>(codeText, out var deviceCodeResp, out var parseEx)
            || string.IsNullOrEmpty(deviceCodeResp?.DeviceCode)
            || string.IsNullOrEmpty(deviceCodeResp.VerificationUriComplete))
        {
            throw new Exception("解析 Player2 授权响应失败: " + (parseEx?.Message ?? "响应字段不完整"));
        }

        ApprovalUrl = deviceCodeResp.VerificationUriComplete;

        ShowMainThreadMessage("浏览器已打开，请登录 Player2 并完成授权。", MessageTypeDefOf.NeutralEvent, () =>
        {
            Application.OpenURL(ApprovalUrl);
        });

        int pollIntervalSeconds = Math.Max(3, deviceCodeResp.Interval);
        int maxPollSeconds = deviceCodeResp.ExpiresIn > 0 ? deviceCodeResp.ExpiresIn : 600;
        int elapsedSeconds = 0;

        string tokenReqBody = $"{{\"client_id\":\"{GameClientId}\",\"device_code\":\"{deviceCodeResp.DeviceCode}\",\"grant_type\":\"urn:ietf:params:oauth:grant-type:device_code\"}}";

        while (!cancellationToken.IsCancellationRequested && elapsedSeconds < maxPollSeconds)
        {
            await Task.Delay(pollIntervalSeconds * 1000, cancellationToken);
            elapsedSeconds += pollIntervalSeconds;
            if (cancellationToken.IsCancellationRequested) break;

            var (tokenStatus, tokenText) = await SendPostAsync(DeviceTokenEndpoint, tokenReqBody, 10);
            if (tokenStatus == 200 && !string.IsNullOrEmpty(tokenText))
            {
                if (JsonUtil.TryDeserializeFromJson<Player2TokenResponse>(tokenText, out var tokenResp, out _)
                    && !string.IsNullOrEmpty(tokenResp?.P2Key))
                {
                    string key = tokenResp.P2Key;
                    Log.Message("[RimTalk.Memory.AI] Player2 专属 API Key 授权成功并获取完成！");

                    ShowMainThreadMessage("Player2 API 密钥获取并保存成功！", MessageTypeDefOf.PositiveEvent, () =>
                    {
                        onKeyReceived?.Invoke(key);
                    });
                    return;
                }
            }

            if (tokenStatus == 400)
            {
                // 等待用户在网页端授权批准
                continue;
            }

            throw new Exception($"轮询授权状态失败 (HTTP {tokenStatus})");
        }

        if (elapsedSeconds >= maxPollSeconds)
        {
            throw new Exception("授权超时，请重试。");
        }
    }

    private static async Task<(long statusCode, string text)> SendPostAsync(string url, string jsonBody, int timeoutSeconds)
    {
        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = timeoutSeconds;

        var tcs = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => tcs.TrySetResult(true);
        await tcs.Task;

        return (request.responseCode, request.downloadHandler?.text);
    }

    private static void ShowMainThreadMessage(string text, MessageTypeDef type, Action action = null)
    {
        RunOnMainThread(() =>
        {
            action?.Invoke();
            Messages.Message(text, type, historical: false);
        });
    }

    private static readonly ConcurrentQueue<Action> pendingActions = new();
    private static void RunOnMainThread(Action action)
    {
        if (action == null) return;
        if (UnityData.IsInMainThread)
        {
            action();
        }
        else
        {
            pendingActions.Enqueue(action);
        }
    }
}
