namespace CodeForCoders.Media.Application.Exceptions;

public sealed class ConcurrencyConflictException() : Exception("The record was changed by another operation.");
