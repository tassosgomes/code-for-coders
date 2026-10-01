namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IServiceAssertionReplayStore
{
    /// <summary>Records the assertion id until it expires; returns false when it was already consumed.</summary>
    Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken);
}
