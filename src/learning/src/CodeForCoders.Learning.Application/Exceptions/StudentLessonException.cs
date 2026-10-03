namespace CodeForCoders.Learning.Application.Exceptions;

public sealed class StudentLessonException(string code, string? reason = null, DateTimeOffset? accessEndedAt = null) : Exception(code)
{
    public string Code { get; } = code;
    public string? Reason { get; } = reason;
    public DateTimeOffset? AccessEndedAt { get; } = accessEndedAt;
}
