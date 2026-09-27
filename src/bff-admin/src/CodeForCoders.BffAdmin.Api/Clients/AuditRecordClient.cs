using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class AuditRecordClient(HttpClient httpClient) : IAuditRecordClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuditRecordClientResult> SearchAsync(
        AuditRecordSearchRequestV1 request,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/audit-record-searches")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var page = await response.Content.ReadFromJsonAsync<AuditRecordPageV1>(JsonOptions, cancellationToken);
                return page is null
                    ? new AuditRecordClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null)
                    : new AuditRecordClientResult(StatusCodes.Status200OK, null, page);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "TOKEN_INVALID")
            {
                return new AuditRecordClientResult(StatusCodes.Status401Unauthorized, code, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden && code == "PERMISSION_DENIED")
            {
                return new AuditRecordClientResult(StatusCodes.Status403Forbidden, code, null);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && code == "AUDIT_FILTER_INVALID")
            {
                return new AuditRecordClientResult(StatusCodes.Status422UnprocessableEntity, code, null);
            }

            return new AuditRecordClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (HttpRequestException)
        {
            return new AuditRecordClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (JsonException)
        {
            return new AuditRecordClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (TimeoutRejectedException)
        {
            return new AuditRecordClientResult(StatusCodes.Status504GatewayTimeout, "AUDIT_UNAVAILABLE", null);
        }
        catch (ExecutionRejectedException)
        {
            return new AuditRecordClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AuditRecordClientResult(StatusCodes.Status504GatewayTimeout, "AUDIT_UNAVAILABLE", null);
        }
    }

    public async Task<AuditRecordDetailClientResult> GetAsync(
        Guid recordId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/audit-records/{recordId:D}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var detail = await response.Content.ReadFromJsonAsync<AuditRecordDetailV1>(JsonOptions, cancellationToken);
                return detail is null
                    ? new AuditRecordDetailClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null)
                    : new AuditRecordDetailClientResult(StatusCodes.Status200OK, null, detail);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "TOKEN_INVALID")
            {
                return new AuditRecordDetailClientResult(StatusCodes.Status401Unauthorized, code, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden && code == "PERMISSION_DENIED")
            {
                return new AuditRecordDetailClientResult(StatusCodes.Status403Forbidden, code, null);
            }

            if (response.StatusCode == HttpStatusCode.NotFound && code == "AUDIT_RECORD_NOT_FOUND")
            {
                return new AuditRecordDetailClientResult(StatusCodes.Status404NotFound, code, null);
            }

            return new AuditRecordDetailClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (HttpRequestException)
        {
            return new AuditRecordDetailClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (JsonException)
        {
            return new AuditRecordDetailClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (TimeoutRejectedException)
        {
            return new AuditRecordDetailClientResult(StatusCodes.Status504GatewayTimeout, "AUDIT_UNAVAILABLE", null);
        }
        catch (ExecutionRejectedException)
        {
            return new AuditRecordDetailClientResult(StatusCodes.Status502BadGateway, "AUDIT_UNAVAILABLE", null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AuditRecordDetailClientResult(StatusCodes.Status504GatewayTimeout, "AUDIT_UNAVAILABLE", null);
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
