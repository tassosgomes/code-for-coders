namespace CodeForCoders.Learning.Domain.SeedWork;

public sealed class CourseRuleException(string code) : Exception("The course request violates a business rule.")
{
    public string Code { get; } = code;
}
