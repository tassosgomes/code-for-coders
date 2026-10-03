using CodeForCoders.Media.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.PlaybackSessions;

public sealed class PlaybackSessionMaintenance(MediaDbContext context) : IPlaybackSessionMaintenance
{
    public Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        => context.PlaybackSessions.IgnoreQueryFilters().Where(session => session.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
}
