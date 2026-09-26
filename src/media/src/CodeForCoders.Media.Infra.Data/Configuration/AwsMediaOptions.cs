using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Media.Infra.Data.Configuration;

public sealed class AwsMediaOptions
{
    public const string SectionName = "AwsMedia";

    [Required]
    public string Region { get; set; } = "us-east-1";

    [Required]
    public string BucketName { get; set; } = "code-for-coders-media";

    [Required]
    public string ObjectKeyPrefix { get; set; } = "media";

    public string? EndpointInternal { get; set; }

    public string? EndpointPublic { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public bool ForcePathStyle { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 30;
}
