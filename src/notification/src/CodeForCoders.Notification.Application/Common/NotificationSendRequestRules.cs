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
            and not NotificationPurposes.PasswordRecovery
            and not NotificationPurposes.StaffInvitation)
        {
            return NotificationRefusalReasons.UnknownPurpose;
        }

        if (request.Modelo is not (
                NotificationPurposes.AccountConfirmation
                or NotificationPurposes.PasswordRecovery
                or NotificationPurposes.StaffInvitation)
            || request.Modelo != request.Finalidade)
        {
            return NotificationRefusalReasons.UnknownModel;
        }

        if (request.Dados is null || string.IsNullOrWhiteSpace(request.Dados.Link)
            || !Uri.TryCreate(request.Dados.Link, UriKind.Absolute, out _))
        {
            return NotificationRefusalReasons.MissingData;
        }

        if (request.Modelo == NotificationPurposes.StaffInvitation)
        {
            if (request.Dados.Papel is not ("professor" or "suporte" or "financeiro" or "administrador"))
            {
                return NotificationRefusalReasons.MissingData;
            }
        }
        else if (string.IsNullOrWhiteSpace(request.Dados.Nome))
        {
            return NotificationRefusalReasons.MissingData;
        }

        return null;
    }
}
