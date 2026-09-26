using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;

public sealed class ProvisionFirstAdministrator(
    IIdentityStaffAccountStore staffAccountStore,
    IIdentityPasswordRecoveryStore passwordRecoveryStore,
    IStaffPasswordRecoveryMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IOptions<StaffAccountOptions> staffAccountOptions,
    TimeProvider timeProvider,
    IValidator<ProvisionFirstAdministratorInput> validator) : IProvisionFirstAdministrator
{
    public async Task<ProvisionFirstAdministratorOutput> ExecuteAsync(
        ProvisionFirstAdministratorInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        if (await staffAccountStore.HasAdministratorAsync(input.TenantId, cancellationToken))
        {
            return new ProvisionFirstAdministratorOutput(ProvisionFirstAdministratorStatus.AlreadyProvisioned);
        }

        var email = input.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();
        var existingAccount = await staffAccountStore.FindActiveAccountByNormalizedEmailAsync(
            input.TenantId,
            normalizedEmail,
            cancellationToken);
        if (existingAccount is not null)
        {
            var status = existingAccount.Type == AccountType.Student
                ? ProvisionFirstAdministratorStatus.EmailBelongsToStudent
                : ProvisionFirstAdministratorStatus.EmailAlreadyInUse;
            return new ProvisionFirstAdministratorOutput(status);
        }

        var now = timeProvider.GetUtcNow();
        var account = Account.CreateInternal(
            Guid.CreateVersion7(now),
            input.TenantId,
            input.Name,
            email,
            normalizedEmail);
        var assignment = StaffRoleAssignment.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            input.TenantId,
            account.Id,
            StaffRoleCatalog.Administrator,
            now);
        var rawToken = CreateToken();
        var resetOptions = staffAccountOptions.Value;
        passwordRecoveryStore.AddVerificationToken(VerificationToken.Create(
            Guid.CreateVersion7(now.AddTicks(2)),
            input.TenantId,
            account.Id,
            PasswordRecoveryPurpose,
            HashToken(rawToken),
            now.AddHours(resetOptions.PasswordResetLifetimeHours)));
        staffAccountStore.AddAccount(account);
        staffAccountStore.AddRoleAssignment(assignment);
        await messageWriter.AppendPasswordResetRequestedAsync(account, rawToken, now, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return new ProvisionFirstAdministratorOutput(ProvisionFirstAdministratorStatus.Created);
    }

    private static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";
}
