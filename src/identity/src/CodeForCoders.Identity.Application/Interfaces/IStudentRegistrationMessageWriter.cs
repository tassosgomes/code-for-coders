using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStudentRegistrationMessageWriter
{
    Task AppendAsync(Account account, string confirmationToken, DateTimeOffset occurredOn, CancellationToken cancellationToken);
}
