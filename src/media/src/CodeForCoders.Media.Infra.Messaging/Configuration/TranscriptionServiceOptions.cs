namespace CodeForCoders.Media.Infra.Messaging.Configuration;

public sealed class TranscriptionServiceOptions
{
    public const string SectionName = "Transcription:Service";
    public string BaseAddress { get; set; } = "https://whisper.lab.tasso.dev.br/v1";
    public string ApiKey { get; set; } = string.Empty;
    public bool IsValid() => !string.IsNullOrWhiteSpace(ApiKey)
        && Uri.TryCreate(BaseAddress, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
