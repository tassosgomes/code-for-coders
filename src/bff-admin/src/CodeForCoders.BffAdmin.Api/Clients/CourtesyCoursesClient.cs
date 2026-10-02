using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Polly.Timeout;
using System.Net.Http.Json;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CourtesyCoursesClient(HttpClient httpClient) : ICourtesyCoursesClient
{
    public async Task<CatalogCoursesResult> ListAsync(CourtesyCoursesRequest input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"internal/v1/courtesy-courses?_page={input.Page}&_size={input.Size}{(input.Title is null ? "" : $"&title={Uri.EscapeDataString(input.Title)}")}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode >= 500) return Unavailable((int)response.StatusCode == 504 ? 504 : 502);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (response.IsSuccessStatusCode)
            {
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data)
                    || data.ValueKind != JsonValueKind.Array || !root.TryGetProperty("pagination", out var pagination)
                    || pagination.ValueKind != JsonValueKind.Object) return Unavailable(502);
                return new(200, null, root.Clone());
            }
            return new((int)response.StatusCode, root.ValueKind == JsonValueKind.Object && root.TryGetProperty("code", out var code) ? code.GetString() : "INVALID_REQUEST", null);
        }
        catch (JsonException) { return Unavailable(502); }
        catch (HttpRequestException) { return Unavailable(502); }
        catch (TimeoutRejectedException) { return Unavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(504); }
    }

    private static CatalogCoursesResult Unavailable(int status)
        => new(status, status == 504 ? "UPSTREAM_TIMEOUT" : "COMMERCE_UNAVAILABLE", null);
}
