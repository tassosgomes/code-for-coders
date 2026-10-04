using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
[Trait("Layer", "Playback progress - Integration")]
public sealed class PlaybackProgressTests(VideoLibraryApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(ValidProgressRecordsFactAndUpdatesSessionFields))]
    public async Task ValidProgressRecordsFactAndUpdatesSessionFields()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using var response = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);

        var ack = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(ack.GetProperty("recorded").GetBoolean());

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var session = await db.PlaybackSessions.SingleAsync(s => s.SessionId == sessionId, Cancellation);
        Assert.Equal(1, session.LastSequence);
        Assert.Equal(10, session.LastPositionSeconds);
        Assert.Equal(clock.GetUtcNow(), session.LastProgressAt);

        var expectedEventId = PlaybackSession.CreateDeterministicEventId(sessionId, 1);
        var outbox = await db.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == expectedEventId, Cancellation);
        Assert.Equal(test.Tenant, outbox.TenantId);
        Assert.Equal("midia.reproducao-avancou.v1", outbox.Type);
        Assert.Equal("midia.reproducao-avancou.v1", outbox.RoutingKey);
        Assert.NotNull(outbox.ProcessedOn); // Born retained

        using var payloadDoc = JsonDocument.Parse(outbox.Payload);
        var payload = payloadDoc.RootElement;
        Assert.Equal(expectedEventId, payload.GetProperty("eventId").GetGuid());
        Assert.Equal(test.Tenant, payload.GetProperty("tenantId").GetGuid());
        Assert.Equal(sessionId, payload.GetProperty("sessionId").GetGuid());
        Assert.Equal(test.Student, payload.GetProperty("studentId").GetGuid());
        Assert.Equal(test.Course, payload.GetProperty("courseId").GetGuid());
        Assert.Equal(test.Lesson, payload.GetProperty("lessonId").GetGuid());
        Assert.Equal(1, payload.GetProperty("sequence").GetInt32());
        Assert.Equal(10, payload.GetProperty("positionSeconds").GetInt32());
        Assert.Equal("heartbeat", payload.GetProperty("reason").GetString());
        Assert.False(payload.TryGetProperty("email", out _));
        Assert.False(payload.TryGetProperty("videoId", out _));
        Assert.False(payload.TryGetProperty("title", out _));
        Assert.False(payload.TryGetProperty("percentage", out _));
    }

    [Fact(DisplayName = nameof(RepeatedSequenceReturnsRecordedFalseWithoutCreatingNewFact))]
    public async Task RepeatedSequenceReturnsRecordedFalseWithoutCreatingNewFact()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using (var res1 = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            var ack1 = await res1.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.True(ack1.GetProperty("recorded").GetBoolean());
        }

        clock.Advance(TimeSpan.FromSeconds(15));

        using (var res2 = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 25, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            var ack2 = await res2.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.False(ack2.GetProperty("recorded").GetBoolean());
        }

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var count = await db.OutboxMessages.IgnoreQueryFilters().CountAsync(m => m.TenantId == test.Tenant, Cancellation);
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = nameof(DecreasedSequenceReturnsRecordedFalse))]
    public async Task DecreasedSequenceReturnsRecordedFalse()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using (var res1 = await SendProgress(client, sessionId, new { sequence = 3, positionSeconds = 20, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            var ack1 = await res1.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.True(ack1.GetProperty("recorded").GetBoolean());
        }

        clock.Advance(TimeSpan.FromSeconds(15));

        using (var res2 = await SendProgress(client, sessionId, new { sequence = 2, positionSeconds = 25, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            var ack2 = await res2.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.False(ack2.GetProperty("recorded").GetBoolean());
        }

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var session = await db.PlaybackSessions.SingleAsync(s => s.SessionId == sessionId, Cancellation);
        Assert.Equal(3, session.LastSequence);
        Assert.Equal(20, session.LastPositionSeconds);
    }

    [Fact(DisplayName = nameof(ProgressLessThan10SecondsSinceLastAcceptedReturnsRecordedFalse))]
    public async Task ProgressLessThan10SecondsSinceLastAcceptedReturnsRecordedFalse()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using (var res1 = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 5, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            var ack1 = await res1.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.True(ack1.GetProperty("recorded").GetBoolean());
        }

        clock.Advance(TimeSpan.FromSeconds(5)); // Only 5 seconds, minGap is 10s

        using (var res2 = await SendProgress(client, sessionId, new { sequence = 2, positionSeconds = 10, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            var ack2 = await res2.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.False(ack2.GetProperty("recorded").GetBoolean());
        }

        // Advance past 10s total since first accepted progress
        clock.Advance(TimeSpan.FromSeconds(6)); // now 11s total since sequence 1

        using (var res3 = await SendProgress(client, sessionId, new { sequence = 2, positionSeconds = 16, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res3.StatusCode);
            var ack3 = await res3.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.True(ack3.GetProperty("recorded").GetBoolean());
        }

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var count = await db.OutboxMessages.IgnoreQueryFilters().CountAsync(m => m.TenantId == test.Tenant, Cancellation);
        Assert.Equal(2, count);
    }

    [Fact(DisplayName = nameof(ProgressForUnknownSessionReturns404))]
    public async Task ProgressForUnknownSessionReturns404()
    {
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();

        using var response = await SendProgress(client, Guid.CreateVersion7(), new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PLAYBACK_SESSION_NOT_FOUND", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ProgressForSessionOfDifferentStudentReturns404))]
    public async Task ProgressForSessionOfDifferentStudentReturns404()
    {
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, Cancellation);
        using var clientOwner = test.Client();
        var sessionId = await test.OpenAsync(clientOwner, Cancellation);

        using var clientOther = test.Client(student: Guid.CreateVersion7());
        using var response = await SendProgress(clientOther, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PLAYBACK_SESSION_NOT_FOUND", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ProgressBeyond60MinutesAfterExpiryReturns410))]
    public async Task ProgressBeyond60MinutesAfterExpiryReturns410()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        // Session valid for 5 min (300 s). Advance 5 min + 61 min = 66 minutes
        clock.Advance(TimeSpan.FromMinutes(66));

        using var response = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PLAYBACK_SESSION_EXPIRED", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ProgressWithinGracePeriodAfterExpiryIsAccepted))]
    public async Task ProgressWithinGracePeriodAfterExpiryIsAccepted()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        // Advance 10 minutes: 5 minutes expired, but well within 60-minute grace window
        clock.Advance(TimeSpan.FromMinutes(10));

        using var response = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 20, reason = "paused" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ack = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(ack.GetProperty("recorded").GetBoolean());
    }

    [Fact(DisplayName = nameof(PositionBeyondVideoDurationPlus10SecondsReturns400ValidationError))]
    public async Task PositionBeyondVideoDurationPlus10SecondsReturns400ValidationError()
    {
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, Cancellation); // Video duration is 60 s, max position is 70 s
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using var response = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 71, reason = "heartbeat" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
    }

    [Theory(DisplayName = nameof(BodyWithProhibitedFieldsReturns400ValidationError))]
    [InlineData("{\"sequence\": 1, \"positionSeconds\": 10, \"reason\": \"heartbeat\", \"email\": \"student@example.com\"}")]
    [InlineData("{\"sequence\": 1, \"positionSeconds\": 10, \"reason\": \"heartbeat\", \"courseId\": \"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"sequence\": 1, \"positionSeconds\": 10, \"reason\": \"heartbeat\", \"lessonId\": \"00000000-0000-0000-0000-000000000001\"}")]
    public async Task BodyWithProhibitedFieldsReturns400ValidationError(string rawJson)
    {
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/internal/v1/playback-sessions/{sessionId:D}/progress", content, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(SessionEndedByDeniedDecisionStillAcceptsProgressAndDoesNotInvalidatePriorFacts))]
    public async Task SessionEndedByDeniedDecisionStillAcceptsProgressAndDoesNotInvalidatePriorFacts()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        // First progress accepted
        using (var res1 = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 15, reason = "heartbeat" }))
        {
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        }

        // Simulate renewal attempt that is denied
        clock.Advance(TimeSpan.FromSeconds(210));
        factory.PlaybackDecisions[test.Tenant] = 403;
        using var deniedRenewal = await client.PostAsync($"/internal/v1/playback-sessions/{sessionId:D}/renewals", null, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, deniedRenewal.StatusCode);

        // Later progress still accepted within grace period and does not invalidate prior fact
        clock.Advance(TimeSpan.FromSeconds(30));
        using (var res2 = await SendProgress(client, sessionId, new { sequence = 2, positionSeconds = 45, reason = "paused" }))
        {
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            var ack2 = await res2.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.True(ack2.GetProperty("recorded").GetBoolean());
        }

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var facts = await db.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.TenantId == test.Tenant && m.RoutingKey == "midia.reproducao-avancou.v1")
            .OrderBy(m => m.OccurredOn)
            .ToListAsync(Cancellation);

        Assert.Equal(2, facts.Count);
    }

    [Fact(DisplayName = nameof(ConcurrentProgressRequestsWithSameSequenceOneSucceedsAndOtherReturnsRecordedFalseWithout500))]
    public async Task ConcurrentProgressRequestsWithSameSequenceOneSucceedsAndOtherReturnsRecordedFalseWithout500()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client1 = test.Client();
        using var client2 = test.Client();
        var sessionId = await test.OpenAsync(client1, Cancellation);

        var task1 = SendProgress(client1, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        var task2 = SendProgress(client2, sessionId, new { sequence = 1, positionSeconds = 10, reason = "left" });

        var responses = await Task.WhenAll(task1, task2);

        foreach (var res in responses)
        {
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        var ack1 = await responses[0].Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var ack2 = await responses[1].Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var recorded1 = ack1.GetProperty("recorded").GetBoolean();
        var recorded2 = ack2.GetProperty("recorded").GetBoolean();

        Assert.True(recorded1 ^ recorded2, "Exactly one concurrent request with the same sequence must be recorded.");

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var count = await db.OutboxMessages.IgnoreQueryFilters().CountAsync(m => m.TenantId == test.Tenant, Cancellation);
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = nameof(ConcurrentProgressRequestsWithDistinctSequencesLessThan10SecondsApartOneSucceedsAndOtherReturnsRecordedFalse))]
    public async Task ConcurrentProgressRequestsWithDistinctSequencesLessThan10SecondsApartOneSucceedsAndOtherReturnsRecordedFalse()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var host = ClockHost(clock);
        var test = new PlaybackTestContext(factory) { Host = host };
        await test.SeedAsync(true, Cancellation);
        using var client1 = test.Client();
        using var client2 = test.Client();
        var sessionId = await test.OpenAsync(client1, Cancellation);

        var task1 = SendProgress(client1, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        var task2 = SendProgress(client2, sessionId, new { sequence = 2, positionSeconds = 15, reason = "left" });

        var responses = await Task.WhenAll(task1, task2);

        foreach (var res in responses)
        {
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        var ack1 = await responses[0].Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var ack2 = await responses[1].Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var recorded1 = ack1.GetProperty("recorded").GetBoolean();
        var recorded2 = ack2.GetProperty("recorded").GetBoolean();

        Assert.True(recorded1 ^ recorded2, "Exactly one concurrent request within 10s window must be recorded.");

        using var scope = test.Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var count = await db.OutboxMessages.IgnoreQueryFilters().CountAsync(m => m.TenantId == test.Tenant, Cancellation);
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = nameof(DeliveredConfigurationPublishesProgressLiveInsteadOfRetainingIt))]
    public async Task DeliveredConfigurationPublishesProgressLiveInsteadOfRetainingIt()
    {
        // No retention override here: the host loads the appsettings.json that media ships.
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, Cancellation);
        using var client = test.Client();
        var sessionId = await test.OpenAsync(client, Cancellation);

        using var response = await SendProgress(client, sessionId, new { sequence = 1, positionSeconds = 10, reason = "heartbeat" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = test.Host.Services.CreateScope();
        Assert.DoesNotContain("midia.reproducao-avancou.v1",
            scope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>().Value.RetainedRoutingKeys, StringComparer.OrdinalIgnoreCase);
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var outbox = await db.OutboxMessages.IgnoreQueryFilters()
            .SingleAsync(m => m.Id == PlaybackSession.CreateDeterministicEventId(sessionId, 1), Cancellation);
        Assert.Null(outbox.ProcessedOn); // Pending for the publisher, not born retained
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ClockHost(AdjustableTimeProvider clock)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Outbox:RetainedRoutingKeys:0", "midia.reproducao-avancou.v1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });

    private static Task<HttpResponseMessage> SendProgress(HttpClient client, Guid sessionId, object payload)
        => client.PostAsJsonAsync($"/internal/v1/playback-sessions/{sessionId:D}/progress", payload, Cancellation);
}
