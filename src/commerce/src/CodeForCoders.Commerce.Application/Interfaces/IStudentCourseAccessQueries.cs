namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IStudentCourseAccessQueries
{
    Task<IReadOnlyList<StudentCourseAccess>> ListAsync(StudentCourseAccessQuery input, CancellationToken cancellationToken);
}
