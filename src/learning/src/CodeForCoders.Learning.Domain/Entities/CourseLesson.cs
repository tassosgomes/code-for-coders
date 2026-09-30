namespace CodeForCoders.Learning.Domain.Entities;

public sealed class CourseLesson
{
    private CourseLesson() { }
    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public Guid? VideoId { get; private set; }

    internal static CourseLesson Create(Guid moduleId, CourseChanges changes) => new()
    { Id = Guid.CreateVersion7(), ModuleId = moduleId, Title = changes.Title!.Trim(), Description = changes.Description, VideoId = changes.VideoId };
    internal void Update(CourseChanges changes)
    {
        if (changes.Title is not null) Title = changes.Title.Trim();
        if (changes.HasDescription) Description = changes.Description;
        if (changes.HasVideoId) VideoId = changes.VideoId;
    }
    internal void MoveTo(Guid moduleId) => ModuleId = moduleId;
    internal static CourseLesson Restore(Guid moduleId, PublishedLesson snapshot)
    {
        var lesson = new CourseLesson { Id = snapshot.LessonId };
        lesson.RestoreContent(moduleId, snapshot);
        return lesson;
    }
    internal void RestoreContent(Guid moduleId, PublishedLesson snapshot)
    {
        ModuleId = moduleId;
        Title = snapshot.Title;
        Description = snapshot.Description;
        Position = snapshot.Position;
        VideoId = snapshot.VideoId;
    }
    internal void SetPosition(int position) => Position = position;
}
