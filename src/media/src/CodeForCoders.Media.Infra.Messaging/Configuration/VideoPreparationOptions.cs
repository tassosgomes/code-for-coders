using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace CodeForCoders.Media.Infra.Messaging.Configuration;

public sealed class VideoPreparationOptions
{
    public const string SectionName = "Preparation";

    public string WorkDirectory { get; set; } = "/var/lib/media-work";

    public int PollingIntervalSeconds { get; set; } = 10;

    public int MaxConcurrency { get; set; } = 1;

    public int Threads { get; set; } = 2;

    public int LeaseDurationSeconds { get; set; } = 300;

    public int LeaseRenewalIntervalSeconds { get; set; } = 60;

    public int CleanupBatchSize { get; set; } = 100;

    public string FfmpegPath { get; set; } = "ffmpeg";

    public string FfprobePath { get; set; } = "ffprobe";

    [Required]
    public string MasterKey { get; set; } = string.Empty;

    [Required]
    public string MasterKeyId { get; set; } = string.Empty;

    public bool HasValidMasterKey()
    {
        try
        {
            var decoded = Convert.FromBase64String(MasterKey);
            var valid = decoded.Length == 32;
            CryptographicOperations.ZeroMemory(decoded);
            return valid;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool HasValidWorkerSettings()
        => !string.IsNullOrWhiteSpace(WorkDirectory)
            && PollingIntervalSeconds is >= 1 and <= 300
            && MaxConcurrency is >= 1 and <= 8
            && Threads is >= 1 and <= 16
            && LeaseDurationSeconds is >= 30 and <= 3600
            && LeaseRenewalIntervalSeconds >= 5
            && LeaseRenewalIntervalSeconds < LeaseDurationSeconds
            && CleanupBatchSize is >= 1 and <= 500
            && !string.IsNullOrWhiteSpace(FfmpegPath)
            && !string.IsNullOrWhiteSpace(FfprobePath)
            && !string.IsNullOrWhiteSpace(MasterKeyId)
            && MasterKeyId.Length <= 64
            && HasValidMasterKey();
}
