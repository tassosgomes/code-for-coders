namespace CodeForCoders.Learning.Domain.SeedWork;

public sealed class CourseRuleException(string code, string? field = null) : Exception(
    field is null ? "The course request violates a business rule." : $"Invalid field: {field}.")
{
    public string Code { get; } = code;
    public string? Field { get; } = field;
}
