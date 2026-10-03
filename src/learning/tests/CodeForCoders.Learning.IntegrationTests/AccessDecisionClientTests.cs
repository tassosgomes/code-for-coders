using System.Net;
using System.Security.Cryptography;
using CodeForCoders.Learning.Api.Clients;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class AccessDecisionClientTests
{
    [Theory(DisplayName = nameof(AllowedAndDeniedDecisionsAreCachedForThirtySeconds))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AllowedAndDeniedDecisionsAreCachedForThirtySeconds(bool allowed)
    {
        using var key = RSA.Create(2048); using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new LessonTestClock(); var handler = new LessonCommerceBoundaryHandler();
        handler.Decision = allowed ? handler.Decision : new("denied", null, "no-grant", null, clock.Now);
        using var http = new HttpClient(handler) { BaseAddress = new("http://commerce.test/") };
        var client = Client(http, cache, key, clock); var query = Query();
        Assert.NotNull(await client.DecideAsync(query, TestContext.Current.CancellationToken));
        var assertion = handler.Assertion;
        Assert.NotNull(await client.DecideAsync(query, TestContext.Current.CancellationToken)); Assert.Equal(1, handler.Calls);
        clock.Now = clock.Now.AddSeconds(31);
        Assert.NotNull(await client.DecideAsync(query, TestContext.Current.CancellationToken)); Assert.Equal(2, handler.Calls);
        Assert.NotEqual(assertion, handler.Assertion);
    }

    [Fact(DisplayName = nameof(CacheNeverOutlivesAccessExpiry))]
    public async Task CacheNeverOutlivesAccessExpiry()
    {
        using var key = RSA.Create(2048); using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new LessonTestClock(); var handler = new LessonCommerceBoundaryHandler { Decision = new("allowed", new("until", clock.Now.AddSeconds(10)), null, null, clock.Now) };
        using var http = new HttpClient(handler) { BaseAddress = new("http://commerce.test/") }; var client = Client(http, cache, key, clock); var query = Query();
        Assert.NotNull(await client.DecideAsync(query, TestContext.Current.CancellationToken)); clock.Now = clock.Now.AddSeconds(10);
        handler.Decision = new("denied", null, "grant-ended", clock.Now, clock.Now);
        Assert.Equal("denied", (await client.DecideAsync(query, TestContext.Current.CancellationToken))!.Decision); Assert.Equal(2, handler.Calls);
    }

    [Fact(DisplayName = nameof(UpstreamFailureIsNeverCachedOrRetried))]
    public async Task UpstreamFailureIsNeverCachedOrRetried()
    {
        using var key = RSA.Create(2048); using var cache = new MemoryCache(new MemoryCacheOptions()); var handler = new LessonCommerceBoundaryHandler { Status = HttpStatusCode.ServiceUnavailable };
        using var http = new HttpClient(handler) { BaseAddress = new("http://commerce.test/") }; var client = Client(http, cache, key, new LessonTestClock()); var query = Query();
        Assert.Null(await client.DecideAsync(query, TestContext.Current.CancellationToken)); Assert.Equal(1, handler.Calls);
        handler.Status = HttpStatusCode.OK; Assert.NotNull(await client.DecideAsync(query, TestContext.Current.CancellationToken)); Assert.Equal(2, handler.Calls);
    }

    [Fact(DisplayName = nameof(CacheSeparatesTenantStudentAndCourse))]
    public async Task CacheSeparatesTenantStudentAndCourse()
    {
        using var key = RSA.Create(2048); using var cache = new MemoryCache(new MemoryCacheOptions()); var handler = new LessonCommerceBoundaryHandler();
        using var http = new HttpClient(handler) { BaseAddress = new("http://commerce.test/") }; var client = Client(http, cache, key, new LessonTestClock()); var q = Query();
        foreach (var query in new[] { q, q with { TenantId = Guid.CreateVersion7() }, q with { StudentId = Guid.CreateVersion7() }, q with { CourseId = Guid.CreateVersion7() } })
            Assert.NotNull(await client.DecideAsync(query, TestContext.Current.CancellationToken));
        Assert.Equal(4, handler.Calls);
    }

    private static AccessDecisionQuery Query() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
    private static AccessDecisionClient Client(HttpClient http, IMemoryCache cache, RSA key, TimeProvider clock)
    {
        var options = Options.Create(new AccessDecisionOptions { SigningKeyId = "test", SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()) });
        return new(http, new AccessDecisionAssertionFactory(options, clock), cache, options, clock);
    }
}
