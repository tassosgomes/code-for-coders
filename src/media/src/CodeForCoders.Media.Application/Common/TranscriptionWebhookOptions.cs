namespace CodeForCoders.Media.Application.Common;

public sealed class TranscriptionWebhookOptions
{
    public const string SectionName = "Transcription:Webhook";
    public string[] Secrets { get; set; } = [];
    public bool IsConfigured => Secrets.Any(secret => !string.IsNullOrWhiteSpace(secret));

    public bool HasValidSecrets()
    {
        var configured = Secrets.Where(secret => !string.IsNullOrWhiteSpace(secret)).ToArray();
        if (configured.Length > 2) return false;
        foreach (var secret in configured)
        {
            if (!secret.StartsWith("whsec_", StringComparison.Ordinal)) return false;
            var buffer = new byte[secret.Length];
            if (!Convert.TryFromBase64String(secret[6..], buffer, out var length) || length == 0) return false;
        }
        return true;
    }
}
