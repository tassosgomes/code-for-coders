using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.Services;
using Xunit;

namespace CodeForCoders.Notification.UnitTests;

public sealed class MessageTemplateRendererTests
{
    private const string Recipient = "ana.souza@example.com";
    private const string ConfirmationLink = "https://accounts.example.invalid/confirm?token=abc123&return=%2Fhome";
    private const string RecoveryLink = "https://accounts.example.invalid/redefinir-senha?token=reset456";

    [Fact(DisplayName = nameof(AccountConfirmationRendersPortugueseTextAndHtmlWithConfiguredValidity))]
    [Trait("Unit", "MessageTemplateRenderer - Account confirmation")]
    public void AccountConfirmationRendersPortugueseTextAndHtmlWithConfiguredValidity()
    {
        var renderer = new MessageTemplateRenderer(new TestEmailTemplateSettings(24, 1));

        var email = renderer.Render(
            NotificationPurposes.AccountConfirmation,
            Recipient,
            "Ana Souza",
            ConfirmationLink);

        Assert.Equal("Confirme seu cadastro na Code4Coders", email.Subject);
        Assert.Contains("Oi, Ana!", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Falta um passo: confirme seu e-mail para entrar na Code4Coders.", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(ConfirmationLink, email.TextBody, StringComparison.Ordinal);
        Assert.Contains("O link vale por 24 horas e só funciona uma vez.", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Não criou esta conta? Ignore este e-mail.", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Souza", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Recipient, email.TextBody, StringComparison.Ordinal);

        var htmlBody = Assert.IsType<string>(email.HtmlBody);
        Assert.Contains("<html lang=\"pt-BR\">", htmlBody, StringComparison.Ordinal);
        Assert.Contains("width=\"600\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("max-width:600px", htmlBody, StringComparison.Ordinal);
        Assert.Contains("Confirmar meu e-mail", htmlBody, StringComparison.Ordinal);
        Assert.Contains("Ou copie este link no navegador:", htmlBody, StringComparison.Ordinal);
        Assert.Contains("https://accounts.example.invalid/confirm?token=abc123&amp;return=%2Fhome", htmlBody, StringComparison.Ordinal);
        Assert.Contains("O link vale por 24 horas e só funciona uma vez.", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Souza", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Recipient, htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", htmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = nameof(PasswordRecoveryUsesItsOwnConfiguredValidity))]
    [Trait("Unit", "MessageTemplateRenderer - Password recovery")]
    public void PasswordRecoveryUsesItsOwnConfiguredValidity()
    {
        var renderer = new MessageTemplateRenderer(new TestEmailTemplateSettings(24, 1));

        var email = renderer.Render(
            NotificationPurposes.PasswordRecovery,
            Recipient,
            "Ana Souza",
            RecoveryLink);

        Assert.Equal("Redefina sua senha da Code4Coders", email.Subject);
        Assert.Contains("Oi, Ana!", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Recebemos um pedido para criar uma nova senha para sua conta.", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(RecoveryLink, email.TextBody, StringComparison.Ordinal);
        Assert.Contains("O link vale por 1 hora e só funciona uma vez.", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("24 horas", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Não pediu? Ignore este e-mail — sua senha continua a mesma.", email.TextBody, StringComparison.Ordinal);

        var htmlBody = Assert.IsType<string>(email.HtmlBody);
        Assert.Contains("Criar nova senha", htmlBody, StringComparison.Ordinal);
        Assert.Contains("O link vale por 1 hora e só funciona uma vez.", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("24 horas", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Recipient, htmlBody, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(StaffInvitationRendersOfferedRoleLinkAndConfiguredValidity))]
    [Trait("Unit", "MessageTemplateRenderer - Staff invitation")]
    public void StaffInvitationRendersOfferedRoleLinkAndConfiguredValidity()
    {
        var renderer = new MessageTemplateRenderer(new TestEmailTemplateSettings(24, 1, 168));
        const string link = "https://backoffice.example.invalid/admin/convite?token=abc123";

        var email = renderer.Render(
            NotificationPurposes.StaffInvitation,
            Recipient,
            recipientName: null,
            link,
            recipientRole: "professor");

        Assert.Equal("Convite para acessar o backoffice da Code4Coders", email.Subject);
        Assert.Contains("como professor", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(link, email.TextBody, StringComparison.Ordinal);
        Assert.Contains("O link vale por 168 horas e só funciona uma vez.", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Recipient, email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("null", email.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("como professor", email.HtmlBody!, StringComparison.Ordinal);
        Assert.Contains("O link vale por 168 horas e só funciona uma vez.", email.HtmlBody!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(RenderHtmlEncodesPersonalizationAndLink))]
    [Trait("Unit", "MessageTemplateRenderer - Html encoding")]
    public void RenderHtmlEncodesPersonalizationAndLink()
    {
        var renderer = new MessageTemplateRenderer(new TestEmailTemplateSettings(24, 1));
        const string name = "<img/onerror=alert(1)>";
        const string link = "https://accounts.example.invalid/reset?token=abc\"&next=home";

        var email = renderer.Render(
            NotificationPurposes.PasswordRecovery,
            Recipient,
            name,
            link);

        var htmlBody = Assert.IsType<string>(email.HtmlBody);
        Assert.Contains("Oi, &lt;img/onerror=alert(1)&gt;!", htmlBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://accounts.example.invalid/reset?token=abc&quot;&amp;next=home\"", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<img/onerror=alert(1)>", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("onmouseover=", htmlBody, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestEmailTemplateSettings(
        int accountValidityHours,
        int recoveryValidityHours,
        int invitationValidityHours = 168)
        : IEmailTemplateSettings
    {
        public int GetLinkValidityHours(string purpose)
        {
            return purpose switch
            {
                NotificationPurposes.AccountConfirmation => accountValidityHours,
                NotificationPurposes.PasswordRecovery => recoveryValidityHours,
                NotificationPurposes.StaffInvitation => invitationValidityHours,
                _ => throw new InvalidOperationException($"Unexpected purpose: {purpose}"),
            };
        }
    }
}
