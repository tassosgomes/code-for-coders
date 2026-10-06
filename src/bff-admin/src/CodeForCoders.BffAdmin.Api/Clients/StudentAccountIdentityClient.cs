using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class StudentAccountIdentityClient(HttpClient httpClient, ServiceAssertionTokenFactory assertionFactory) : IStudentAccountIdentityClient
{
    public async Task<StudentAccountLookupResult> LookupAsync(Guid staffSessionId, StudentAccountLookupRequestV1 input, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/student-account-lookups") { Content = JsonContent.Create(input) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionFactory.Create("student-account:lookup"));
        message.Headers.Add("X-Staff-Session", staffSessionId.ToString("D"));
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var account = await response.Content.ReadFromJsonAsync<StudentAccountV1>(cancellationToken);
                return account is null || account.StudentId == Guid.Empty || string.IsNullOrWhiteSpace(account.Email)
                    || string.IsNullOrWhiteSpace(account.Name) || string.IsNullOrWhiteSpace(account.Status)
                    ? Unavailable() : new StudentAccountLookupResult(200, null, account);
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = document.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return ((int)response.StatusCode, code) switch
            {
                (400, "VALIDATION_ERROR") or (401, "SESSION_REQUIRED") or (401, "SERVICE_UNAUTHORIZED")
                    or (403, "PERMISSION_DENIED") or (404, "STUDENT_ACCOUNT_NOT_FOUND") => new((int)response.StatusCode, code, null),
                _ => Unavailable(),
            };
        }
        catch (HttpRequestException) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
        catch (TimeoutRejectedException) { return new(504, "UPSTREAM_TIMEOUT", null); }
        catch (ExecutionRejectedException) { return Unavailable(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, "UPSTREAM_TIMEOUT", null); }
    }

    public async Task<StudentAccountResolutionResult> ResolveAsync(Guid staffSessionId, IReadOnlyList<Guid> studentIds, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/student-account-resolutions") { Content = JsonContent.Create(new { studentIds }) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionFactory.Create("student-account:resolve"));
        message.Headers.Add("X-Staff-Session", staffSessionId.ToString("D"));
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<StudentAccountResolutionList>(cancellationToken);
                return value?.Data is null || value.Data.Count > 50 || value.Data.Any(account => account.StudentId == Guid.Empty
                    || !studentIds.Contains(account.StudentId) || string.IsNullOrWhiteSpace(account.Name)
                    || string.IsNullOrWhiteSpace(account.Email) || account.Status is not ("active" or "disabled"))
                    || value.Data.Select(account => account.StudentId).Distinct().Count() != value.Data.Count
                    ? new(502, "IDENTITY_UNAVAILABLE", null) : new(200, null, value.Data);
            }
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = document.RootElement.TryGetProperty("code", out var property) ? property.GetString() : null;
            return ((int)response.StatusCode, code) switch
            {
                (401, "SESSION_REQUIRED") or (403, "PERMISSION_DENIED") => new((int)response.StatusCode, code, null),
                _ => new(502, "IDENTITY_UNAVAILABLE", null),
            };
        }
        catch (HttpRequestException) { return new(502, "IDENTITY_UNAVAILABLE", null); }
        catch (JsonException) { return new(502, "IDENTITY_UNAVAILABLE", null); }
        catch (TimeoutRejectedException) { return new(502, "IDENTITY_UNAVAILABLE", null); }
        catch (ExecutionRejectedException) { return new(502, "IDENTITY_UNAVAILABLE", null); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(502, "IDENTITY_UNAVAILABLE", null); }
    }

    private static StudentAccountLookupResult Unavailable() => new(502, "IDENTITY_UNAVAILABLE", null);
}
