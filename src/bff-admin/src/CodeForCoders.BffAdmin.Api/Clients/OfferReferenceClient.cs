using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class OfferReferenceClient(HttpClient httpClient) : IOfferReferenceClient
{
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(string accessToken, IReadOnlyCollection<Guid> offerIds,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/offer-references/resolve")
        {
            Content = JsonContent.Create(new { OfferIds = offerIds })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new Dictionary<Guid, string>();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new Dictionary<Guid, string>();
            var labels = new Dictionary<Guid, string>();
            foreach (var reference in data.EnumerateArray())
            {
                if (reference.ValueKind == JsonValueKind.Object
                    && reference.TryGetProperty("offerId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var offerId)
                    && offerIds.Contains(offerId) && reference.TryGetProperty("label", out var label)
                    && label.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(label.GetString()))
                    labels[offerId] = label.GetString()!;
            }
            return labels;
        }
        catch (JsonException) { return new Dictionary<Guid, string>(); }
        catch (HttpRequestException) { return new Dictionary<Guid, string>(); }
        catch (TimeoutRejectedException) { return new Dictionary<Guid, string>(); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new Dictionary<Guid, string>(); }
    }
}
