using System.Net;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

[Collection(BffStudentIntegrationCollection.Name)]
public sealed class PurchaseIntentRateLimitTests(BffStudentIntegrationFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(AnonymousClickHasNoCsrfSessionOrVisitorHeadersAndSignsOnlyTheWriteScope))]
    public async Task AnonymousClickHasNoCsrfSessionOrVisitorHeadersAndSignsOnlyTheWriteScope()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Null(request.Content);
            Assert.Equal("opaque-click", request.Headers.GetValues("Idempotency-Key").Single());
            foreach (var header in new[] { "Cookie", "User-Agent", "X-Forwarded-For", "X-CSRF-Token" }) Assert.False(request.Headers.Contains(header));
            var parts = request.Headers.Authorization!.Parameter!.Split('.');
            using var payload = JsonDocument.Parse(Decode(parts[1]));
            Assert.Equal("purchase-intent:write", payload.RootElement.GetProperty("scope").GetString());
            Assert.Equal(ShowcaseBffFactory.TenantId, payload.RootElement.GetProperty("tenantId").GetString());
            Assert.Equal("commerce", payload.RootElement.GetProperty("aud").GetString());
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(factory.CommercePublicKey, out _);
            Assert.True(rsa.VerifyData(System.Text.Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            return Accepted();
        };
        using var client = factory.CreateClient();
        using var request = Request(Guid.CreateVersion7(), "opaque-click");
        request.Headers.Add("Cookie", "cfc.student-session=ignored-cookie");
        request.Headers.Add("User-Agent", "ignored-browser");
        request.Headers.Add("X-Forwarded-For", "192.0.2.1");
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("{\"purchaseAvailability\":\"coming-soon\"}", await response.Content.ReadAsStringAsync(Cancellation));
        Assert.DoesNotContain(response.Headers, header => header.Key == "Set-Cookie");
        Assert.Equal(0, factory.Identity.Calls);
        Assert.Single(factory.Commerce.Requests);
    }

    [Fact(DisplayName = nameof(LimitIsSharedForTheSameOfferAndIndependentForOtherOffers))]
    public async Task LimitIsSharedForTheSameOfferAndIndependentForOtherOffers()
    {
        await using var factory = new ShowcaseBffFactory(fixture) { PurchaseIntentPermitLimit = 2 };
        factory.Commerce.Respond = _ => Accepted();
        using var client = factory.CreateClient();
        var offer = Guid.CreateVersion7();
        for (var index = 0; index < 2; index++)
        {
            using var request = Request(offer, $"click-{index}");
            using var response = await client.SendAsync(request, Cancellation);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        using var deniedRequest = Request(offer, "third");
        using var denied = await client.SendAsync(deniedRequest, Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.True(denied.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains("RATE_LIMITED", await denied.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        using var anotherRequest = Request(Guid.CreateVersion7(), "another");
        using var another = await client.SendAsync(anotherRequest, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, another.StatusCode);
        Assert.Equal(3, factory.Commerce.Requests.Count);
    }

    [Fact(DisplayName = nameof(UnavailableOfferIsMappedWithoutRetry))]
    public async Task UnavailableOfferIsMappedWithoutRetry()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.NotFound, "{\"code\":\"OFFER_NOT_AVAILABLE\"}");
        using var client = factory.CreateClient();
        using var request = Request(Guid.CreateVersion7(), "unavailable");
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("OFFER_NOT_AVAILABLE", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Single(factory.Commerce.Requests);
    }

    [Fact(DisplayName = nameof(ServerFailureDoesNotRepeatThePurchaseClick))]
    public async Task ServerFailureDoesNotRepeatThePurchaseClick()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.InternalServerError, "{}");
        using var client = factory.CreateClient();
        using var request = Request(Guid.CreateVersion7(), "server-error");
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("SHOWCASE_UNAVAILABLE", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Single(factory.Commerce.Requests);
    }

    [Fact(DisplayName = nameof(HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey))]
    public async Task HttpSpansNeverRecordVisitorHeadersOrTheIdempotencyKey()
    {
        var tags = new ConcurrentQueue<KeyValuePair<string, object?>>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => { foreach (var tag in activity.TagObjects) tags.Enqueue(tag); },
        };
        ActivitySource.AddActivityListener(listener);
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => Accepted();
        using var client = factory.CreateClient();
        using var request = Request(Guid.CreateVersion7(), "private-idempotency-key");
        request.Headers.Add("User-Agent", "private-browser-sentinel");
        request.Headers.Add("Cookie", "visitor=private-cookie-sentinel");
        request.Headers.Add("X-Forwarded-For", "192.0.2.100");
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotEmpty(tags);
        var serialized = string.Join(';', tags);
        foreach (var value in new[] { "private-idempotency-key", "private-browser-sentinel", "private-cookie-sentinel", "192.0.2.100" })
            Assert.DoesNotContain(value, serialized, StringComparison.Ordinal);
    }

    private static HttpResponseMessage Accepted() => CommerceBoundaryHandler.Json(HttpStatusCode.Accepted, "{\"purchaseAvailability\":\"coming-soon\"}");
    private static HttpRequestMessage Request(Guid offer, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/showcase/offers/{offer}/purchase-intents");
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }
    private static byte[] Decode(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
}
