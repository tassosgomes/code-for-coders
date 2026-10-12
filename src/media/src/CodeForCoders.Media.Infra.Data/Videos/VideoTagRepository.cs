using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoTagRepository(MediaDbContext dbContext, ITenantContext tenantContext, TimeProvider timeProvider) : IVideoTagRepository
{
    public async Task<bool> WithLockedVideoAsync(VideoTagWriteScope scope, Func<Video, CancellationToken, Task> update, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize the operation key too, including requests targeting different videos.
        var operationKey = $"replaceVideoTags:{tenantContext.TenantId}:{scope.ActorAccountId}:{scope.IdempotencyKey}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({operationKey}, 0))", cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT video_id FROM media_access.videos WHERE video_id = {scope.VideoId} AND tenant_id = {tenantContext.TenantId} FOR UPDATE", cancellationToken);
        var video = await dbContext.Videos.AsNoTracking().SingleOrDefaultAsync(video => video.VideoId == scope.VideoId, cancellationToken);
        if (video is null) return false;
        await update(video, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RecordAssignments();
        return true;
    }

    public async Task<IReadOnlyList<string>> ResolveExistingAsync(IReadOnlyList<VideoTagValue> tags, CancellationToken cancellationToken)
    {
        var names = tags.Select(tag => tag.NormalizedName).ToArray();
        var existing = await dbContext.Tags.AsNoTracking()
            .Where(tag => names.Contains(tag.NormalizedName))
            .ToDictionaryAsync(tag => tag.NormalizedName, tag => tag.DisplayName, cancellationToken);
        // Pending uploads do not claim a new spelling before a video actually uses the tag.
        return tags.Select(tag => existing.GetValueOrDefault(tag.NormalizedName, tag.DisplayName)).ToArray();
    }

    public void RecordAssignments()
    {
        foreach (var origin in assignedOrigins)
            MediaTelemetry.VideoTagsAssigned.Add(1, new KeyValuePair<string, object?>("origin", origin));
        assignedOrigins.Clear();
    }

    public async Task<IReadOnlyList<string>> ReplaceAsync(Guid videoId, IReadOnlyList<VideoTagValue> tags, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var canonical = new List<VideoTag>();
        foreach (var value in tags.OrderBy(tag => tag.NormalizedName, StringComparer.Ordinal))
        {
            var candidate = VideoTag.Create(tenantId, value, timeProvider.GetUtcNow());
            var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO media_access.tags (tag_id, tenant_id, display_name, normalized_name, created_at) VALUES ({candidate.TagId}, {tenantId}, {candidate.DisplayName}, {candidate.NormalizedName}, {candidate.CreatedAt}) ON CONFLICT (tenant_id, normalized_name) DO NOTHING", cancellationToken);
            canonical.Add(await dbContext.Tags.AsNoTracking().SingleAsync(tag => tag.NormalizedName == value.NormalizedName, cancellationToken));
            assignedOrigins.Add(inserted == 1 ? "new" : "existing");
        }
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM media_access.video_tags WHERE video_id = {videoId} AND tenant_id = {tenantId}", cancellationToken);
        dbContext.VideoTags.AddRange(canonical.Select(tag => VideoTagLink.Create(videoId, tag)));
        return canonical.Select(tag => tag.DisplayName).ToArray();
    }

    private readonly List<string> assignedOrigins = [];

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetAsync(IReadOnlyList<Guid> videoIds, CancellationToken cancellationToken)
    {
        var rows = await (from link in dbContext.VideoTags.AsNoTracking()
                          join tag in dbContext.Tags.AsNoTracking() on link.TagId equals tag.TagId
                          where videoIds.Contains(link.VideoId)
                          select new { link.VideoId, tag.DisplayName, tag.NormalizedName }).ToArrayAsync(cancellationToken);
        return rows.GroupBy(row => row.VideoId).ToDictionary(group => group.Key,
            group => (IReadOnlyList<string>)group.OrderBy(row => row.NormalizedName, StringComparer.Ordinal).Select(row => row.DisplayName).ToArray());
    }
}
