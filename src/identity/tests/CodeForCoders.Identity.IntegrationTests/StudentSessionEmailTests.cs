using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Accounts;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StudentSessionEmailTests(IdentityIntegrationFixture fixture)
{
    [Theory(DisplayName = nameof(RenewalProjectsEmailFromActiveStudentAccount))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenewalProjectsEmailFromActiveStudentAccount(bool confirmed)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = Guid.CreateVersion7();
        var student = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var context = fixture.CreateDbContext(tenant);
        var account = Account.CreateStudent(student, tenant, "Student", "student@example.com", "student@example.com");
        if (confirmed) account.Confirm();
        context.Accounts.Add(account);
        var session = StudentSession.Create(Guid.CreateVersion7(), tenant, student, now, now.AddMinutes(30));
        context.StudentSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        var useCase = new ValidateStudentSession(new IdentitySessionStore(context),
            Options.Create(new StudentSessionOptions()), TimeProvider.System, new ValidateStudentSessionInputValidator());
        var result = await useCase.ExecuteAsync(new(tenant, session.Id), cancellationToken);
        if (confirmed) { Assert.NotNull(result); Assert.Equal("student@example.com", result.Email); }
        else Assert.Null(result);
    }
}
