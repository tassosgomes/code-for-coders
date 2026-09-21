namespace CodeForCoders.Learning.Application.Exceptions;

public sealed class NotFoundException(string message) : UseCaseException(message)
{
    public static void ThrowIfNull<T>(T? value, string message)
    {
        if (value is null)
        {
            throw new NotFoundException(message);
        }
    }
}
