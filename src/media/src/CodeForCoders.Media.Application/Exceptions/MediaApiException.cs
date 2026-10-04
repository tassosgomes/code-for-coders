namespace CodeForCoders.Media.Application.Exceptions;

public sealed class MediaApiException(
    int statusCode,
    string code,
    string title,
    string? detail = null) : UseCaseException(title)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;

    public string? Reason { get; init; }

    public DateTimeOffset? AccessEndedAt { get; init; }

    public string? Detail { get; } = detail;
}
