namespace CodeForCoders.BffStudent.Api.Clients;

public interface IStudentLessonLearningClient
{
    Task<StudentLessonProxyResult> GetAsync(Guid lessonId, string accessToken, CancellationToken cancellationToken);
}
