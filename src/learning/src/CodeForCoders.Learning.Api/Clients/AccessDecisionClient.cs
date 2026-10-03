using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Learning.Api.Clients;

public sealed class AccessDecisionClient(HttpClient client, AccessDecisionAssertionFactory assertions,
    IMemoryCache cache, IOptions<AccessDecisionOptions> options, TimeProvider clock) : IAccessDecisionClient
{
    public async Task<StudentAccessDecision?> DecideAsync(AccessDecisionQuery query, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(query, out CachedDecision? cached) && cached is not null && cached.ExpiresAt > clock.GetUtcNow()) return cached.Decision;
        if (string.IsNullOrWhiteSpace(options.Value.SigningKeyBase64)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/access-decision?studentId={query.StudentId:D}&courseId={query.CourseId:D}");
        request.Headers.Authorization = new("Bearer", assertions.Create(query.TenantId));
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<StudentAccessDecision>(cancellationToken);
            if (!IsValid(result)) return null;
            var now = clock.GetUtcNow();
            var end = now.AddSeconds(options.Value.CacheSeconds);
            if (result!.Validity?.ExpiresAt is { } expires && expires < end) end = expires;
            if (end > now) cache.Set(query, new CachedDecision(result, end), end);
            return result;
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
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
