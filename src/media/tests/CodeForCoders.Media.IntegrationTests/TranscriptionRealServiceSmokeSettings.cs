using Npgsql;

namespace CodeForCoders.Media.IntegrationTests;

internal sealed record TranscriptionRealServiceSmokeSettings(
    string ConnectionString, string VideoPath, string ApiKey, string S3Endpoint,
    string S3AccessKey, string S3SecretKey, TimeSpan Timeout)
{
    public const string ServiceAddress = "https://whisper.lab.tasso.dev.br/v1";
    public const string BucketName = "code-for-coders-media";

    public static TranscriptionRealServiceSmokeSettings Load()
    {
        string[] required = ["TRANSCRIPTION_SMOKE_CONNECTION_STRING", "TRANSCRIPTION_SMOKE_VIDEO_PATH",
            "WHISPER_API_KEY", "TRANSCRIPTION_SMOKE_S3_ENDPOINT", "TRANSCRIPTION_SMOKE_S3_ACCESS_KEY",
            "TRANSCRIPTION_SMOKE_S3_SECRET_KEY", "TRANSCRIPTION_SMOKE_PRIVATE_SOURCES_CONFIRMED"];
        var missing = required.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Missing real-service smoke variables: " + string.Join(", ", missing));
        if (Value("TRANSCRIPTION_SMOKE_PRIVATE_SOURCES_CONFIRMED") != "true")
            throw new InvalidOperationException("The operator must confirm TRUSTED_PRIVATE_SOURCES before this smoke.");
        var connection = Value("TRANSCRIPTION_SMOKE_CONNECTION_STRING");
        // Claims are global: even a unique tenant is unsafe in the application's database.
        if (!IsExclusiveDatabase(connection))
            throw new InvalidOperationException("The smoke requires an exclusive database ending in _transcription_smoke.");
        var path = Value("TRANSCRIPTION_SMOKE_VIDEO_PATH");
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase)
            || new FileInfo(path).Length is <= 0 or > 20 * 1024 * 1024)
            throw new InvalidOperationException("The smoke requires a nonempty MP4 with speech, at most 20 MiB.");
        var endpoint = Value("TRANSCRIPTION_SMOKE_S3_ENDPOINT");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new InvalidOperationException("The smoke S3 endpoint must be an absolute HTTP(S) origin without credentials.");
        var timeout = Environment.GetEnvironmentVariable("TRANSCRIPTION_SMOKE_TIMEOUT_MINUTES") ?? "30";
        if (!int.TryParse(timeout, out var minutes) || minutes is < 1 or > 1800)
            throw new InvalidOperationException("TRANSCRIPTION_SMOKE_TIMEOUT_MINUTES must be between 1 and 1800.");
        return new(connection, path, Value("WHISPER_API_KEY"), endpoint,
            Value("TRANSCRIPTION_SMOKE_S3_ACCESS_KEY"), Value("TRANSCRIPTION_SMOKE_S3_SECRET_KEY"), TimeSpan.FromMinutes(minutes));
    }

    private static string Value(string name) => Environment.GetEnvironmentVariable(name)!;

    private static bool IsExclusiveDatabase(string connectionString)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Database?
                .EndsWith("_transcription_smoke", StringComparison.Ordinal) == true;
        }
        catch (ArgumentException)
        {
            // Parsing errors can contain fragments of the secret connection string.
            return false;
        }
    }
}
