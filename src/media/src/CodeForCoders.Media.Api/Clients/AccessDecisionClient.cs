using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Api.Clients;

public sealed class AccessDecisionClient(HttpClient client, AccessDecisionAssertionFactory assertions,
    IMemoryCache cache, IOptions<AccessDecisionOptions> options, TimeProvider clock) : IAccessDecisionClient
{
    public async Task<StudentAccessDecision?> DecideAsync(AccessDecisionQuery query, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(query, out CachedDecision? cached) && cached is not null && cached.ExpiresAt > clock.GetUtcNow()) return cached.Decision;
        return await DecideFreshAsync(query, cancellationToken);
    }

    public async Task<StudentAccessDecision?> DecideFreshAsync(AccessDecisionQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SigningKeyBase64))
        {
            MediaTelemetry.RecordDecisionFailed();
            return null;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/access-decision?studentId={query.StudentId:D}&courseId={query.CourseId:D}");
        request.Headers.Authorization = new("Bearer", assertions.Create(query.TenantId));
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            MediaTelemetry.RecordDecisionDuration(startedAt);
            if (!response.IsSuccessStatusCode)
            {
                MediaTelemetry.RecordDecisionFailed();
                return null;
            }
            var result = await response.Content.ReadFromJsonAsync<StudentAccessDecision>(cancellationToken);
            if (!IsValid(result))
            {
                MediaTelemetry.RecordDecisionFailed();
                return null;
            }
            var now = clock.GetUtcNow();
            var end = now.AddSeconds(options.Value.CacheSeconds);
            if (result!.Validity?.ExpiresAt is { } expires && expires < end) end = expires;
            if (end > now) cache.Set(query, new CachedDecision(result, end), end);
            return result;
        }
        catch (HttpRequestException)
        {
            MediaTelemetry.RecordDecisionDuration(startedAt);
            MediaTelemetry.RecordDecisionFailed();
            return null;
        }
        catch (JsonException)
        {
            MediaTelemetry.RecordDecisionDuration(startedAt);
            MediaTelemetry.RecordDecisionFailed();
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            MediaTelemetry.RecordDecisionDuration(startedAt);
            MediaTelemetry.RecordDecisionFailed();
            return null;
        }
    }

    private sealed record CachedDecision(StudentAccessDecision Decision, DateTimeOffset ExpiresAt);

    private bool IsValid(StudentAccessDecision? result)
        => result is not null && result.DecidedAt != default
            && (result.Decision == "allowed" && result.Validity is { } validity
                && (validity.Type == "lifetime" && validity.ExpiresAt is null
                    || validity.Type == "until" && validity.ExpiresAt > clock.GetUtcNow())
                || result.Decision == "denied" && result.DeniedReason is { Length: > 0 }
                    && (result.DeniedReason != "grant-ended" || result.LastExpiredAt.HasValue));
}
