using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Settings;

namespace Hermes.Windows.AIAction;

public sealed class DeepSeekProvider : IAIActionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly AIActionConfigService _configService;
    private readonly AIActionSecretStorageService _secretStorage;
    private readonly AppLogger _logger;
    private readonly ISecretStorageService? _fallbackSecretStorage;

    public DeepSeekProvider(
        HttpClient httpClient,
        AIActionConfigService configService,
        AIActionSecretStorageService secretStorage,
        AppLogger logger,
        ISecretStorageService? fallbackSecretStorage = null)
    {
        _httpClient = httpClient;
        _configService = configService;
        _secretStorage = secretStorage;
        _logger = logger;
        _fallbackSecretStorage = fallbackSecretStorage;
    }

    public async Task<AIActionProviderResult> ChatAsync(AIActionRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AIActionProviderResult.Fail("Set an API key in AI Actions or Hermes API settings first.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(_configService.Config.BaseUrl));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(request), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return MapFailure(response.StatusCode, body);
            }

            var content = ExtractMessageContent(body);
            return string.IsNullOrWhiteSpace(content)
                ? AIActionProviderResult.Fail("AI provider returned an empty response.")
                : AIActionProviderResult.Ok(content.Trim());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AIActionProviderResult.Fail("AI Action was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return AIActionProviderResult.Fail("AI provider request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning($"AI Action network failure. {ex.Message}");
            return AIActionProviderResult.Fail("Network unavailable or AI provider cannot be reached.");
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected AI Action provider failure.", ex);
            return AIActionProviderResult.Fail("AI Action failed. Check logs for details.");
        }
    }

    private async Task<string?> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        var apiKey = await _secretStorage.GetApiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        return _fallbackSecretStorage is null
            ? null
            : await _fallbackSecretStorage.GetApiKeyAsync(cancellationToken);
    }
    internal static object CreatePayload(AIActionRequest request)
    {
        return new
        {
            model = request.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = request.Prompt
                }
            },
            temperature = request.Temperature
        };
    }

    internal static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.deepseek.com"
            : baseUrl.Trim().TrimEnd('/');

        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed, UriKind.Absolute);
        }

        return new Uri($"{trimmed}/chat/completions", UriKind.Absolute);
    }

    internal static string? ExtractMessageContent(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }
        }

        return null;
    }

    private AIActionProviderResult MapFailure(HttpStatusCode statusCode, string body)
    {
        var providerMessage = ExtractProviderError(body);
        _logger.Warning($"AI Action provider failure: {(int)statusCode} {statusCode}. {providerMessage}");

        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                AIActionProviderResult.Fail("API key is invalid or not authorized."),
            HttpStatusCode.PaymentRequired =>
                AIActionProviderResult.Fail("AI provider quota or balance is insufficient."),
            (HttpStatusCode)429 =>
                AIActionProviderResult.Fail("AI provider rate limit reached. Try again later."),
            HttpStatusCode.BadRequest =>
                AIActionProviderResult.Fail(providerMessage ?? "AI provider rejected the request. Check model and base URL."),
            _ =>
                AIActionProviderResult.Fail(providerMessage ?? "AI provider request failed.")
        };
    }

    private static string? ExtractProviderError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }

                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }
            }
        }
        catch
        {
            return Redactor.SummarizeText(body);
        }

        return null;
    }
}

