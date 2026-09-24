using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;

public sealed class ValidateStudentSession(
    IIdentitySessionStore sessionStore,
    IOptions<StudentSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    IValidator<ValidateStudentSessionInput> validator) : IValidateStudentSession
{
    public async Task<ValidateStudentSessionOutput?> ExecuteAsync(
        ValidateStudentSessionInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var active = await sessionStore.RenewActiveSessionAsync(
            input.TenantId,
            input.SessionId,
            now,
            now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes),
            cancellationToken);
        return active is null
            ? null
            : new ValidateStudentSessionOutput(active.AccountId, active.Name, active.ExpiresAt);
    }
}
