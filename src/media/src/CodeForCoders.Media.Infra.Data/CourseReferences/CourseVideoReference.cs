namespace CodeForCoders.Media.Infra.Data.CourseReferences;

public sealed class CourseVideoReference
{
    public Guid TenantId { get; set; }
    public Guid CourseId { get; set; }
    public Guid LessonId { get; set; }
    public Guid VideoId { get; set; }
}
