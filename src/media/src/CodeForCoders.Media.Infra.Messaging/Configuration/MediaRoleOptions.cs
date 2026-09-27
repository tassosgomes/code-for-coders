using Microsoft.Extensions.Configuration;

namespace CodeForCoders.Media.Infra.Messaging.Configuration;

public enum MediaServiceRole
{
    Api,
    Worker,
}

public sealed class MediaRoleOptions
{
    public const string SectionName = "Media";

    public const string RolePropertyName = "Role";

    public string Role { get; set; } = string.Empty;

    public static MediaServiceRole ReadRole(IConfiguration configuration)
    {
        var role = configuration.GetSection($"{SectionName}:{RolePropertyName}").Value;
        if (string.Equals(role, "api", StringComparison.OrdinalIgnoreCase))
        {
            return MediaServiceRole.Api;
        }

        if (string.Equals(role, "worker", StringComparison.OrdinalIgnoreCase))
        {
            return MediaServiceRole.Worker;
        }

        throw new InvalidOperationException(
            $"Media role is missing or invalid (current value: '{role ?? "<not set>"}'). " +
            $"Set {SectionName}__{RolePropertyName} to 'api' or 'worker'.");
    }
}
