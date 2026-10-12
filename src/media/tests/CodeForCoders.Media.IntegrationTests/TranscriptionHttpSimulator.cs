using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Media.IntegrationTests;

internal sealed class TranscriptionHttpSimulator : IAsyncDisposable
{
    public const string ApiKey = "transcription-test-api-key";
    public const string DefaultResult = """{"schemaVersion":1,"jobId":"trn_test","language":"pt-BR","durationMs":2000,"segments":[{"startMs":1000,"endMs":2000,"text":" second private lesson "},{"startMs":0,"endMs":1000,"text":" first private lesson "},{"startMs":2000,"endMs":2000,"text":" "}]}""";
    private readonly WebApplication app;
    private TranscriptionHttpSimulator(WebApplication app) => this.app = app;
    public string BaseAddress => app.Urls.Single() + "/v1";
    public string Status { get; set; } = "downloading";
    public string Result { get; set; } = DefaultResult;
    public bool ExpiredResult { get; set; }
    public bool FetchAudio { get; set; }
    public byte[]? Audio { get; private set; }
    public int SubmitStatusCode { get; set; } = 202;
    public ConcurrentQueue<Submission> Submissions { get; } = new();

    public static async Task<TranscriptionHttpSimulator> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "IntegrationTest" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var server = new TranscriptionHttpSimulator(app);
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers["X-API-Key"] != ApiKey) { context.Response.StatusCode = 401; return; }
            await next(context);
        });
        app.MapPost("/v1/transcriptions", async (HttpContext context) =>
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var root = document.RootElement;
            var source = root.GetProperty("sourceUrl").GetString()!;
            server.Submissions.Enqueue(new(context.Request.Headers["Idempotency-Key"].ToString(), source,
                root.GetProperty("clientReference").GetString()!));
            if (server.FetchAudio)
            {
                using var client = new HttpClient();
                server.Audio = await client.GetByteArrayAsync(source, context.RequestAborted);
            }
            return Results.Json(new
            {
                jobId = "trn_test",
                status = "downloading",
                createdAt = DateTimeOffset.UtcNow,
                statusUrl = "/v1/transcriptions/trn_test"
            }, statusCode: server.SubmitStatusCode);
        });
        app.MapGet("/v1/transcriptions/{jobId}", (string jobId) => Results.Json(new { jobId, status = server.Status }));
        app.MapGet("/v1/transcriptions/{jobId}/result", () => server.ExpiredResult ? Results.NotFound()
            : Results.Text(server.Result, "application/json"));
        await app.StartAsync(cancellationToken);
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync(CancellationToken.None);
        await app.DisposeAsync();
    }

    public sealed record Submission(string IdempotencyKey, string SourceUrl, string ClientReference);
}
