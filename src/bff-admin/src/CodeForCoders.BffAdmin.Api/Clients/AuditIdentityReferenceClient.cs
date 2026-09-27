using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class AuditIdentityReferenceClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IAuditIdentityReferenceClient
{
    private const string Scope = "audit-references:read";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuditIdentityReferenceClientResult> ResolveAsync(
        Guid staffSessionId,
        IReadOnlyList<AuditIdentityReferenceV1> references,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/audit-identity-reference-lookups")
        {
            Content = JsonContent.Create(new AuditIdentityReferenceLookupRequestV1(references
                .Select(reference => new AuditIdentityReferenceLookupItemV1(reference.Type, reference.Id))
                .ToArray())),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(Scope));
        message.Headers.Add("X-Staff-Session", staffSessionId.ToString("D"));

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<AuditIdentityReferenceLookupResponseV1>(JsonOptions, cancellationToken);
                return result is null
                    ? new AuditIdentityReferenceClientResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null)
                    : new AuditIdentityReferenceClientResult(StatusCodes.Status200OK, null, result.Data);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized
                && code is "SERVICE_UNAUTHORIZED" or "SESSION_REQUIRED")
            {
                return new AuditIdentityReferenceClientResult(StatusCodes.Status401Unauthorized, code, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden && code == "PERMISSION_DENIED")
            {
                return new AuditIdentityReferenceClientResult(StatusCodes.Status403Forbidden, code, null);
            }

            return new AuditIdentityReferenceClientResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (HttpRequestException)
        {
            return new AuditIdentityReferenceClientResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (JsonException)
        {
            return new AuditIdentityReferenceClientResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (TimeoutRejectedException)
        {
            return new AuditIdentityReferenceClientResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null);
        }
        catch (ExecutionRejectedException)
        {
            return new AuditIdentityReferenceClientResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AuditIdentityReferenceClientResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null);
        }
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
