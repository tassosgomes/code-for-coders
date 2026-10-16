using System.Net;
using System.Text;
using CodeForCoders.BffStudent.Api.Captcha;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Captcha;

public sealed class TurnstileVerifierTests
{
    [Fact]
    public async Task VerifyAsync_SendsSecretResponseAndRemoteIpAsForm_AndReturnsPassed()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"success":true}"""));
        var verifier = Create(handler);

        var result = await verifier.VerifyAsync("tok-123", "203.0.113.9", TestContext.Current.CancellationToken);

        Assert.Equal(TurnstileOutcome.Passed, result.Outcome);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(TurnstileOptions.DefaultVerifyUrl, request.Uri);
        Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
        Assert.Contains("secret=test-secret", request.Body);
        Assert.Contains("response=tok-123", request.Body);
        Assert.Contains("remoteip=203.0.113.9", request.Body);
    }

    [Fact]
    public async Task VerifyAsync_OmitsRemoteIpWhenUnknown()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"success":true}"""));

        await Create(handler).VerifyAsync("tok", null, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("remoteip", Assert.Single(handler.Requests).Body);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFailedWithErrorCodes()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"success":false,"error-codes":["timeout-or-duplicate"]}"""));

        var result = await Create(handler).VerifyAsync("tok", null, TestContext.Current.CancellationToken);

        Assert.Equal(TurnstileOutcome.Failed, result.Outcome);
        Assert.Equal(["timeout-or-duplicate"], result.ErrorCodes);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.BadGateway, "")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "null")]
    public async Task VerifyAsync_ReturnsUnavailableForServerErrorOrInvalidBody(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(_ => Json(status, body));

        var result = await Create(handler).VerifyAsync("tok", null, TestContext.Current.CancellationToken);

        Assert.Equal(TurnstileOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsUnavailableOnNetworkError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));

        var result = await Create(handler).VerifyAsync("tok", null, TestContext.Current.CancellationToken);

        Assert.Equal(TurnstileOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsUnavailableOnTimeout()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timeout"));

        var result = await Create(handler).VerifyAsync("tok", null, TestContext.Current.CancellationToken);

        Assert.Equal(TurnstileOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task VerifyAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new StubHandler(_ => throw new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Create(handler).VerifyAsync("tok", null, cts.Token));
    }

    private static TurnstileVerifier Create(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            Options.Create(new TurnstileOptions { Enabled = true, SecretKey = "test-secret" }),
            NullLogger<TurnstileVerifier>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string? ContentType, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.ToString(), request.Content?.Headers.ContentType?.MediaType, body));
            cancellationToken.ThrowIfCancellationRequested();
            return respond(request);
        }
    }
}
