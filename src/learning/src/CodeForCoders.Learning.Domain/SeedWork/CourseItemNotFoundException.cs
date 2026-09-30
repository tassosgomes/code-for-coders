namespace CodeForCoders.Learning.Domain.SeedWork;

public sealed class CourseItemNotFoundException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
