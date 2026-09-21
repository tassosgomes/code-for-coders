using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Media.Infra.Data.Configuration;

/// <summary>
/// Secretless AWS wiring. Credentials come from the AWS default credential chain or workload identity.
/// </summary>
public sealed class AwsMediaOptions
{
    public const string SectionName = "AwsMedia";

    [Required]
    public string Region { get; set; } = "us-east-1";

    [Required]
    public string BucketName { get; set; } = "code-for-coders-media";

    [Required]
    public string CloudFrontDistributionDomain { get; set; } = "media.example.invalid";

    [Required]
    public string ObjectKeyPrefix { get; set; } = "media";
}
