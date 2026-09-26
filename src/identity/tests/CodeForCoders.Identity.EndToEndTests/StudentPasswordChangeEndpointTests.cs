using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Identity.EndToEndTests;

[Collection(IdentityApiCollection.Name)]
public sealed class StudentPasswordChangeEndpointTests(IdentityApiFactory factory)
{
    private const string Path = "/internal/v1/password-changes";
    private const string ChangeScope = "student-password-changes:execute";
    private const string FactRoutingKey = "identidade.senha-redefinida.v1";
    private static readonly Guid TenantId = Guid.Parse(IdentityApiFactory.ServiceTenantId);

    [Fact]
    public async Task ChangeStudentPasswordInternal_ReturnsNoContentPreservesCurrentSessionAndReplaysTheSameKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await SeedStudentAsync("SenhaForte1!", cancellationToken);
        using var client = factory.CreateClient();
        var body = new { sessionId = student.CurrentSessionId, currentPassword = "SenhaForte1!", newPassword = "NovaSenha2!" };

        using var response = await SendAsync(client, JsonContent.Create(body), "change-success-1", ChangeScope, cancellationToken);
        using var replay = await SendAsync(client, JsonContent.Create(body), "change-success-1", ChangeScope, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);

        await using var scope = CreateTenantScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var credential = await dbContext.Credentials.SingleAsync(item => item.AccountId == student.AccountId, cancellationToken);
        var sessions = await dbContext.StudentSessions.Where(item => item.AccountId == student.AccountId).ToListAsync(cancellationToken);
        var facts = await FindFactsAsync(dbContext, student.AccountId, cancellationToken);

        Assert.True(hasher.Verify("NovaSenha2!", credential.PasswordHash));
        Assert.Null(sessions.Single(item => item.Id == student.CurrentSessionId).RevokedOn);
        Assert.NotNull(sessions.Single(item => item.Id == student.OtherSessionId).RevokedOn);
        var fact = Assert.Single(facts);
        Assert.DoesNotContain("SenhaForte1!", fact.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("NovaSenha2!", fact.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeStudentPasswordInternal_RejectsMissingWrongScopeOrReplayedAssertionWithServiceUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await SeedStudentAsync("SenhaForte1!", cancellationToken);
        using var client = factory.CreateClient();
        var body = new { sessionId = student.CurrentSessionId, currentPassword = "SenhaForte1!", newPassword = "NovaSenha2!" };
        var assertion = IdentityApiFactory.CreateServiceAssertion(ChangeScope);

        using var missing = await SendAsync(client, JsonContent.Create(body), "change-unauthorized-1", scope: null, cancellationToken);
        using var wrongScope = await SendAsync(client, JsonContent.Create(body), "change-unauthorized-2", "student-sessions:execute", cancellationToken);
        using var firstUse = await SendWithAssertionAsync(client, JsonContent.Create(body with { newPassword = "fraca" }), "change-unauthorized-3", assertion, cancellationToken);
        using var replayed = await SendWithAssertionAsync(client, JsonContent.Create(body), "change-unauthorized-4", assertion, cancellationToken);

        await AssertProblemAsync(missing, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", cancellationToken);
        await AssertProblemAsync(wrongScope, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", cancellationToken);
        await AssertProblemAsync(firstUse, HttpStatusCode.UnprocessableEntity, "PASSWORD_CHANGE_REJECTED", cancellationToken);
        await AssertProblemAsync(replayed, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", cancellationToken);
        await AssertUnchangedAsync(student, "SenhaForte1!", cancellationToken);
    }

    [Fact]
    public async Task ChangeStudentPasswordInternal_RejectsForeignIssuerAssertionWithServiceUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await SeedStudentAsync("SenhaForte1!", cancellationToken);
        using var client = factory.CreateClient();
        var body = new { sessionId = student.CurrentSessionId, currentPassword = "SenhaForte1!", newPassword = "NovaSenha2!" };

        using var adminIssuer = await SendWithAssertionAsync(
            client,
            JsonContent.Create(body),
            "change-issuer-1",
            IdentityApiFactory.CreateAdminServiceAssertion("staff-sessions:validate"),
            cancellationToken);
        using var studentInBackofficeScope = await SendAsync(
            client,
            JsonContent.Create(body),
            "change-issuer-2",
            "staff-sessions:validate",
            cancellationToken);

        await AssertProblemAsync(adminIssuer, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", cancellationToken);
        await AssertProblemAsync(studentInBackofficeScope, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", cancellationToken);
        await AssertUnchangedAsync(student, "SenhaForte1!", cancellationToken);
    }

    [Fact]
    public async Task ChangeStudentPasswordInternal_RejectsInvalidJsonMissingFieldsOrIdempotencyKeyWithBadRequestProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await SeedStudentAsync("SenhaForte1!", cancellationToken);
        using var client = factory.CreateClient();
        var body = new { sessionId = student.CurrentSessionId, currentPassword = "SenhaForte1!", newPassword = "NovaSenha2!" };

        using var invalidJson = await SendAsync(
            client,
            new StringContent("{\"sessionId\":", Encoding.UTF8, "application/json"),
            "change-bad-request-1",
            ChangeScope,
            cancellationToken);
        using var missingField = await SendAsync(
            client,
            JsonContent.Create(new { sessionId = student.CurrentSessionId, newPassword = "NovaSenha2!" }),
            "change-bad-request-2",
            ChangeScope,
            cancellationToken);
        using var missingKey = await SendAsync(client, JsonContent.Create(body), idempotencyKey: null, ChangeScope, cancellationToken);

        await AssertProblemAsync(invalidJson, HttpStatusCode.BadRequest, "INVALID_REQUEST", cancellationToken);
        await AssertProblemAsync(missingField, HttpStatusCode.BadRequest, "INVALID_REQUEST", cancellationToken);
        await AssertProblemAsync(missingKey, HttpStatusCode.BadRequest, "INVALID_REQUEST", cancellationToken);
        await AssertUnchangedAsync(student, "SenhaForte1!", cancellationToken);
    }

    [Fact]
    public async Task ChangeStudentPasswordInternal_MapsRejectedPasswordConflictAndInactiveSessionToContractCodes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await SeedStudentAsync("SenhaForte1!", cancellationToken);
        using var client = factory.CreateClient();
        var wrongCurrent = new { sessionId = student.CurrentSessionId, currentPassword = "SenhaErrada1!", newPassword = "NovaSenha2!" };

        using var rejected = await SendAsync(client, JsonContent.Create(wrongCurrent), "change-rules-1", ChangeScope, cancellationToken);
        using var conflict = await SendAsync(
            client,
            JsonContent.Create(wrongCurrent with { newPassword = "OutraSenha3!" }),
            "change-rules-1",
            ChangeScope,
            cancellationToken);
        using var unknownSession = await SendAsync(
            client,
            JsonContent.Create(wrongCurrent with { sessionId = Guid.CreateVersion7(), currentPassword = "SenhaForte1!" }),
            "change-rules-2",
            ChangeScope,
            cancellationToken);

        await AssertProblemAsync(rejected, HttpStatusCode.UnprocessableEntity, "PASSWORD_CHANGE_REJECTED", cancellationToken);
        await AssertProblemAsync(conflict, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", cancellationToken);
        await AssertProblemAsync(unknownSession, HttpStatusCode.Unauthorized, "SESSION_REQUIRED", cancellationToken);
        await AssertUnchangedAsync(student, "SenhaForte1!", cancellationToken);
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpContent content,
        string? idempotencyKey,
        string? scope,
        CancellationToken cancellationToken)
        => SendWithAssertionAsync(
            client,
            content,
            idempotencyKey,
            scope is null ? null : IdentityApiFactory.CreateServiceAssertion(scope),
            cancellationToken);

    private static async Task<HttpResponseMessage> SendWithAssertionAsync(
        HttpClient client,
        HttpContent content,
        string? idempotencyKey,
        string? assertion,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = content };
        if (assertion is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var problem = JsonDocument.Parse(content);
        var root = problem.RootElement;
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("SenhaForte1!", content, StringComparison.Ordinal);
        Assert.DoesNotContain("NovaSenha2!", content, StringComparison.Ordinal);
    }

    private async Task AssertUnchangedAsync(SeededStudent student, string password, CancellationToken cancellationToken)
    {
        await using var scope = CreateTenantScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var credential = await dbContext.Credentials.SingleAsync(item => item.AccountId == student.AccountId, cancellationToken);
        var sessions = await dbContext.StudentSessions.Where(item => item.AccountId == student.AccountId).ToListAsync(cancellationToken);
        var facts = await FindFactsAsync(dbContext, student.AccountId, cancellationToken);

        Assert.True(hasher.Verify(password, credential.PasswordHash));
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, session => Assert.Null(session.RevokedOn));
        Assert.Empty(facts);
    }

    private static async Task<List<OutboxMessage>> FindFactsAsync(
        IdentityDbContext dbContext,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .Where(message => message.RoutingKey == FactRoutingKey)
            .ToListAsync(cancellationToken);
        return messages
            .Where(message => message.Payload.Contains(accountId.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<SeededStudent> SeedStudentAsync(string password, CancellationToken cancellationToken)
    {
        await using var scope = CreateTenantScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.CreateVersion7(now);
        var email = $"student-{accountId:N}@example.com";
        var account = Account.CreateStudent(accountId, TenantId, "Ana Souza", email, email);
        account.Confirm();
        dbContext.Accounts.Add(account);
        dbContext.Credentials.Add(Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            TenantId,
            accountId,
            hasher.Hash(password),
            now));

        var currentSessionId = Guid.CreateVersion7(now.AddTicks(2));
        var otherSessionId = Guid.CreateVersion7(now.AddTicks(3));
        dbContext.StudentSessions.AddRange(
            StudentSession.Create(currentSessionId, TenantId, accountId, now, now.AddHours(1)),
            StudentSession.Create(otherSessionId, TenantId, accountId, now, now.AddHours(1)));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SeededStudent(accountId, currentSessionId, otherSessionId);
    }

    private AsyncServiceScope CreateTenantScope()
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(TenantId);
        return scope;
    }

    private sealed record SeededStudent(Guid AccountId, Guid CurrentSessionId, Guid OtherSessionId);
}
