namespace CodeForCoders.Notification.Application.Interfaces;

public interface IConsentService
{
    Task<bool> AllowsAsync(
        string recipient,
        string purpose,
        CancellationToken cancellationToken);
}
