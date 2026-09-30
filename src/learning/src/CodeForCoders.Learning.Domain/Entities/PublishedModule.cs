namespace CodeForCoders.Learning.Domain.Entities;

public sealed record PublishedModule(Guid ModuleId, string Title, int Position, IReadOnlyList<PublishedLesson> Lessons);
