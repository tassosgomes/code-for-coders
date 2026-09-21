namespace CodeForCoders.BffStudent.Application.Exceptions;

public sealed class NotFoundException(string message) : UseCaseException(message);
