using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseVersionStore
{
    void Add(CourseVersion version);
    Task<bool> HasPublishedAsync(Guid courseId, CancellationToken cancellationToken);
    Task<CourseVersion?> GetAsync(Guid courseId, int versionNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublishedRecommendedCourse>> GetCurrentReferencesAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<CourseVersionSummaryPage> ListAsync(Guid courseId, int currentVersion, int page, int size, CancellationToken cancellationToken);
}
