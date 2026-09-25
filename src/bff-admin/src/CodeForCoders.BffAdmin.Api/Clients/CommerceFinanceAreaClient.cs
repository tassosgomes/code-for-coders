using System.Net;
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
