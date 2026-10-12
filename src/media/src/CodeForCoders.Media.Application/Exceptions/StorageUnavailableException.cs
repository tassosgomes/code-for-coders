namespace CodeForCoders.Media.Application.Exceptions;

public sealed class StorageUnavailableException(string? cause = null, Exception? innerException = null)
    : Exception(cause is null ? DefaultMessage : $"{DefaultMessage} Cause: {cause}.", innerException)
{
    private const string DefaultMessage = "Media storage is temporarily unavailable.";
}
