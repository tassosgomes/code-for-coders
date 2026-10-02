using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CourtesyGrantsClient(HttpClient client) : ICourtesyGrantsClient
{
    public async Task<CourtesyGrantResponse> SendAsync(CourtesyGrantRequest input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(input.Body is null ? HttpMethod.Get : HttpMethod.Post, "internal/v1/" + input.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        if (input.Body is not null) request.Content = JsonContent.Create(input.Body.Value);
        if (input.IdempotencyKey is not null) request.Headers.Add("Idempotency-Key", input.IdempotencyKey);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unavailable(502);
            var code = root.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if ((int)response.StatusCode >= 500) return new((int)response.StatusCode == 504 ? 504 : 502,
                (int)response.StatusCode == 504 ? "UPSTREAM_TIMEOUT" : code == "STUDENT_ACCOUNT_CHECK_UNAVAILABLE" ? code : "COMMERCE_UNAVAILABLE", null);
            if (!response.IsSuccessStatusCode) return new((int)response.StatusCode, code ?? "INVALID_REQUEST", null, root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String ? detail.GetString() : null);
            if (input.Path.StartsWith("courtesy-term-preview", StringComparison.Ordinal))
            {
                if (!root.TryGetProperty("endsOn", out var end) || !DateOnly.TryParse(end.GetString(), out _)
                    || !root.TryGetProperty("expiresAt", out var expiry) || !expiry.TryGetDateTimeOffset(out _)) return Unavailable(502);
            }
            else if (!root.TryGetProperty("grantId", out var id) || !id.TryGetGuid(out _)) return Unavailable(502);
            return new((int)response.StatusCode, null, root.Clone());
        }
        catch (JsonException) { return Unavailable(502); }
        catch (HttpRequestException) { return Unavailable(502); }
        catch (InvalidOperationException) { return Unavailable(502); }
        catch (TimeoutRejectedException) { return Unavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(504); }
    }
    private static CourtesyGrantResponse Unavailable(int status) => new(status, status == 504 ? "UPSTREAM_TIMEOUT" : "COMMERCE_UNAVAILABLE", null);
}
