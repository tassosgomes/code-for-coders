namespace CodeForCoders.BffStudent.Api.Security;

public sealed class StudentSpaCorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}
