namespace CodeForCoders.Notification.Application.Interfaces;

public interface IEmailTemplateSettings
{
    TimeZoneInfo SchoolTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    int GetLinkValidityHours(string purpose);
}
