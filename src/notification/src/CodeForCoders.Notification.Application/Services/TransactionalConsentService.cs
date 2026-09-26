using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;

namespace CodeForCoders.Notification.Application.Services;

public sealed class TransactionalConsentService : IConsentService
{
    public Task<bool> AllowsAsync(
        string recipient,
        string purpose,
        CancellationToken cancellationToken)
        => Task.FromResult(
            purpose is NotificationPurposes.AccountConfirmation
                or NotificationPurposes.PasswordRecovery
                or NotificationPurposes.StaffInvitation);
}
