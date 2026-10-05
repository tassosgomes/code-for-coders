using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class UnavailableExistingCourseAccessReader : IExistingCourseAccessReader
{
    public Task<ExistingCourseAccess?> FindAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken) => throw new HttpRequestException("Unavailable");
}
