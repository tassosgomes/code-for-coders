namespace CodeForCoders.Learning.Application.Interfaces;

public interface IStudentLessonQueries
{
    Task<StudentLessonScreen?> FindAsync(Guid lessonId, CancellationToken cancellationToken);
}
