using System.Net;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Domain.SeedWork;

namespace CodeForCoders.Notification.Application.Services;

public sealed class MessageTemplateRenderer(IEmailTemplateSettings settings) : IMessageTemplateRenderer
{
    private const string Footer = "Code4Coders · e-mail automático";

    public TransactionalEmail Render(
        string model,
        string recipient,
        string? recipientName,
        string link,
        string? recipientRole = null)
    {
        return model switch
        {
            NotificationPurposes.AccountConfirmation => RenderAccountConfirmation(
                recipient,
                recipientName ?? string.Empty,
                link),
            NotificationPurposes.PasswordRecovery => RenderPasswordRecovery(
                recipient,
                recipientName ?? string.Empty,
                link),
            NotificationPurposes.StaffInvitation => RenderStaffInvitation(
                recipient,
                recipientRole,
                link),
            _ => throw new EntityValidationException(
                "The notification model is not supported by this slice."),
        };
    }

    private TransactionalEmail RenderAccountConfirmation(
        string recipient,
        string recipientName,
        string link)
    {
        var validityText = GetValidityText(NotificationPurposes.AccountConfirmation);
        var greeting = GetGreeting(recipientName);
        const string introduction = "Falta um passo: confirme seu e-mail para entrar na Code4Coders.";
        const string action = "Confirmar meu e-mail";
        const string notice = "Não criou esta conta? Ignore este e-mail.";
        var textBody = CreateTextBody(greeting, introduction, action, link, validityText, notice);
        var htmlBody = CreateHtmlBody(greeting, introduction, action, link, validityText, notice);

        return new TransactionalEmail(
            recipient,
            "Confirme seu cadastro na Code4Coders",
            textBody,
            htmlBody);
    }

    private TransactionalEmail RenderPasswordRecovery(
        string recipient,
        string recipientName,
        string link)
    {
        var validityText = GetValidityText(NotificationPurposes.PasswordRecovery);
        var greeting = GetGreeting(recipientName);
        const string introduction = "Recebemos um pedido para criar uma nova senha para sua conta.";
        const string action = "Criar nova senha";
        const string notice = "Não pediu? Ignore este e-mail — sua senha continua a mesma.";
        var textBody = CreateTextBody(greeting, introduction, action, link, validityText, notice);
        var htmlBody = CreateHtmlBody(greeting, introduction, action, link, validityText, notice);

        return new TransactionalEmail(
            recipient,
            "Redefina sua senha da Code4Coders",
            textBody,
            htmlBody);
    }

    private TransactionalEmail RenderStaffInvitation(string recipient, string? recipientRole, string link)
    {
        if (string.IsNullOrWhiteSpace(recipientRole))
        {
            throw new EntityValidationException("The staff invitation role is required.");
        }

        var validityText = GetValidityText(NotificationPurposes.StaffInvitation);
        var greeting = GetGreeting(null);
        var introduction = $"Você recebeu um convite para acessar o backoffice da Code4Coders como {recipientRole}.";
        const string action = "Aceitar convite";
        const string notice = "Não esperava este convite? Ignore este e-mail.";
        var textBody = CreateTextBody(greeting, introduction, action, link, validityText, notice);
        var htmlBody = CreateHtmlBody(greeting, introduction, action, link, validityText, notice);

        return new TransactionalEmail(
            recipient,
            "Convite para acessar o backoffice da Code4Coders",
            textBody,
            htmlBody);
    }

    private static string CreateTextBody(
        string greeting,
        string introduction,
        string action,
        string link,
        string validityText,
        string notice)
    {
        return $"{greeting}\n\n"
            + $"{introduction}\n\n"
            + $"{action}:\n{link}\n\n"
            + $"O link vale por {validityText} e só funciona uma vez.\n\n"
            + $"{notice}\n\n"
            + Footer;
    }

    private static string CreateHtmlBody(
        string greeting,
        string introduction,
        string action,
        string link,
        string validityText,
        string notice)
    {
        var encodedGreeting = WebUtility.HtmlEncode(greeting);
        var encodedLink = WebUtility.HtmlEncode(link);
        var encodedAction = WebUtility.HtmlEncode(action);
        var encodedIntroduction = WebUtility.HtmlEncode(introduction);
        var encodedNotice = WebUtility.HtmlEncode(notice);
        var encodedValidity = WebUtility.HtmlEncode(validityText);
        var encodedFooter = WebUtility.HtmlEncode(Footer);

        return "<!doctype html>"
            + "<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">"
            + "<title>Code4Coders</title></head>"
            + "<body style=\"margin:0;padding:0;background-color:#f3f5f7;color:#1b2333;"
            + "font-family:Arial,Helvetica,sans-serif;\">"
            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" "
            + "border=\"0\" bgcolor=\"#f3f5f7\" style=\"width:100%;background-color:#f3f5f7;\"><tr>"
            + "<td align=\"center\" style=\"padding:24px 12px;\">"
            + "<table role=\"presentation\" width=\"600\" cellspacing=\"0\" cellpadding=\"0\" "
            + "border=\"0\" bgcolor=\"#ffffff\" style=\"width:100%;max-width:600px;"
            + "background-color:#ffffff;border:1px solid #e1e5ec;\"><tr>"
            + "<td style=\"padding:24px 32px;border-bottom:1px solid #e1e5ec;"
            + "font-size:18px;font-weight:700;color:#1b2333;\">"
            + "<span style=\"color:#5b5bd6;\">&lt;/&gt;</span> Code4Coders</td></tr>"
            + "<tr><td style=\"padding:32px;font-size:16px;line-height:1.5;color:#1b2333;\">"
            + $"<p style=\"margin:0 0 16px;font-size:20px;font-weight:700;\">{encodedGreeting}</p>"
            + $"<p style=\"margin:0 0 24px;\">{encodedIntroduction}</p>"
            + "<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\"><tr>"
            + "<td bgcolor=\"#5b5bd6\" style=\"border-radius:6px;background-color:#5b5bd6;\">"
            + $"<a href=\"{encodedLink}\" style=\"display:inline-block;padding:12px 20px;"
            + "color:#ffffff;text-decoration:none;font-weight:700;\">"
            + $"{encodedAction}</a></td></tr></table>"
            + "<p style=\"margin:24px 0 8px;\">Ou copie este link no navegador:</p>"
            + $"<p style=\"margin:0 0 24px;overflow-wrap:anywhere;word-break:break-word;\">"
            + $"<a href=\"{encodedLink}\" style=\"color:#3f3faf;overflow-wrap:anywhere;"
            + $"word-break:break-word;\">{encodedLink}</a></p>"
            + $"<p style=\"margin:0 0 24px;color:#4b5565;\">O link vale por {encodedValidity} "
            + "e só funciona uma vez.</p>"
            + $"<p style=\"margin:0;color:#4b5565;\">{encodedNotice}</p>"
            + "</td></tr>"
            + $"<tr><td style=\"padding:16px 32px;border-top:1px solid #e1e5ec;"
            + $"font-size:12px;color:#667085;\">{encodedFooter}</td></tr>"
            + "</table></td></tr></table></body></html>";
    }

    private string GetValidityText(string purpose)
    {
        var validityHours = settings.GetLinkValidityHours(purpose);
        return validityHours == 1
            ? "1 hora"
            : $"{validityHours} horas";
    }

    private static string GetGreeting(string? recipientName)
    {
        var firstName = recipientName?
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return firstName is null
            ? "Olá!"
            : $"Oi, {firstName}!";
    }
}
