namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IStudentOrderQueries
{
    Task<StudentOrderRows> ListAsync(StudentOrderQuery input, CancellationToken cancellationToken);
}
