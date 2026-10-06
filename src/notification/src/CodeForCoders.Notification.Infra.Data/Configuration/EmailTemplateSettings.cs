using CodeForCoders.Notification.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class EmailTemplateSettings(IOptions<EmailOptions> options) : IEmailTemplateSettings
{
    public TimeZoneInfo SchoolTimeZone => TimeZoneInfo.FindSystemTimeZoneById(options.Value.SchoolTimeZone);
    public int GetLinkValidityHours(string purpose)
    {
        if (options.Value.ValidityHoursByPurpose.TryGetValue(purpose, out var validityHours)
            && validityHours > 0)
        {
            return validityHours;
        }

        throw new InvalidOperationException($"Email validity is not configured for purpose '{purpose}'.");
    }
}
