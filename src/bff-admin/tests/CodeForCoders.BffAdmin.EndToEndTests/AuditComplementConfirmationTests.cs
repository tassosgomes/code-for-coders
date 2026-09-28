using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Contracts;
using CodeForCoders.BffAdmin.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class AuditComplementConfirmationTests(BffAdminApiFactory factory)
{
    private const string Explanation = "The role assignment was verified against support case AUD-502.";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ReturnsAcceptedOnlyAfterTheProtectedOutboxCommit))]
    public async Task AuditComplementConfirmation_ReturnsAcceptedOnlyAfterTheProtectedOutboxCommit()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();

        using var response = await SendConfirmationAsync(
            client,
            login,
            AuditRecordSearchHandler.RecordId,
            idempotencyKey,
            Explanation);
        var accepted = await response.Content.ReadFromJsonAsync<AuditComplementConfirmationAcceptedV1>(TestContext.Current.CancellationToken);
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("accepted", accepted?.Status);
        Assert.NotEqual(Guid.Empty, accepted?.ConfirmationId);
        Assert.Single(outboxMessages);
        Assert.Single(records);
        Assert.Equal(accepted?.ConfirmationId, outboxMessages[0].Id);
        Assert.Equal("audit.events", outboxMessages[0].DestinationExchange);
        Assert.Equal("e2e-v1", outboxMessages[0].PayloadKeyVersion);
        Assert.DoesNotContain(Explanation, outboxMessages[0].Payload, StringComparison.Ordinal);
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(AuditRecordSearchHandler.RecordId, factory.AuditRecordSearchHandler.LastRecordId);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ReturnsTheSameConfirmationForAnIdenticalRetry))]
    public async Task AuditComplementConfirmation_ReturnsTheSameConfirmationForAnIdenticalRetry()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();

        using var first = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, idempotencyKey, Explanation);
        factory.AuditRecordSearchHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        using var second = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, idempotencyKey, Explanation);
        var firstAccepted = await first.Content.ReadFromJsonAsync<AuditComplementConfirmationAcceptedV1>(TestContext.Current.CancellationToken);
        var secondAccepted = await second.Content.ReadFromJsonAsync<AuditComplementConfirmationAcceptedV1>(TestContext.Current.CancellationToken);
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(firstAccepted?.ConfirmationId, secondAccepted?.ConfirmationId);
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Single(outboxMessages);
        Assert.Single(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsReusingAKeyWithDifferentText))]
    public async Task AuditComplementConfirmation_RejectsReusingAKeyWithDifferentText()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();
        using var first = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, idempotencyKey, Explanation);
        using var conflict = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, idempotencyKey, "A changed explanation.");

        await AssertProblemAsync(conflict, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT");
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);
        Assert.Single(outboxMessages);
        Assert.Single(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsBlankTextWithoutWriting))]
    public async Task AuditComplementConfirmation_RejectsBlankTextWithoutWriting()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();

        using var response = await SendConfirmationAsync(
            client,
            login,
            AuditRecordSearchHandler.RecordId,
            idempotencyKey,
            "   ");

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "EXPLANATION_REQUIRED");
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);
        Assert.Empty(outboxMessages);
        Assert.Empty(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsARequestWithoutTheAdministratorRole))]
    public async Task AuditComplementConfirmation_RejectsARequestWithoutTheAdministratorRole()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            Roles = ["professor"],
        };

        using var response = await SendConfirmationAsync(
            client,
            login,
            AuditRecordSearchHandler.RecordId,
            idempotencyKey,
            Explanation);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);
        Assert.Empty(outboxMessages);
        Assert.Empty(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsInvalidCsrfBeforeLookupOrWriting))]
    public async Task AuditComplementConfirmation_RejectsInvalidCsrfBeforeLookupOrWriting()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();

        using var request = CreateRequest(login, AuditRecordSearchHandler.RecordId, idempotencyKey, Explanation);
        request.Headers.Remove("X-CSRF-Token");
        request.Headers.Add("X-CSRF-Token", "wrong-csrf-token");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "CSRF_INVALID");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);
        Assert.Empty(outboxMessages);
        Assert.Empty(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ReturnsNeutralNotFoundWithoutWriting))]
    public async Task AuditComplementConfirmation_ReturnsNeutralNotFoundWithoutWriting()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var idempotencyKey = Guid.CreateVersion7();
        factory.AuditRecordSearchHandler.StatusCode = HttpStatusCode.NotFound;
        factory.AuditRecordSearchHandler.ProblemCode = "AUDIT_RECORD_NOT_FOUND";

        using var response = await SendConfirmationAsync(
            client,
            login,
            AuditRecordSearchHandler.RecordId,
            idempotencyKey,
            Explanation);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "AUDIT_RECORD_NOT_FOUND");
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
        var (outboxMessages, records) = await ReadStateAsync(idempotencyKey);
        Assert.Empty(outboxMessages);
        Assert.Empty(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RequiresAUuidIdempotencyKey))]
    public async Task AuditComplementConfirmation_RequiresAUuidIdempotencyKey()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        using var request = CreateRequest(login, AuditRecordSearchHandler.RecordId, Guid.Empty, Explanation);
        request.Headers.Remove("Idempotency-Key");
        request.Headers.Add("Idempotency-Key", "invalid-key");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        var (outboxMessages, records) = await ReadStateAsync(Guid.Empty);
        Assert.Empty(outboxMessages);
        Assert.Empty(records);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_KeepsTheExplanationOutOfCapturedLogs))]
    public async Task AuditComplementConfirmation_KeepsTheExplanationOutOfCapturedLogs()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var acceptedText = $"Accepted log marker {Guid.CreateVersion7():N}";
        var conflictText = $"Conflict log marker {Guid.CreateVersion7():N}";
        var oversizedText = $"Oversized log marker {Guid.CreateVersion7():N} " + new string('x', 1000);
        var unavailableText = $"Unavailable log marker {Guid.CreateVersion7():N}";
        var forbiddenText = $"Forbidden log marker {Guid.CreateVersion7():N}";
        var acceptedKey = Guid.CreateVersion7();
        var logCountBefore = factory.CapturedLogs.Entries.Count;

        using var accepted = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, acceptedKey, acceptedText);
        using var replay = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, acceptedKey, acceptedText);
        using var conflict = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, acceptedKey, conflictText);
        using var oversized = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, Guid.CreateVersion7(), oversizedText);
        factory.AuditRecordSearchHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        using var unavailable = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, Guid.CreateVersion7(), unavailableText);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            Roles = ["professor"],
        };
        using var forbidden = await SendConfirmationAsync(client, login, AuditRecordSearchHandler.RecordId, Guid.CreateVersion7(), forbiddenText);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        await AssertProblemAsync(conflict, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT");
        await AssertProblemAsync(oversized, HttpStatusCode.UnprocessableEntity, "EXPLANATION_REQUIRED");
        Assert.NotEqual(HttpStatusCode.Accepted, unavailable.StatusCode);
        await AssertProblemAsync(forbidden, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        var logs = factory.CapturedLogs.Entries;
        Assert.True(logs.Count > logCountBefore);
        foreach (var text in new[] { acceptedText, conflictText, oversizedText[..53], unavailableText, forbiddenText })
        {
            Assert.DoesNotContain(logs, entry => entry.Contains(text, StringComparison.Ordinal));
        }
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.AuditRecordSearchHandler.Reset();
        factory.AuditIdentityReferenceHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", $"audit-confirmation-{Guid.CreateVersion7():N}");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        return new LoginResult(cookie, session);
    }

    private static Task<HttpResponseMessage> SendConfirmationAsync(
        HttpClient client,
        LoginResult login,
        Guid recordId,
        Guid idempotencyKey,
        string? explanation)
        => client.SendAsync(CreateRequest(login, recordId, idempotencyKey, explanation), TestContext.Current.CancellationToken);

    private static HttpRequestMessage CreateRequest(
        LoginResult login,
        Guid recordId,
        Guid idempotencyKey,
        string? explanation)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/audit-records/{recordId:D}/complement-confirmations")
        {
            Content = JsonContent.Create(new AuditComplementConfirmationRequestV1(explanation)),
        };
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString("D"));
        request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        request.Headers.Add("Origin", "http://localhost:8081");
        return request;
    }

    private async Task<(IReadOnlyList<CodeForCoders.BffAdmin.Infra.Data.Outbox.OutboxMessage> OutboxMessages,
        IReadOnlyList<CodeForCoders.BffAdmin.Infra.Data.Idempotency.AuditComplementIdempotencyRecord> Records)> ReadStateAsync(
        Guid? idempotencyKey = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BffAdminDbContext>();
        var records = await dbContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .Where(record => record.TenantId == TenantId
                && (!idempotencyKey.HasValue || record.IdempotencyKey == idempotencyKey.Value))
            .ToListAsync(TestContext.Current.CancellationToken);
        var confirmationIds = records.Select(record => record.ConfirmationId).ToArray();
        var messages = await dbContext.OutboxMessages.IgnoreQueryFilters()
            .Where(message => message.TenantId == TenantId
                && message.DestinationExchange == "audit.events"
                && confirmationIds.Contains(message.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        return (messages, records);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode statusCode, string code)
    {
        Assert.Equal(statusCode, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
