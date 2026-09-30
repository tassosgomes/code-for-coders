using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Repositories;

public sealed class CourseVersionStore(LearningDbContext context) : ICourseVersionStore
{
    public void Add(CourseVersion version) => context.CourseVersions.Add(version);

    public Task<bool> HasPublishedAsync(Guid courseId, CancellationToken cancellationToken)
        => context.CourseVersions.AnyAsync(version => version.CourseId == courseId, cancellationToken);

    public Task<CourseVersion?> GetAsync(Guid courseId, int versionNumber, CancellationToken cancellationToken)
        => context.CourseVersions.AsNoTracking().SingleOrDefaultAsync(version => version.CourseId == courseId && version.VersionNumber == versionNumber, cancellationToken);

    public async Task<CourseVersionSummaryPage> ListAsync(Guid courseId, int currentVersion, int page, int size, CancellationToken cancellationToken)
    {
        var query = context.CourseVersions.AsNoTracking().Where(version => version.CourseId == courseId);
        var total = await query.LongCountAsync(cancellationToken);
        var data = await query.OrderByDescending(version => version.VersionNumber).Skip((page - 1) * size).Take(size)
            .Select(version => new CourseVersionSummary(version.VersionNumber, version.PublishedAt,
                new CourseActor(version.PublishedByName), version.VersionNote, version.VersionNumber == currentVersion)).ToListAsync(cancellationToken);
        return new(data, new(page, size, total, (total + size - 1) / size));
    }
}
