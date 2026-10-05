namespace CodeForCoders.BffStudent.Api.Clients;

public interface ICourseProgressLearningClient
{
    Task<CourseProgressProxyResult> GetAsync(Guid courseId, string accessToken, CancellationToken cancellationToken);
}
