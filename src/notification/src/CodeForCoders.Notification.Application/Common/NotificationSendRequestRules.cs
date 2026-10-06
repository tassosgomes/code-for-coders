using CodeForCoders.Notification.Contracts;

namespace CodeForCoders.Notification.Application.Common;

public static class NotificationSendRequestRules
{
    public static string? GetRefusalReason(NotificationSendRequestedV1 request)
    {
        if ((request.Destinatario is null) == (request.DestinatarioConta is null)
            || request.DestinatarioConta is { } account && (account.Tipo != "conta-aluno" || account.Id == Guid.Empty))
            return NotificationRefusalReasons.InvalidFormat;

        if (string.IsNullOrWhiteSpace(request.Finalidade))
        {
            return NotificationRefusalReasons.MissingPurpose;
        }

        if (request.Finalidade is not NotificationPurposes.AccountConfirmation
            and not NotificationPurposes.PasswordRecovery
            and not NotificationPurposes.StaffInvitation
            and not NotificationPurposes.PurchaseReceipt)
        {
            return NotificationRefusalReasons.UnknownPurpose;
        }

        if (request.Modelo is not (
                NotificationPurposes.AccountConfirmation
                or NotificationPurposes.PasswordRecovery
                or NotificationPurposes.StaffInvitation
                or NotificationPurposes.PurchaseReceipt)
            || request.Modelo != request.Finalidade)
        {
            return NotificationRefusalReasons.UnknownModel;
        }

        if (request.Dados is null || string.IsNullOrWhiteSpace(request.Dados.Link)
            || !Uri.TryCreate(request.Dados.Link, UriKind.Absolute, out _))
        {
            return NotificationRefusalReasons.MissingData;
        }

        if (request.Modelo == NotificationPurposes.PurchaseReceipt)
        {
            if (request.DestinatarioConta is null) return NotificationRefusalReasons.InvalidFormat;
            var data = request.Dados;
            if (data.NumeroPedido is null || data.NumeroPedido.Length < 6 || !data.NumeroPedido.All(char.IsAsciiDigit)
                || string.IsNullOrWhiteSpace(data.Curso) || string.IsNullOrWhiteSpace(data.Opcao)
                || data.ValorCentavos is not > 0 || data.Meio is not ("card" or "pix" or "boleto")
                || data.PagoEm is null || data.PagoEm == default(DateTimeOffset)
                || data.Vigencia is null || !(data.Vigencia.Type == "months" && data.Vigencia.Months is >= 1 and <= 60
                    || data.Vigencia.Type == "lifetime" && data.Vigencia.Months is null))
                return NotificationRefusalReasons.MissingData;
        }
        else if (request.Modelo == NotificationPurposes.StaffInvitation)
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
