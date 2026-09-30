namespace CodeForCoders.Learning.Domain.Entities;

public sealed class CourseVersion
{
    private CourseVersion() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CourseId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? VersionNote { get; private set; }
    public Guid PublishedById { get; private set; }
    public string PublishedByName { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public IReadOnlyList<PublishedModule> Modules { get; private set; } = [];

    internal static CourseVersion Create(Course course, CoursePublication input, int number)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = course.TenantId,
            CourseId = course.Id,
            VersionNumber = number,
            Title = course.Title,
            Description = course.Description,
            VersionNote = input.VersionNote,
            PublishedById = input.Actor.ActorId,
            PublishedByName = input.Actor.ActorName,
            PublishedAt = input.Actor.Now,
            Modules = course.Modules.OrderBy(module => module.Position).Select(module =>
                new PublishedModule(module.Id, module.Title, module.Position,
                    module.Lessons.OrderBy(lesson => lesson.Position).Select(lesson =>
                        new PublishedLesson(lesson.Id, lesson.Title, lesson.Description, lesson.Position, lesson.VideoId!.Value)).ToArray())).ToArray(),
        };
}
