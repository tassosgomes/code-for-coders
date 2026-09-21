namespace CodeForCoders.BffAdmin.Application.Exceptions;

public sealed class NotFoundException(string message) : UseCaseException(message);
