namespace CodeForCoders.Learning.Application.Interfaces;

public interface IStudentCourseAccessClient
{
    Task<StudentCourseAccessList?> ListAsync(StudentCourseAccessQuery input, CancellationToken cancellationToken);
}
