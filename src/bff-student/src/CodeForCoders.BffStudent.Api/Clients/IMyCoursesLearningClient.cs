namespace CodeForCoders.BffStudent.Api.Clients;

public interface IMyCoursesLearningClient
{
    Task<MyCoursesProxyResult> ListAsync(string accessToken, CancellationToken cancellationToken);
}
