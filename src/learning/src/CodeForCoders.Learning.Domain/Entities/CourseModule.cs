namespace CodeForCoders.Learning.Domain.Entities;

public sealed class CourseModule
{
    private CourseModule() { }
    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Position { get; private set; }
    public List<CourseLesson> Lessons { get; private set; } = [];

    internal static CourseModule Create(Guid courseId, string title) => new() { Id = Guid.CreateVersion7(), CourseId = courseId, Title = title.Trim() };
    internal void Rename(string title) => Title = title.Trim();
    internal void SetPosition(int position) => Position = position;
    internal void RenumberLessons()
    {
        for (var index = 0; index < Lessons.Count; index++) Lessons[index].SetPosition(index + 1);
    }
}
