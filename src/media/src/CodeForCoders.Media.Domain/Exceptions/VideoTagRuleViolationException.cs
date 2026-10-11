namespace CodeForCoders.Media.Domain.Exceptions;

public sealed class VideoTagRuleViolationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
