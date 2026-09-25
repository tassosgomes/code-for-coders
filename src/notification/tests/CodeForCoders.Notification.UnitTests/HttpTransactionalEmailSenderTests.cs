using System.Net;
using System.Text.Json;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Data.Adapters;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Xunit;

namespace CodeForCoders.Notification.UnitTests;

public sealed class HttpTransactionalEmailSenderTests
{
    private static readonly TransactionalEmail Email = new(
        "destinatario@example.invalid",
        "Assunto",
        "Corpo em texto");

    [Fact]
    public async Task SendAsync_SuccessStatusCode_CompletesWithoutThrowing()
    {
        var sender = CreateSender((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));

        await sender.SendAsync(Email, CancellationToken.None);
    }

    [Fact(DisplayName = nameof(SendAsync_WithHtmlAlternate_MapsBothBodiesToProviderPayload))]
    [Trait("Unit", "HttpTransactionalEmailSender - Provider payload")]
    public async Task SendAsync_WithHtmlAlternate_MapsBothBodiesToProviderPayload()
    {
        string? capturedBody = null;
        var sender = CreateSender(
            async (request, cancellationToken) =>
            {
                capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            });
        var email = new TransactionalEmail(
            "destinatario@example.invalid",
            "Assunto",
            "Alternativa em texto",
            "<html><body>Alternativa HTML</body></html>");

        await sender.SendAsync(email, TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(Assert.IsType<string>(capturedBody));
        Assert.Equal("Alternativa em texto", payload.RootElement.GetProperty("text").GetString());
        Assert.Equal(
            "<html><body>Alternativa HTML</body></html>",
            payload.RootElement.GetProperty("html").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendAsync_TransientStatusCode_ThrowsTransientProviderUnavailable(HttpStatusCode statusCode)
    {
        var sender = CreateSender((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

        var exception = await Assert.ThrowsAsync<TransactionalEmailSendException>(
            () => sender.SendAsync(Email, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(NotificationFailureReasons.ProviderUnavailable, exception.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendAsync_PermanentStatusCode_ThrowsPermanentProviderFailure(HttpStatusCode statusCode)
    {
        var sender = CreateSender((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

        var exception = await Assert.ThrowsAsync<TransactionalEmailSendException>(
            () => sender.SendAsync(Email, CancellationToken.None));

        Assert.False(exception.IsTransient);
        Assert.Equal(NotificationFailureReasons.PermanentProviderFailure, exception.Reason);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_ThrowsTransientProviderUnavailable()
    {
        var innerException = new HttpRequestException("connection reset");
        var sender = CreateSender((_, _) => throw innerException);

        var exception = await Assert.ThrowsAsync<TransactionalEmailSendException>(
            () => sender.SendAsync(Email, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(NotificationFailureReasons.ProviderUnavailable, exception.Reason);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public async Task SendAsync_ProviderTimeout_ThrowsTransientProviderTimeout()
    {
        var sender = CreateSender(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            timeout: TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<TransactionalEmailSendException>(
            () => sender.SendAsync(Email, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(NotificationFailureReasons.ProviderTimeout, exception.Reason);
    }

    [Fact]
    public async Task SendAsync_PollyTimeoutRejectedException_ThrowsTransientProviderTimeout()
    {
        var resilienceException = new TimeoutRejectedException(TimeSpan.FromSeconds(1));
        var sender = CreateSender((_, _) => Task.FromException<HttpResponseMessage>(resilienceException));

        var exception = await Assert.ThrowsAsync<TransactionalEmailSendException>(
            () => sender.SendAsync(Email, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(NotificationFailureReasons.ProviderTimeout, exception.Reason);
        Assert.Same(resilienceException, exception.InnerException);
    }

    [Fact]
    public async Task SendAsync_PollyExecutionRejectedException_ThrowsTransientProviderUnavailable()
    {
        ExecutionRejectedException resilienceException = new BrokenCircuitException(
            "resilience rejected the request");
        var sender = CreateSender((_, _) => Task.FromException<HttpResponseMessage>(resilienceException));

        var exception = await Assert.ThrowsAsync<TransactionalEmailSendException>(
            () => sender.SendAsync(Email, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(NotificationFailureReasons.ProviderUnavailable, exception.Reason);
        Assert.Same(resilienceException, exception.InnerException);
    }

    private static HttpTransactionalEmailSender CreateSender(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(respond))
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
        var options = Options.Create(new EmailOptions
        {
            Endpoint = "https://email.example.invalid/v1/send",
            FromAddress = "notifications@example.invalid",
        });

        return new HttpTransactionalEmailSender(httpClient, options);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
