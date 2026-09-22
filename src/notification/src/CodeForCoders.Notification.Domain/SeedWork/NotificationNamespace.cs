namespace CodeForCoders.Notification.Domain.SeedWork;

public static class NotificationNamespace
{
    public const int MaxLength = 100;

    public static bool IsValid(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Trim().Length <= MaxLength
            && !value.Any(character =>
                !char.IsLetterOrDigit(character)
                && character is not '-' and not '_' and not '.');

    public static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EntityValidationException("Notification namespace must not be empty.");
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            throw new EntityValidationException("Notification namespace is too long.");
        }

        if (normalized.Any(character =>
                !char.IsLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new EntityValidationException(
                "Notification namespace contains an unsupported character.");
        }

        return normalized;
    }
}
