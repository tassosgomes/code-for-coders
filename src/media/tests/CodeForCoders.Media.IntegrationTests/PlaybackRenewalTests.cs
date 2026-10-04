using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
[Trait("Layer", "Playback renewal - Integration")]
public sealed class PlaybackRenewalTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(RenewalKeepsSessionAndIssuesNewCredentialsWithoutAccumulatingTime))]
    public async Task RenewalKeepsSessionAndIssuesNewCredentialsWithoutAccumulatingTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, ct); using var client = test.Client();
        using var opened = await client.PostAsync($"/internal/v1/lessons/{test.Lesson:D}/playback-sessions", null, ct);
        var old = await opened.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = old.GetProperty("sessionId").GetGuid();
        clock.Advance(TimeSpan.FromSeconds(210));
        using var response = await Renew(client, id, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(id, body.GetProperty("sessionId").GetGuid()); Assert.Equal(test.Lesson, body.GetProperty("lessonId").GetGuid());
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), body.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(clock.GetUtcNow().AddSeconds(210), body.GetProperty("renewAfter").GetDateTimeOffset());
        Assert.NotEqual(old.GetProperty("segmentAccess").GetProperty("query").GetString(), body.GetProperty("segmentAccess").GetProperty("query").GetString());
        Assert.Equal(body.GetProperty("expiresAt").GetDateTimeOffset(), body.GetProperty("segmentAccess").GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(2, factory.PlaybackDecisionCalls[test.Tenant]); Assert.Equal(1, await test.CountSessionsAsync(ct));
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), await Expiration(test, id, ct));
    }

    [Theory(DisplayName = nameof(FailuresNeverExtendSession))]
    [InlineData("denied", 403, "ACCESS_DENIED")]
    [InlineData("ended", 403, "ACCESS_DENIED")]
    [InlineData("unavailable", 503, "ACCESS_DECISION_UNAVAILABLE")]
    [InlineData("email", 422, "WATERMARK_UNAVAILABLE")]
    public async Task FailuresNeverExtendSession(string scenario, int status, string code)
    {
        var ct = TestContext.Current.CancellationToken; var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock); var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, ct); using var opener = test.Client(); var id = await test.OpenAsync(opener, ct);
        var original = await Expiration(test, id, ct); clock.Advance(TimeSpan.FromSeconds(210));
        factory.PlaybackDecisions[test.Tenant] = scenario == "unavailable" ? 503 : scenario is "denied" or "ended" ? 403 : 200;
        if (scenario == "ended") factory.PlaybackEndedAt[test.Tenant] = clock.GetUtcNow();
        using var client = test.Client(email: scenario == "email" ? null : "student@example.com");
        using var response = await Renew(client, id, ct); Assert.Equal(status, (int)response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct); Assert.Equal(code, problem.GetProperty("code").GetString());
        if (scenario is "denied" or "ended") Assert.Equal(scenario == "ended" ? "grant-ended" : "no-grant", problem.GetProperty("reason").GetString());
        if (scenario == "ended") Assert.Equal(clock.GetUtcNow(), problem.GetProperty("accessEndedAt").GetDateTimeOffset());
        Assert.Equal(original, await Expiration(test, id, ct)); Assert.Equal(1, await test.CountSessionsAsync(ct));
    }

    [Theory(DisplayName = nameof(UnknownForeignAndExpiredSessionsNeverConsultCommerce))]
    [InlineData("missing", 404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData("student", 404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData("tenant", 404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData("expired", 410, "PLAYBACK_SESSION_EXPIRED")]
    public async Task UnknownForeignAndExpiredSessionsNeverConsultCommerce(string scenario, int status, string code)
    {
        var ct = TestContext.Current.CancellationToken; var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock); var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, ct); using var opener = test.Client(); var id = await test.OpenAsync(opener, ct);
        var original = await Expiration(test, id, ct);
        clock.Advance(TimeSpan.FromSeconds(scenario == "expired" ? 300 : 210));
        using var client = scenario == "tenant" ? host.CreateClient() : test.Client(scenario == "student" ? Guid.CreateVersion7() : null);
        if (scenario == "tenant") client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateStudentToken(Guid.CreateVersion7(), test.Student));
        using var response = await Renew(client, scenario == "missing" ? Guid.CreateVersion7() : id, ct);
        Assert.Equal(status, (int)response.StatusCode); var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(code, problem.GetProperty("code").GetString()); Assert.Equal(1, factory.PlaybackDecisionCalls[test.Tenant]);
        Assert.Equal(original, await Expiration(test, id, ct));
    }

    [Fact(DisplayName = nameof(CommerceRecoveryBeforeExpiryAllowsRenewal))]
    public async Task CommerceRecoveryBeforeExpiryAllowsRenewal()
    {
        var ct = TestContext.Current.CancellationToken; var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock); var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, ct); using var client = test.Client(); var id = await test.OpenAsync(client, ct);
        clock.Advance(TimeSpan.FromSeconds(210)); factory.PlaybackDecisions[test.Tenant] = 503;
        using var unavailable = await Renew(client, id, ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(60)); factory.PlaybackDecisions[test.Tenant] = 200;
        using var recovered = await Renew(client, id, ct); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), await Expiration(test, id, ct));
    }

    [Fact(DisplayName = nameof(SimultaneousRenewalsDoNotAddTenMinutesOrReuseCachedDecision))]
    public async Task SimultaneousRenewalsDoNotAddTenMinutesOrReuseCachedDecision()
    {
        var ct = TestContext.Current.CancellationToken; var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock); var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, ct); using var client = test.Client(); var id = await test.OpenAsync(client, ct);
        clock.Advance(TimeSpan.FromSeconds(210));
        var responses = await Task.WhenAll(Renew(client, id, ct), Renew(client, id, ct));
        foreach (var response in responses) { using (response) Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), await Expiration(test, id, ct));
        Assert.Equal(3, factory.PlaybackDecisionCalls[test.Tenant]);
        // Even an immediate repeat must repeat the authorization decision.
        factory.PlaybackDecisions[test.Tenant] = 403;
        using var denied = await Renew(client, id, ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ClockHost(AdjustableTimeProvider clock)
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
        }));
    private static Task<HttpResponseMessage> Renew(HttpClient client, Guid id, CancellationToken ct)
        => client.PostAsync($"/internal/v1/playback-sessions/{id:D}/renewals", null, ct);
    private static async Task<DateTimeOffset> Expiration(PlaybackTestContext test, Guid id, CancellationToken ct)
    {
        using var scope = test.Host.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        return await scope.ServiceProvider.GetRequiredService<MediaDbContext>().PlaybackSessions
            .Where(session => session.SessionId == id).Select(session => session.ExpiresAt).SingleAsync(ct);
    }
}
