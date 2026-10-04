namespace CodeForCoders.Learning.Application.Interfaces;

public interface IStudentCoursesQueries
{
    Task<IReadOnlyList<CurrentStudentCourse>> ListCurrentVersionsAsync(IReadOnlyList<Guid> courseIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentCourseLessonActivity>> ListProgressAsync(Guid studentId, IReadOnlyList<Guid> courseIds, CancellationToken cancellationToken);
}
