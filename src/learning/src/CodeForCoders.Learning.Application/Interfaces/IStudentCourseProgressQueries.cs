namespace CodeForCoders.Learning.Application.Interfaces;

public interface IStudentCourseProgressQueries
{
    Task<CurrentCourseProgressVersion?> FindCurrentVersionAsync(Guid courseId, CancellationToken cancellationToken);
    Task<StudentCourseProgress> ReadAsync(CurrentCourseProgressVersion version, Guid studentId, CancellationToken cancellationToken);
}
