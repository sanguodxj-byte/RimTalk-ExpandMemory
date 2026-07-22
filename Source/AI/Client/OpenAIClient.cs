using RimTalk.Memory.AI.Client.Dto;
using RimTalk.Memory.Utils;
using RimTalk.MemoryPatch;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Verse;

namespace RimTalk.Memory.AI.Client;

/// <summary>
/// OpenAI 兼容端点的 LLM 客户端。
/// 设计蓝本为 RimTalk 主模组的 <c>RimTalk.Client.OpenAI.OpenAIClient</c>,
/// 沿用其 primary constructor + FormatEndpointUrl + UnityWebRequest + inactivity-based 超时 等核心模式。
/// </summary>
public class OpenAIClient : IAIClient
{
    // 基础字段
    private readonly Uri _url;
    private readonly string _apiKey;
    private readonly string _model;

    private const string DefaultPath = "/v1/chat/completions";

    // --- 超时常量 ---
    private const float ConnectTimeoutSec = 120f;
    private const float ReadTimeoutSec = 60f;
    private const float PollIntervalSec = 0.1f;

    // --- ExpandMemory 特有的 retry 常量 ---
    private static int MaxRetrys => RetryDelayMs.Length;
    private static readonly int[] RetryDelayMs = [4000, 8000];

    public OpenAIClient(ApiConfig config)
    {
        // 如果 URL 不合法，则会向外层抛出异常
        // 正常管线中会提前校验，一般不会出现这种情况
        _url = new Uri(config.URL, UriKind.Absolute);

        // 如果只指定了基础 URL，则补充默认路径 /v1/chat/completions
        if (_url.AbsolutePath == "/")
            _url = new Uri(_url, DefaultPath);

        _apiKey = config.ApiKey;
        _model = config.CustomModelName;
    }


    // ====================================================================
    // IAIClient
    // ====================================================================

    /// <summary>
    /// 顶层入口
    /// </summary>
    public async Task<Payload> GetChatCompletionAsync(string prompt)
    {
        // 获取配置项
        bool isAILogEnabled = RimTalkMemoryPatchMod.Settings.EnableAILog;

        // 构建请求 JSON
        string jsonContent = BuildRequestJson(prompt);

        // 初始化 payload，此时其 IsValid 默认为 false
        var payload = isAILogEnabled
            ? new Payload
            {
                URL = _url.OriginalString,
                Model = _model,
                Request = jsonContent
            }
            : new Payload();

        Exception lastEx = null;

        // 内置 retry 循环
        for (int retry = 0; retry <= MaxRetrys; retry++)
        {
            // 重试准备
            if (retry > 0)
            {
                int delay = RetryDelayMs[retry - 1];
                Log.Message($"[RimTalk.Memory.AI.Client] Attempt {retry}/{MaxRetrys + 1} failed (model={_model}). Retrying in {delay} ms.");
                await Task.Delay(delay);
            }

            // 执行任务
            try
            {
                // 可能抛出多种错误
                string responseText = await SendRequestAsync(jsonContent);

                // 确定成功，尝试解析（这一步也可能失败）并存放在 Payload 中返回
                return ParseSuccessResponse(responseText, payload);
            }
            catch (GameExitException)
            {
                Log.Error($"[RimTalk.Memory.AI.Client] 中途退出，请求终止");
                if (isAILogEnabled)
                    payload.ErrorMessage = "Game exited during request";
                return payload;
            }
            catch (ConnectTimeoutException ex)
            {
                Log.Warning($"[RimTalk.Memory.AI.Client] Connection timeout after {ConnectTimeoutSec} seconds");
                lastEx = ex;
            }
            catch (ReadTimeoutException ex)
            {
                Log.Warning($"[RimTalk.Memory.AI.Client] Read timeout after {ReadTimeoutSec} seconds");
                lastEx = ex;
            }
            catch (ResponseException ex)
            {
                long code = ex.StatusCode;
                string rawResponse = ex.RawResponse;

                switch (code)
                {
                    // 429 配额/限流 - 可能为临时限流，可重试
                    case 429L:
                        Log.Warning($"[RimTalk.Memory.AI.Client] Quota exceeded for model {_model}");
                        lastEx = ex;
                        break;

                    // 5xx 服务端错误 - 可重试
                    case >= 500L and < 600L:
                        Log.Warning($"[RimTalk.Memory.AI.Client] HTTP {code} error for model {_model}");
                        lastEx = ex;
                        break;

                    // 401/403 鉴权失败 - 致命，不重试
                    case 401L or 403L:
                        Log.Warning($"[RimTalk.Memory.AI.Client] Authentication failed for model {_model}");
                        if (isAILogEnabled)
                        {
                            payload.ErrorMessage = ExtractErrorMessage(rawResponse) ?? "Authentication failed";
                            payload.Response = rawResponse;
                        }
                        return payload;

                    // 其它 4xx 及以上客户端错误 - 视为致命，不重试
                    default:
                        Log.Warning($"[RimTalk.Memory.AI.Client] HTTP {code} error for model {_model}");
                        if (isAILogEnabled)
                        {
                            payload.ErrorMessage = ExtractErrorMessage(rawResponse) ?? $"HTTP {code} error";
                            payload.Response = rawResponse;
                        }
                        return payload;
                }
            }
        }

        // 正常结束循环，此时所有重试均失败，返回最后一次错误信息
        Log.Warning($"[RimTalk.Memory.AI.Client] All {MaxRetrys} attempts exhausted (model={_model}).");
        if (isAILogEnabled)
        {
            if (lastEx is ResponseException ex)
            {
                string rawResponse = ex.RawResponse;
                payload.Response = rawResponse;
                payload.ErrorMessage = ExtractErrorMessage(rawResponse)
                    ?? (ex.StatusCode == 429L ? "Quota exceeded" : $"HTTP {ex.StatusCode} error");
            }
            else
            {
                payload.ErrorMessage = lastEx switch
                {
                    ConnectTimeoutException => "Connection timeout",
                    ReadTimeoutException => "Read timeout",
                    _ => null
                };
            }
        }
        return payload;
    }

    /// <summary>
    /// 验证 API Key 和模型是否可用
    /// </summary>
    public async Task<bool> ValidateAsync()
    {
        string pingJson = BuildRequestJson("ping", maxTokens: 1);

        try
        {
            await SendRequestAsync(pingJson);
            return true;
        }
        catch (ConnectTimeoutException)
        {
            MessageUtil.MessageAndError(
                $"[RimTalk.Memory.AI.Client] ValidateAsync connection timeout after {ConnectTimeoutSec} seconds for model {_model}"
                );
        }
        catch (ReadTimeoutException)
        {
            MessageUtil.MessageAndError(
                $"[RimTalk.Memory.AI.Client] ValidateAsync read timeout after {ReadTimeoutSec} seconds for model {_model}"
                );
        }
        catch (ResponseException ex)
        {
            MessageUtil.MessageAndError(
                $"[RimTalk.Memory.AI.Client] ValidateAsync HTTP {ex.StatusCode} error for model {_model}"
                );
        }
        catch (Exception ex)
        {
            MessageUtil.MessageAndError(
                $"[RimTalk.Memory.AI.Client] ValidateAsync unexpected error for model {_model}: {ex.Message}"
                );
        }

        return false;
    }


    // ====================================================================
    // 请求构建
    // ====================================================================

    /// <summary>
    /// 构建 OpenAI 兼容 chat/completions 请求 JSON
    /// </summary>
    private string BuildRequestJson(string prompt, int? maxTokens = null) =>
         JsonUtil.SerializeToJson(new OpenAIRequest
         {
             Model = _model,
             Messages = [new() { Role = "user", Content = prompt ?? string.Empty }],
             MaxTokens = maxTokens ?? RimTalkMemoryPatchMod.Settings.SummaryMaxTokens
         });


    // ====================================================================
    // 网络核心 - 直接对齐 RimTalk OpenAIClient.SendRequestAsync
    // ====================================================================

    /// <summary>
    /// 发送请求并等待响应文本。仿 RimTalk OpenAIClient.SendRequestAsync。
    /// 差异:
    /// - 取消信号仍是 Current.Game == null,但行为是抛 AIRequestException 而非返回 null
    ///   (便于上层 retry 决策)
    /// </summary>
    private async Task<string> SendRequestAsync(string jsonContent, DownloadHandler downloadHandler = null)
    {
        // 初始化 UnityWebRequest
        // 通过 using 确保请求完成后释放资源
        using var webRequest = new UnityWebRequest(_url, "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
        webRequest.downloadHandler = downloadHandler ??= new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        // 某些 OpenAI 兼容端点可能不需要 API Key
        if (!string.IsNullOrEmpty(_apiKey))
            webRequest.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

        // 初始化栈上状态机
        bool hasStartedReceiving = false;
        float inactivityTimer = 0f;
        ulong lastBytes = 0;

        // 异步网络请求，启动！
        var asyncOp = webRequest.SendWebRequest();

        while (!asyncOp.isDone)
        {
            // 存档退出: abort 并抛错
            if (Current.Game == null)
            {
                webRequest.Abort();
                throw new GameExitException();
            }

            // 每 PollIntervalSec 秒检查一次下载进度
            await Task.Delay((int)(PollIntervalSec * 1000f));

            ulong currentBytes = webRequest.downloadedBytes;

            // 更新状态机
            inactivityTimer = currentBytes > lastBytes ? 0f : (inactivityTimer + PollIntervalSec);
            lastBytes = currentBytes;
            hasStartedReceiving = hasStartedReceiving || currentBytes > 0;

            if (!hasStartedReceiving && inactivityTimer > ConnectTimeoutSec)
            {
                // 首字响应超时
                webRequest.Abort();
                throw new ConnectTimeoutException();
            }

            if (hasStartedReceiving && inactivityTimer > ReadTimeoutSec)
            {
                // 接收中途超时
                webRequest.Abort();
                throw new ReadTimeoutException();
            }
        }

        // 网络请求完成，读取结果
        string responseText = downloadHandler.text;

        // 校验 response 是否有效
        long statusCode = webRequest.responseCode;

        if (statusCode >= 400
            || webRequest.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
            throw new ResponseException(statusCode, responseText);

        // 校验通过，返回响应文本
        return responseText;
    }


    // ====================================================================
    // 响应解析
    // ====================================================================

    /// <summary>
    /// 解析成功响应为 Payload
    /// </summary>
    private Payload ParseSuccessResponse(string responseText, Payload payload)
    {
        if (// 结果为空
            string.IsNullOrWhiteSpace(responseText)
            // 解析失败
            || !JsonUtil.TryDeserializeFromJson<OpenAIResponse>(responseText, out var openAIResponse, out _)
            // 解析成功但结果异常
            || openAIResponse.Choices is not { } choices || choices.Count == 0
            // 极特殊情况，请求有效但响应体显式包含 error 字段
            || openAIResponse?.Error is not null)
        {
            if (RimTalkMemoryPatchMod.Settings.EnableAILog)
            {
                payload.ErrorMessage = $"解析错误";
                payload.Response = responseText;
            }
            return payload;
        }

        // 解析大成功，将结果存放在 Payload 中返回
        payload.IsValid = true;
        payload.Response = choices[0]?.Message?.Content;
        payload.TokenCount = openAIResponse?.Usage?.TotalTokens;
        return payload;
    }

    private static string ExtractErrorMessage(string jsonResponse)
    {
        if (string.IsNullOrEmpty(jsonResponse)) return null;

        if (JsonUtil.TryDeserializeFromJson<ErrorResponse>(jsonResponse, out var wrapped, out _)
            && wrapped?.Error is { } wrappedError)
        {
            return FormatError(wrappedError);
        }

        if (JsonUtil.TryDeserializeFromJson<ErrorDetail>(jsonResponse, out var flatError, out _)
            && flatError is not null)
        {
            return FormatError(flatError);
        }

        return null;
    }

    private static string FormatError(ErrorDetail detail)
    {
        string msg = detail.Message;
        if (string.IsNullOrEmpty(msg)) msg = detail.Status;
        if (string.IsNullOrEmpty(msg)) msg = detail.Type;
        if (string.IsNullOrEmpty(msg)) return null;
        return !string.IsNullOrEmpty(detail.Code) ? $"[{detail.Code}] {msg}" : msg;
    }


    // ====================================================================
    // 内部错误
    // ====================================================================

    // 中途退出游戏
    private class GameExitException : Exception { }

    // 连接超时
    private class ConnectTimeoutException : Exception { }

    // 传输间隔超时
    private class ReadTimeoutException : Exception { }

    private class ResponseException : Exception
    {
        public long StatusCode { get; }
        public string RawResponse { get; }
        public ResponseException(long statusCode, string rawResponse) : base()
        {
            StatusCode = statusCode;
            RawResponse = rawResponse;
        }
    }
}
