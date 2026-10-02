namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IStudentAccessGrantQueries
{
    Task<StudentAccessGrantPage> ListAsync(StudentAccessGrantsQuery input, CancellationToken cancellationToken);
}
