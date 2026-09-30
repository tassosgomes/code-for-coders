namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseCreationTransaction : IAsyncDisposable
{
    Task CompleteAsync(CancellationToken cancellationToken);
}
