using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Polly;
using Polly.Timeout;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CommerceFinanceAreaClient(HttpClient httpClient) : ICommerceFinanceAreaClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CommerceFinanceAreaResult> GetFinanceAreaAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/finance-area");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var area = await response.Content.ReadFromJsonAsync<FinanceAreaResponse>(JsonOptions, cancellationToken);
            if (area is null || area.Status != "reserved")
            {
                throw new JsonException("Commerce returned an invalid finance area response.");
            }

            return new CommerceFinanceAreaResult(StatusCodes.Status200OK, null, area);
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new CommerceFinanceAreaResult(
                (int)response.StatusCode,
                await ReadCodeAsync(response, cancellationToken),
                null);
        }

        response.EnsureSuccessStatusCode();
        return new CommerceFinanceAreaResult((int)response.StatusCode, null, null);
    }

    public Task<FinanceOrdersResult<CommerceFinanceOrderDetail>> GetOrderAsync(string accessToken, Guid orderId, CancellationToken cancellationToken)
        => ReadOrderAsync<CommerceFinanceOrderDetail>(accessToken, $"internal/v1/finance/orders/{orderId:D}", cancellationToken);

    public Task<FinanceOrdersResult<CommerceFinanceOrderPage>> ListOrdersAsync(string accessToken, FinanceOrderFilters filters, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["status"] = filters.Status,
            ["courseId"] = filters.CourseId?.ToString("D"),
            ["studentId"] = filters.StudentId?.ToString("D"),
            ["createdFrom"] = filters.CreatedFrom?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["createdTo"] = filters.CreatedTo?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["_page"] = filters.Page.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["_size"] = filters.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return ReadOrderAsync<CommerceFinanceOrderPage>(accessToken, QueryHelpers.AddQueryString("internal/v1/finance/orders", parameters), cancellationToken);
    }

    private async Task<FinanceOrdersResult<T>> ReadOrderAsync<T>(string accessToken, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                return value is null ? new(502, "COMMERCE_UNAVAILABLE", default) : new(200, null, value);
            }
            return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                ? new((int)response.StatusCode, await ReadCodeAsync(response, cancellationToken), default)
                : new(502, "COMMERCE_UNAVAILABLE", default);
        }
        catch (HttpRequestException) { return new(502, "COMMERCE_UNAVAILABLE", default); }
        catch (JsonException) { return new(502, "COMMERCE_UNAVAILABLE", default); }
        catch (TimeoutRejectedException) { return new(504, "UPSTREAM_TIMEOUT", default); }
        catch (ExecutionRejectedException) { return new(502, "COMMERCE_UNAVAILABLE", default); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, "UPSTREAM_TIMEOUT", default); }
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("code", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
