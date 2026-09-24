using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;

public sealed class RegisterStudentAccount(
    IIdentityRegistrationStore registrationStore,
    IStudentRegistrationMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IIdempotencyFingerprinter fingerprinter,
    IOptions<RegistrationOptions> registrationOptions,
    TimeProvider timeProvider,
    IValidator<RegisterStudentAccountInput> validator) : IRegisterStudentAccount
{
    public const string OperationId = "createStudentAccountInternal";
    private const string RegistrationRejectedTitle = "Student registration could not be completed.";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<RegisterStudentAccountOutput> ExecuteAsync(
        RegisterStudentAccountInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var name = input.Name.Trim();
        var email = input.Email.Trim();
        var values = new RegistrationValues(
            name,
            email,
            email.ToLowerInvariant(),
            fingerprinter.HashKey(input.IdempotencyKey),
            fingerprinter.Fingerprint(name, email.ToLowerInvariant(), input.Password));
        var existingAttempt = await registrationStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);

        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        if (!StudentPasswordPolicy.IsSatisfiedBy(input.Password))
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                "PASSWORD_POLICY_VIOLATION",
                RegistrationRejectedTitle,
                now,
                cancellationToken);
        }

        if (await registrationStore.HasActiveAccountAsync(input.TenantId, values.NormalizedEmail, cancellationToken))
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                "ACCOUNT_ALREADY_EXISTS",
                "An active account already uses this email address.",
                now,
                cancellationToken);
        }

        return await CreateAccountAsync(input, values, existingAttempt, now, cancellationToken);
    }

    private async Task<RegisterStudentAccountOutput> CreateAccountAsync(
        RegisterStudentAccountInput input,
        RegistrationValues values,
        IdempotencyRecord? existingAttempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accountId = Guid.CreateVersion7(now);
        var account = Account.CreateStudent(
            accountId,
            input.TenantId,
            values.Name,
            values.Email,
            values.NormalizedEmail);
        var rawToken = CreateToken();
        var credential = Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            input.TenantId,
            accountId,
            passwordHasher.Hash(input.Password),
            now);
        var token = VerificationToken.Create(
            Guid.CreateVersion7(now.AddTicks(2)),
            input.TenantId,
            accountId,
            StudentRegistrationMessageWriterPurpose,
            HashToken(rawToken),
            now.AddHours(registrationOptions.Value.ConfirmationLifetimeHours));
        var attempt = CreateAttempt(existingAttempt, input.TenantId, values, 202, null, null, now);

        registrationStore.AddRegistration(account, credential, token);
        if (existingAttempt is null)
        {
            registrationStore.AddIdempotencyRecord(attempt);
        }

        await messageWriter.AppendAsync(account, rawToken, now, cancellationToken);
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return new RegisterStudentAccountOutput(202, null, null);
        }
        catch (RegistrationWriteConflictException exception)
        {
            return await ResolveWriteConflictAsync(
                input,
                values,
                existingAttempt,
                now,
                exception,
                cancellationToken);
        }
    }

    private async Task<RegisterStudentAccountOutput> ResolveWriteConflictAsync(
        RegisterStudentAccountInput input,
        RegistrationValues values,
        IdempotencyRecord? originalAttempt,
        DateTimeOffset now,
        RegistrationWriteConflictException exception,
        CancellationToken cancellationToken)
    {
        var winner = await registrationStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (winner is not null && !winner.IsExpired(now))
        {
            return ResolveReplay(winner, values.Fingerprint);
        }

        if (!await registrationStore.HasActiveAccountAsync(input.TenantId, values.NormalizedEmail, cancellationToken))
        {
            throw exception;
        }

        return await SaveRejectedAttemptAsync(
            winner ?? originalAttempt,
            input.TenantId,
            values,
            "ACCOUNT_ALREADY_EXISTS",
            "An active account already uses this email address.",
            now,
            cancellationToken);
    }

    private async Task<RegisterStudentAccountOutput> SaveRejectedAttemptAsync(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        RegistrationValues values,
        string code,
        string title,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var attempt = CreateAttempt(existingAttempt, tenantId, values, 422, code, title, now);
        if (existingAttempt is null)
        {
            registrationStore.AddIdempotencyRecord(attempt);
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException)
        {
            var winner = await registrationStore.FindIdempotencyAsync(
                tenantId,
                OperationId,
                values.KeyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return ResolveReplay(winner, values.Fingerprint);
            }

            throw;
        }

        throw new StudentRegistrationException(code, title, title);
    }

    private static IdempotencyRecord CreateAttempt(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        RegistrationValues values,
        int statusCode,
        string? code,
        string? title,
        DateTimeOffset now)
    {
        if (existingAttempt is not null)
        {
            existingAttempt.Refresh(
                values.Fingerprint,
                statusCode,
                code,
                title,
                now,
                now.Add(IdempotencyWindow));
            return existingAttempt;
        }

        return IdempotencyRecord.Create(
            tenantId,
            OperationId,
            values.KeyHash,
            values.Fingerprint,
            statusCode,
            code,
            title,
            now,
            now.Add(IdempotencyWindow));
    }

    private static RegisterStudentAccountOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.Fingerprint),
                Convert.FromHexString(fingerprint)))
        {
            throw new StudentRegistrationException(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == 202)
        {
            return new RegisterStudentAccountOutput(record.StatusCode, null, null);
        }

        throw new StudentRegistrationException(
            record.Code ?? "REGISTRATION_REJECTED",
            record.Title ?? RegistrationRejectedTitle,
            record.Title ?? RegistrationRejectedTitle);
    }

    private static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private sealed record RegistrationValues(
        string Name,
        string Email,
        string NormalizedEmail,
        string KeyHash,
        string Fingerprint);

    private const string StudentRegistrationMessageWriterPurpose = "confirmacao-de-conta";
}
