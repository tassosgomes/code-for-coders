namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface ICourseAuthoringClient
{
    Task<CourseClientResult> SendAsync(CourseClientRequest request, CancellationToken cancellationToken);
}
