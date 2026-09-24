using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStudentPasswordRecoveryMessageWriter
{
    Task AppendPasswordResetRequestedAsync(
        Account account,
        string rawToken,
        DateTimeOffset requestedOn,
        CancellationToken cancellationToken);

    Task AppendPasswordResetAsync(
        Account account,
        DateTimeOffset resetOn,
        CancellationToken cancellationToken);
}
