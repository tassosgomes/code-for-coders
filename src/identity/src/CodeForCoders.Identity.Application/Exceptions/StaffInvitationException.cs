namespace CodeForCoders.Identity.Application.Exceptions;

public sealed class StaffInvitationException(int statusCode, string code, string title)
    : Exception(title)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;
}
