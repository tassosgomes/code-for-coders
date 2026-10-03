namespace CodeForCoders.Media.Application.Interfaces;

public interface IPlaybackSessionMaintenance
{
    Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
