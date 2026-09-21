namespace CodeForCoders.Notification.Application.Interfaces;

public interface IEmailTemplateSettings
{
    int GetLinkValidityHours(string purpose);
}
