using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.BffAdmin.Application.Common;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Captcha;

/// <summary>
/// Typed client for the Cloudflare siteverify. There is no retry handler on purpose: the token is single-use, so a
/// retry after a timeout would be answered with "timeout-or-duplicate". The secret and the token are never logged.
/// </summary>
public sealed class TurnstileVerifier(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileVerifier> logger) : ITurnstileVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TurnstileVerification> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var activity = BffAdminTelemetry.ActivitySource.StartActivity("captcha.siteverify");
        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", settings.SecretKey),
            new("response", token),
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form.Add(new("remoteip", remoteIp));
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClient.PostAsync(settings.VerifyUrl, content, cancellationToken);
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                return Unavailable(activity, "status", null);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<SiteverifyResponse>(body, JsonOptions, cancellationToken);
            if (payload is null)
            {
                return Unavailable(activity, "empty_body", null);
            }

            if (payload.Success)
            {
                activity?.SetTag("captcha.outcome", "passed");
                return TurnstileVerification.Passed;
            }

            var codes = payload.ErrorCodes ?? [];
            activity?.SetTag("captcha.outcome", "failed");
            activity?.SetTag("captcha.error_codes", string.Join(',', codes));
            return TurnstileVerification.Failed(codes);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(activity, "timeout", exception);
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(activity, "network", exception);
        }
        catch (JsonException exception)
        {
            return Unavailable(activity, "invalid_json", exception);
        }
    }

    private TurnstileVerification Unavailable(Activity? activity, string reason, Exception? exception)
    {
        activity?.SetTag("captcha.outcome", "unavailable");
        activity?.SetTag("captcha.unavailable_reason", reason);
        logger.LogWarning("Turnstile siteverify unavailable: {Reason} {ExceptionType}", reason, exception?.GetType().Name);
        return TurnstileVerification.Unavailable;
    }

    private sealed record SiteverifyResponse(
        bool Success,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
