using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Media.Infra.Data.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 2;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 10;

    // Explicit, tenant-scoped operator request; leave unset in normal deployments.
    public Guid? VideoFactReplayTenantId { get; set; }

    [Required]
    public string VideoFactReplayQueue { get; set; } = "learning.video-availability";

    public List<string> RetainedRoutingKeys { get; set; } = [];

    public Guid? ProgressFactReplayTenantId { get; set; }

    [Required]
    public string ProgressFactReplayQueue { get; set; } = "learning.playback-progress";
}
