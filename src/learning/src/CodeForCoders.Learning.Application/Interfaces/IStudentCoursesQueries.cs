namespace CodeForCoders.Learning.Application.Interfaces;

public interface IStudentCoursesQueries
{
    Task<IReadOnlyList<CurrentStudentCourse>> ListCurrentVersionsAsync(IReadOnlyList<Guid> courseIds, CancellationToken cancellationToken);
    /// <summary>Returns null when progress storage is unavailable; an empty list means no activity.</summary>
    Task<IReadOnlyList<StudentCourseLessonActivity>?> ListProgressAsync(Guid studentId, IReadOnlyList<Guid> courseIds, CancellationToken cancellationToken);
}
