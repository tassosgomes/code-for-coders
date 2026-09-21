using CodeForCoders.Notification.Contracts;

namespace CodeForCoders.Notification.Application.Common;

public static class NotificationSendRequestRules
{
    public static string? GetRefusalReason(NotificationSendRequestedV1 request)
    {
        if (string.IsNullOrWhiteSpace(request.Finalidade))
        {
            return NotificationRefusalReasons.MissingPurpose;
        }

        if (request.Finalidade is not NotificationPurposes.AccountConfirmation
            and not NotificationPurposes.PasswordRecovery)
        {
            return NotificationRefusalReasons.UnknownPurpose;
        }

        if (request.Modelo != NotificationPurposes.AccountConfirmation)
        {
            return NotificationRefusalReasons.UnknownModel;
        }

        if (request.Dados is null
            || string.IsNullOrWhiteSpace(request.Dados.Nome)
            || string.IsNullOrWhiteSpace(request.Dados.Link)
            || !Uri.TryCreate(request.Dados.Link, UriKind.Absolute, out _))
        {
            return NotificationRefusalReasons.MissingData;
        }

        return null;
    }
}
