namespace CodeForCoders.Identity.Application.Exceptions;

public sealed class StaffPasswordRecoveryException(string code, string title, string message)
    : UseCaseException(message)
{
    public string Code { get; } = code;

    public string Title { get; } = title;

    public int StatusCode => 422;
}
