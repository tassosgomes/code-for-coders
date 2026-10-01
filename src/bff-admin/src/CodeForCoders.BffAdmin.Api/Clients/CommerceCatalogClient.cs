using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Polly.Timeout;
using System.Net.Http.Json;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CommerceCatalogClient(HttpClient httpClient) : ICommerceCatalogClient
{
    public Task<CatalogCourseRecordResult> CreateOfferAsync(CatalogOfferRequest input, CancellationToken cancellationToken)
        => SendOfferAsync(input, HttpMethod.Post, $"internal/v1/catalog/courses/{input.TargetId:D}/offers", cancellationToken);

    public Task<CatalogCourseRecordResult> UpdateOfferAsync(CatalogOfferRequest input, CancellationToken cancellationToken)
        => SendOfferAsync(input, HttpMethod.Patch, $"internal/v1/catalog/offers/{input.TargetId:D}", cancellationToken);

    public Task<CatalogCourseRecordResult> DeleteOfferAsync(CatalogOfferRequest input, CancellationToken cancellationToken)
        => SendOfferAsync(input, HttpMethod.Delete, $"internal/v1/catalog/offers/{input.TargetId:D}", cancellationToken);

    private async Task<CatalogCourseRecordResult> SendOfferAsync(CatalogOfferRequest input, HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", input.IdempotencyKey);
        if (input.Body is { } body) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            if (status >= 500) return RecordUnavailable(status == 504 ? 504 : 502);
            if (method == HttpMethod.Delete && status == 204) return new(204, null, null);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return RecordUnavailable(502);
            if (response.IsSuccessStatusCode)
            {
                if (!root.TryGetProperty("offerId", out var offerId) || !offerId.TryGetGuid(out _)
                    || !root.TryGetProperty("courseId", out var courseId) || !courseId.TryGetGuid(out _)
                    || !root.TryGetProperty("priceCents", out var price) || !price.TryGetInt32(out _)
                    || !root.TryGetProperty("accessPeriod", out var period) || period.ValueKind != JsonValueKind.Object)
                    return RecordUnavailable(502);
                return new(status, null, root.Clone());
            }
            return new(status, root.TryGetProperty("code", out var code) ? code.GetString() : "INVALID_REQUEST", null,
                root.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
        }
        catch (JsonException) { return RecordUnavailable(502); }
        catch (HttpRequestException) { return RecordUnavailable(502); }
        catch (TimeoutRejectedException) { return RecordUnavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return RecordUnavailable(504); }
    }

    public Task<CatalogCourseRecordResult> GetAsync(CatalogCourseRecordRequest input, CancellationToken cancellationToken)
        => SendRecordAsync(input, HttpMethod.Get, cancellationToken);

    public Task<CatalogCourseRecordResult> UpdateAsync(CatalogCourseRecordRequest input, CancellationToken cancellationToken)
        => SendRecordAsync(input, HttpMethod.Patch, cancellationToken);

    private async Task<CatalogCourseRecordResult> SendRecordAsync(CatalogCourseRecordRequest input, HttpMethod method, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, $"internal/v1/catalog/courses/{input.CourseId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        if (input.Body is { } body) request.Content = JsonContent.Create(body);
        if (input.IdempotencyKey is { } key) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode >= 500) return RecordUnavailable((int)response.StatusCode == 504 ? 504 : 502);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return RecordUnavailable(502);
            if (response.IsSuccessStatusCode)
            {
                if (!root.TryGetProperty("courseId", out var courseId) || !courseId.TryGetGuid(out _)
                    || !root.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
                    return RecordUnavailable(502);
                return new(200, null, root.Clone());
            }
            return new((int)response.StatusCode, root.TryGetProperty("code", out var code) ? code.GetString() : "INVALID_REQUEST", null,
                root.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
        }
        catch (JsonException) { return RecordUnavailable(502); }
        catch (HttpRequestException) { return RecordUnavailable(502); }
        catch (TimeoutRejectedException) { return RecordUnavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return RecordUnavailable(504); }
    }

    private static CatalogCourseRecordResult RecordUnavailable(int status)
        => new(status, status == 504 ? "COMMERCE_TIMEOUT" : "COMMERCE_UNAVAILABLE", null);

    public async Task<CatalogCoursesResult> ListAsync(CatalogCoursesRequest input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"internal/v1/catalog/courses?_page={input.Page}&_size={input.Size}");
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
            return new((int)response.StatusCode, root.TryGetProperty("code", out var code) ? code.GetString() : "INVALID_REQUEST", null);
        }
        catch (JsonException) { return Unavailable(502); }
        catch (HttpRequestException) { return Unavailable(502); }
        catch (TimeoutRejectedException) { return Unavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(504); }
    }

    private static CatalogCoursesResult Unavailable(int status)
        => new(status, status == 504 ? "COMMERCE_TIMEOUT" : "COMMERCE_UNAVAILABLE", null);
}
