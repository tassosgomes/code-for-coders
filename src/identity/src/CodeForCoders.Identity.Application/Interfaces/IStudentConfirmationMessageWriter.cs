using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStudentConfirmationMessageWriter
{
    Task AppendAccountConfirmedAsync(Account account, DateTimeOffset occurredOn, CancellationToken cancellationToken);

    Task AppendConfirmationRequestedAsync(
        Account account,
        string rawToken,
        DateTimeOffset requestedOn,
        CancellationToken cancellationToken);
}
