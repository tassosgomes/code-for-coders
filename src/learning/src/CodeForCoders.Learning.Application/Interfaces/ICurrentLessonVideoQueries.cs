namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICurrentLessonVideoQueries
{
    Task<Guid?> FindVideoAsync(CurrentLessonReference lesson, CancellationToken cancellationToken);
    Task<IReadOnlyList<CurrentLessonReference>> ListByVideoAsync(Guid videoId, CancellationToken cancellationToken);
}
