using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Identity.EndToEndTests;

[Collection(IdentityApiCollection.Name)]
public sealed class StaffInvitationIssuingEndpointTests(IdentityApiFactory factory)
{
    private const string Path = "/internal/v1/staff-invitations";
    private const string WriteScope = "staff-invitations:write";
    private static readonly Guid TenantId = Guid.Parse(IdentityApiFactory.ServiceTenantId);

    [Fact]
    public async Task CreateStaffInvitationInternal_RejectsASessionWithoutManageAccessWithoutWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacherSessionId = await SeedStaffSessionAsync(StaffRoleCatalog.Teacher, cancellationToken);
        var inviteeEmail = $"guest-{Guid.CreateVersion7():N}@example.com";
        var before = await CountSideEffectsAsync(inviteeEmail, cancellationToken);
        using var client = factory.CreateClient();

        using var response = await SendAsync(client, teacherSessionId, inviteeEmail, "invitation-denied-1", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("PERMISSION_DENIED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(403, problem.RootElement.GetProperty("status").GetInt32());
        var after = await CountSideEffectsAsync(inviteeEmail, cancellationToken);
        Assert.Equal(0, after.Invitations);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task CreateStaffInvitationInternal_CreatesTheInvitationForASessionWithManageAccess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var administratorSessionId = await SeedStaffSessionAsync(StaffRoleCatalog.Administrator, cancellationToken);
        var inviteeEmail = $"guest-{Guid.CreateVersion7():N}@example.com";
        var before = await CountSideEffectsAsync(inviteeEmail, cancellationToken);
        using var client = factory.CreateClient();

        using var response = await SendAsync(client, administratorSessionId, inviteeEmail, "invitation-allowed-1", cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var after = await CountSideEffectsAsync(inviteeEmail, cancellationToken);
        Assert.Equal(1, after.Invitations);
        Assert.Equal(before.OutboxMessages + 2, after.OutboxMessages);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Guid staffSessionId,
        string inviteeEmail,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = JsonContent.Create(new
            {
                email = inviteeEmail,
                role = StaffRoleCatalog.Teacher,
                reason = "Contratada para a trilha avançada.",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IdentityApiFactory.CreateAdminServiceAssertion(WriteScope));
        request.Headers.Add("X-Staff-Session", staffSessionId.ToString("D"));
        request.Headers.Add("Idempotency-Key", $"{idempotencyKey}-{staffSessionId:N}");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task<Guid> SeedStaffSessionAsync(string role, CancellationToken cancellationToken)
    {
        await using var scope = CreateTenantScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.CreateVersion7(now);
        var email = $"staff-{accountId:N}@example.com";
        dbContext.Accounts.Add(Account.CreateInternal(accountId, TenantId, "Staff Actor", email, email));
        dbContext.StaffRoleAssignments.Add(StaffRoleAssignment.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            TenantId,
            accountId,
            role,
            now));
        var sessionId = Guid.CreateVersion7(now.AddTicks(2));
        dbContext.StaffSessions.Add(StaffSession.Create(sessionId, TenantId, accountId, now, now.AddHours(1)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return sessionId;
    }

    private async Task<SideEffects> CountSideEffectsAsync(string inviteeEmail, CancellationToken cancellationToken)
    {
        await using var scope = CreateTenantScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return new SideEffects(
            await dbContext.StaffInvitations.CountAsync(item => item.NormalizedEmail == inviteeEmail, cancellationToken),
            await dbContext.OutboxMessages.CountAsync(cancellationToken),
            await dbContext.IdempotencyRecords.CountAsync(cancellationToken));
    }

    private AsyncServiceScope CreateTenantScope()
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(TenantId);
        return scope;
    }

    private sealed record SideEffects(int Invitations, int OutboxMessages, int IdempotencyRecords);
}
