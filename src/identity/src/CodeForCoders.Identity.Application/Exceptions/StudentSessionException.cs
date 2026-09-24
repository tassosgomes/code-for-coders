namespace CodeForCoders.Identity.Application.Exceptions;

public sealed class StudentSessionException(
    int statusCode,
    string code,
    string title,
    string message) : UseCaseException(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;
}
