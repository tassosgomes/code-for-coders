namespace CodeForCoders.Media.Domain.Exceptions;

public sealed class VideoUploadRuleViolationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
