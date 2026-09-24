namespace CodeForCoders.Identity.Application.Interfaces;

public interface IServiceAssertionReplayStore
{
    Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken);
}
