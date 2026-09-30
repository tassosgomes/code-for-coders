namespace CodeForCoders.Media.Infra.Data.CourseReferences;

public sealed class CourseReferenceVersion
{
    public Guid TenantId { get; set; }
    public Guid CourseId { get; set; }
    public int VersionNumber { get; set; }
}
