using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;

public sealed class ValidateStaffSession(
    IIdentitySessionStore sessionStore,
    IOptions<StaffSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    IValidator<ValidateStaffSessionInput> validator) : IValidateStaffSession
{
    public async Task<ValidateStaffSessionOutput?> ExecuteAsync(
        ValidateStaffSessionInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var active = await sessionStore.RenewActiveStaffSessionAsync(
            input.TenantId,
            input.SessionId,
            now,
            now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes),
            cancellationToken);
        return active is null ? null : new ValidateStaffSessionOutput(active);
    }
}
