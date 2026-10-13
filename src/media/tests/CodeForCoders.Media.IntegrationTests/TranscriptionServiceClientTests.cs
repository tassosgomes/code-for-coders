using System.Text;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.SeedWork;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class TranscriptionServiceClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task SubmitSendsApiKeyReferenceAndIdempotencyThroughRealHttp()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        using var http = new HttpClient(); var client = Client(http, server); var id = Guid.CreateVersion7();
        Assert.Equal("trn_test", await client.SubmitAsync(new(id, 2, new Uri("https://storage.test/audio?signature=private")), Token));
        var submission = Assert.Single(server.Submissions);
        Assert.Equal($"c4c-{id:D}-2", submission.IdempotencyKey);
        Assert.Equal(id.ToString("D"), submission.ClientReference);
        Assert.Equal("https://storage.test/audio?signature=private", submission.SourceUrl);
    }

    [Fact]
    public async Task HttpPrivateSourceIsSentWithoutHttpsOnlyValidation()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        using var http = new HttpClient();
        await Client(http, server).SubmitAsync(new(Guid.CreateVersion7(), 1, new Uri("http://192.168.0.5:9000/audio")), Token);
        Assert.StartsWith("http://", Assert.Single(server.Submissions).SourceUrl);
    }

    [Fact]
    public async Task ReadsProcessingStatus()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Status = "processing";
        using var http = new HttpClient();
        Assert.Equal(new TranscriptionJob("processing", null), await Client(http, server).GetJobAsync("trn_test", Token));
    }

    [Fact]
    public async Task LeavesUnknownStatusForApplicationReconciliation()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Status = "future-state";
        using var http = new HttpClient();
        Assert.Equal(new TranscriptionJob("future-state", null), await Client(http, server).GetJobAsync("trn_test", Token));
    }

    [Fact]
    public async Task ReadsFailureCodeAndReportsUnknownJobAsNull()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Status = "failed"; server.FailureCode = "SOURCE_UNAVAILABLE";
        using var http = new HttpClient();
        Assert.Equal(new TranscriptionJob("failed", "SOURCE_UNAVAILABLE"), await Client(http, server).GetJobAsync("trn_test", Token));
        server.JobExpired = true;
        Assert.Null(await Client(http, server).GetJobAsync("trn_test", Token));
    }

    [Fact]
    public async Task ReadsResultWithoutChangingItsSchema()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        using var http = new HttpClient();
        var result = await Client(http, server).GetResultAsync("trn_test", Token);
        Assert.Equal(TranscriptionHttpSimulator.DefaultResult, Encoding.UTF8.GetString(result!));
    }

    [Fact]
    public async Task UnknownSchemaIsRejectedAfterRealHttpFetch()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Result = server.Result.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal);
        using var http = new HttpClient(); var result = await Client(http, server).GetResultAsync("trn_test", Token);
        Assert.Throws<EntityValidationException>(() => TranscriptionResultValidation.Validate(Guid.CreateVersion7(), Guid.CreateVersion7(), result!));
    }

    [Fact]
    public async Task ExpiredResultReturnsNull()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.ExpiredResult = true;
        using var http = new HttpClient(); Assert.Null(await Client(http, server).GetResultAsync("trn_test", Token));
    }

    [Fact]
    public async Task WrongCredentialPropagatesStatusWithoutBody()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        using var http = new HttpClient();
        var client = new TranscriptionServiceClient(http, Options.Create(new TranscriptionServiceOptions { BaseAddress = server.BaseAddress, ApiKey = "wrong-key" }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetJobAsync("trn_test", Token));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.DoesNotContain("wrong-key", error.Message);
    }

    [Fact]
    public async Task OversizedHttpResultIsBoundedBeforeParsing()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Result = new string('x', TranscriptionResultValidation.MaximumJsonBytes + 1);
        using var http = new HttpClient();
        await Assert.ThrowsAsync<EntityValidationException>(() => Client(http, server).GetResultAsync("trn_test", Token));
    }

    private static TranscriptionServiceClient Client(HttpClient http, TranscriptionHttpSimulator server)
        => new(http, Options.Create(new TranscriptionServiceOptions { BaseAddress = server.BaseAddress + "/", ApiKey = TranscriptionHttpSimulator.ApiKey }));
}
