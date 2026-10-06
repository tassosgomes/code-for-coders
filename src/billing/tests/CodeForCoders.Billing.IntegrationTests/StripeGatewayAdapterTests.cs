using System.Net;
using System.Text;
using System.Text.Json;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Infra.Gateway;
using CodeForCoders.Billing.Infra.Gateway.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Billing.IntegrationTests;

public sealed class StripeGatewayAdapterTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(CreatesCheckoutSessionWithExpectedFormParameters))]
    public async Task CreatesCheckoutSessionWithExpectedFormParameters()
    {
        var tenantId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            var responseJson = JsonSerializer.Serialize(new
            {
                id = "cs_test_adapter_1",
                url = "https://checkout.stripe.com/c/pay/cs_test_adapter_1",
                expires_at = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var options = Options.Create(new StripeGatewayOptions
        {
            SecretKey = "sk_test_mock_adapter_key",
            WebhookSigningSecret = "whsec_test"
        });

        var adapter = new StripeGatewayAdapter(httpClient, options, TimeProvider.System);
        var terms = new PaymentTerms(studentId, 49700, "BRL", ".NET do zero à API");
        var session = await adapter.OpenAsync(new(tenantId, orderId, terms, "https://app.code4coders.com.br/success", "https://app.code4coders.com.br/cancel"), Cancellation);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/v1/checkout/sessions", capturedRequest.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("sk_test_mock_adapter_key", capturedRequest.Headers.Authorization?.Parameter);

        Assert.NotNull(capturedBody);
        var form = capturedBody.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));

        Assert.Equal("payment", form["mode"]);
        Assert.Equal(orderId.ToString("D"), form["client_reference_id"]);
        Assert.Equal(tenantId.ToString("D"), form["metadata[tenantId]"]);
        Assert.Equal(orderId.ToString("D"), form["metadata[orderId]"]);
        Assert.Equal(tenantId.ToString("D"), form["payment_intent_data[metadata][tenantId]"]);
        Assert.Equal(orderId.ToString("D"), form["payment_intent_data[metadata][orderId]"]);
        Assert.Equal("1", form["line_items[0][quantity]"]);
        Assert.Equal("brl", form["line_items[0][price_data][currency]"]);
        Assert.Equal("49700", form["line_items[0][price_data][unit_amount]"]);
        Assert.Equal(".NET do zero à API", form["line_items[0][price_data][product_data][name]"]);
        Assert.Equal("card", form["payment_method_types[0]"]);
        Assert.Equal("pix", form["payment_method_types[1]"]);
        Assert.Equal("boleto", form["payment_method_types[2]"]);
        Assert.Equal("86400", form["payment_method_options[pix][expires_after_seconds]"]);
        Assert.Equal("3", form["payment_method_options[boleto][expires_after_days]"]);
        Assert.Equal("https://app.code4coders.com.br/success", form["success_url"]);
        Assert.Equal("https://app.code4coders.com.br/cancel", form["cancel_url"]);

        Assert.Equal("cs_test_adapter_1", session.Reference);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_adapter_1", session.PaymentUrl);
    }

    [Fact(DisplayName = nameof(PropagatesIdempotencyKeyHeader))]
    public async Task PropagatesIdempotencyKeyHeader()
    {
        var tenantId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();

        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            capturedRequest = req;
            var responseJson = JsonSerializer.Serialize(new
            {
                id = "cs_test_idemp",
                url = "https://checkout.stripe.com/c/pay/cs_test_idemp",
                expires_at = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var options = Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" });
        var adapter = new StripeGatewayAdapter(httpClient, options, TimeProvider.System);

        var terms = new PaymentTerms(Guid.CreateVersion7(), 49700, "BRL", "Curso");
        await adapter.OpenAsync(new(tenantId, orderId, terms, "https://app.code4coders.com.br/success", "https://app.code4coders.com.br/cancel"), Cancellation);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.TryGetValues("Idempotency-Key", out var values));
        Assert.Equal($"payment/{tenantId:D}/{orderId:D}", values.Single());
    }

    [Theory(DisplayName = nameof(Treats5xxOrNetworkErrorAsGatewayUnavailableException))]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Treats5xxOrNetworkErrorAsGatewayUnavailableException(HttpStatusCode status)
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("error") }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var options = Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" });
        var adapter = new StripeGatewayAdapter(httpClient, options, TimeProvider.System);

        var terms = new PaymentTerms(Guid.CreateVersion7(), 49700, "BRL", "Curso");
        await Assert.ThrowsAsync<GatewayUnavailableException>(() =>
            adapter.OpenAsync(new(Guid.CreateVersion7(), Guid.CreateVersion7(), terms, "https://app.code4coders.com.br/success", "https://app.code4coders.com.br/cancel"), Cancellation));
    }

    [Fact(DisplayName = nameof(GetInstructionsUrlAsyncReturnsPixHostedInstructionsUrl))]
    public async Task GetInstructionsUrlAsyncReturnsPixHostedInstructionsUrl()
    {
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("/v1/payment_intents/pi_test_pix_123", req.RequestUri?.AbsolutePath);
            var responseJson = JsonSerializer.Serialize(new
            {
                id = "pi_test_pix_123",
                next_action = new
                {
                    type = "pix_display_qr_code",
                    pix_display_qr_code = new
                    {
                        hosted_instructions_url = "https://payments.stripe.com/pix/instructions/test_123"
                    }
                }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var options = Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" });
        var adapter = new StripeGatewayAdapter(httpClient, options, TimeProvider.System);

        var url = await adapter.GetInstructionsUrlAsync("pi_test_pix_123", "pix", Cancellation);
        Assert.Equal("https://payments.stripe.com/pix/instructions/test_123", url);
    }

    [Fact(DisplayName = nameof(GetInstructionsUrlAsyncReturnsBoletoHostedVoucherUrl))]
    public async Task GetInstructionsUrlAsyncReturnsBoletoHostedVoucherUrl()
    {
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("/v1/payment_intents/pi_test_boleto_123", req.RequestUri?.AbsolutePath);
            var responseJson = JsonSerializer.Serialize(new
            {
                id = "pi_test_boleto_123",
                next_action = new
                {
                    type = "boleto_display_details",
                    boleto_display_details = new
                    {
                        hosted_voucher_url = "https://payments.stripe.com/boleto/voucher/test_456"
                    }
                }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var options = Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" });
        var adapter = new StripeGatewayAdapter(httpClient, options, TimeProvider.System);

        var url = await adapter.GetInstructionsUrlAsync("pi_test_boleto_123", "boleto", Cancellation);
        Assert.Equal("https://payments.stripe.com/boleto/voucher/test_456", url);
    }

    [Theory(DisplayName = nameof(GetPaymentMethodAsyncReadsTheMethodFromTheExpandedPaymentIntent))]
    [InlineData("card")]
    [InlineData("pix")]
    [InlineData("boleto")]
    public async Task GetPaymentMethodAsyncReadsTheMethodFromTheExpandedPaymentIntent(string type)
    {
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            Assert.Equal("/v1/payment_intents/pi_method_1", req.RequestUri?.AbsolutePath);
            Assert.Contains("expand[]=payment_method", Uri.UnescapeDataString(req.RequestUri!.Query));
            var responseJson = JsonSerializer.Serialize(new
            {
                id = "pi_method_1",
                payment_method_types = new[] { "card", "pix", "boleto" },
                payment_method = new { id = "pm_1", type }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var adapter = new StripeGatewayAdapter(httpClient, Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" }), TimeProvider.System);

        Assert.Equal(type, await adapter.GetPaymentMethodAsync("pi_method_1", Cancellation));
    }

    [Fact(DisplayName = nameof(GetPaymentMethodAsyncRefusesAnIntentWithoutAKnownMethod))]
    public async Task GetPaymentMethodAsyncRefusesAnIntentWithoutAKnownMethod()
    {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { id = "pi_x", payment_method_types = new[] { "card", "pix", "boleto" }, payment_method = (object?)null }), Encoding.UTF8, "application/json")
        }));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") };
        var adapter = new StripeGatewayAdapter(httpClient, Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" }), TimeProvider.System);

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.GetPaymentMethodAsync("pi_x", Cancellation));
    }

    [Fact(DisplayName = nameof(ExpireSessionAsyncPostsToTheExpireEndpointWithBearerAuthorization))]
    public async Task ExpireSessionAsyncPostsToTheExpireEndpointWithBearerAuthorization()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests, [(HttpStatusCode.OK, new { id = "cs_exp_1", status = "expired" })]));

        await adapter.ExpireSessionAsync("cs_exp_1", Cancellation);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/checkout/sessions/cs_exp_1/expire", request.Path);
        Assert.Equal("sk_test_key", request.Token);
    }

    [Theory(DisplayName = nameof(ExpireSessionAsyncAcceptsASessionThatStripeRefusesToExpireBecauseItIsAlreadyClosed))]
    [InlineData("expired")]
    [InlineData("complete")]
    public async Task ExpireSessionAsyncAcceptsASessionThatStripeRefusesToExpireBecauseItIsAlreadyClosed(string status)
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests,
        [
            (HttpStatusCode.BadRequest, new { error = new { type = "invalid_request_error", message = "Only Checkout Sessions with a status in ['open'] can be expired." } }),
            (HttpStatusCode.OK, new { id = "cs_closed_1", status })
        ]));

        await adapter.ExpireSessionAsync("cs_closed_1", Cancellation);

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[1].Method);
        Assert.Equal("/v1/checkout/sessions/cs_closed_1", requests[1].Path);
    }

    [Fact(DisplayName = nameof(ExpireSessionAsyncTreatsAMissingSessionAsAlreadyGone))]
    public async Task ExpireSessionAsyncTreatsAMissingSessionAsAlreadyGone()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests, [(HttpStatusCode.NotFound, new { error = new { code = "resource_missing" } })]));

        await adapter.ExpireSessionAsync("cs_gone_1", Cancellation);

        Assert.Single(requests);
    }

    [Fact(DisplayName = nameof(ExpireSessionAsyncFailsWhenTheRefusedSessionIsStillOpen))]
    public async Task ExpireSessionAsyncFailsWhenTheRefusedSessionIsStillOpen()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests,
        [
            (HttpStatusCode.BadRequest, new { error = new { type = "invalid_request_error" } }),
            (HttpStatusCode.OK, new { id = "cs_open_1", status = "open" })
        ]));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.ExpireSessionAsync("cs_open_1", Cancellation));
    }

    [Theory(DisplayName = nameof(ExpireSessionAsyncFailsWhenTheGatewayIsUnavailable))]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task ExpireSessionAsyncFailsWhenTheGatewayIsUnavailable(HttpStatusCode status)
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests, [(status, new { error = new { message = "already_expired canceled" } })]));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.ExpireSessionAsync("cs_down_1", Cancellation));
        Assert.Single(requests);
    }

    [Fact(DisplayName = nameof(ExpireSessionAsyncFailsOnNetworkError))]
    public async Task ExpireSessionAsyncFailsOnNetworkError()
    {
        var adapter = Adapter(new MockHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused")));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.ExpireSessionAsync("cs_net_1", Cancellation));
    }

    [Fact(DisplayName = nameof(CancelPaymentIntentAsyncPostsToTheCancelEndpointWithBearerAuthorization))]
    public async Task CancelPaymentIntentAsyncPostsToTheCancelEndpointWithBearerAuthorization()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests, [(HttpStatusCode.OK, new { id = "pi_1", status = "canceled" })]));

        await adapter.CancelPaymentIntentAsync("pi_1", Cancellation);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/payment_intents/pi_1/cancel", request.Path);
        Assert.Equal("sk_test_key", request.Token);
    }

    [Theory(DisplayName = nameof(CancelPaymentIntentAsyncAcceptsAnIntentThatStripeRefusesToCancel))]
    [InlineData("canceled")]
    [InlineData("succeeded")]
    [InlineData("requires_action")]
    [InlineData("processing")]
    public async Task CancelPaymentIntentAsyncAcceptsAnIntentThatStripeRefusesToCancel(string status)
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests,
        [
            (HttpStatusCode.BadRequest, new { error = new { type = "invalid_request_error", code = "payment_intent_unexpected_state" } }),
            (HttpStatusCode.OK, new { id = "pi_refused_1", status })
        ]));

        await adapter.CancelPaymentIntentAsync("pi_refused_1", Cancellation);

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[1].Method);
        Assert.Equal("/v1/payment_intents/pi_refused_1", requests[1].Path);
    }

    [Fact(DisplayName = nameof(CancelPaymentIntentAsyncFailsWhenTheRefusedIntentStillReportsAnUnknownStatus))]
    public async Task CancelPaymentIntentAsyncFailsWhenTheRefusedIntentStillReportsAnUnknownStatus()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests,
        [
            (HttpStatusCode.BadRequest, new { error = new { type = "invalid_request_error" } }),
            (HttpStatusCode.OK, new { id = "pi_unknown_1", status = "requires_capture" })
        ]));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.CancelPaymentIntentAsync("pi_unknown_1", Cancellation));
    }

    [Fact(DisplayName = nameof(CancelPaymentIntentAsyncTreatsAMissingIntentAsAlreadyGone))]
    public async Task CancelPaymentIntentAsyncTreatsAMissingIntentAsAlreadyGone()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests, [(HttpStatusCode.NotFound, new { error = new { code = "resource_missing" } })]));

        await adapter.CancelPaymentIntentAsync("pi_gone_1", Cancellation);

        Assert.Single(requests);
    }

    [Theory(DisplayName = nameof(CancelPaymentIntentAsyncFailsWhenTheGatewayIsUnavailable))]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CancelPaymentIntentAsyncFailsWhenTheGatewayIsUnavailable(HttpStatusCode status)
    {
        var requests = new List<(HttpMethod Method, string Path, string? Token)>();
        var adapter = Adapter(Respond(requests, [(status, new { error = new { message = "canceled" } })]));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.CancelPaymentIntentAsync("pi_down_1", Cancellation));
        Assert.Single(requests);
    }

    [Fact(DisplayName = nameof(CancelPaymentIntentAsyncFailsOnNetworkError))]
    public async Task CancelPaymentIntentAsyncFailsOnNetworkError()
    {
        var adapter = Adapter(new MockHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused")));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => adapter.CancelPaymentIntentAsync("pi_net_1", Cancellation));
    }

    private static StripeGatewayAdapter Adapter(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5999/") },
            Options.Create(new StripeGatewayOptions { SecretKey = "sk_test_key" }), TimeProvider.System);

    // Replies with the scripted responses in order and records what the adapter sent.
    private static MockHttpMessageHandler Respond(List<(HttpMethod Method, string Path, string? Token)> requests, (HttpStatusCode Status, object Body)[] script)
    {
        var index = 0;
        return new MockHttpMessageHandler((req, _) =>
        {
            requests.Add((req.Method, req.RequestUri!.AbsolutePath, req.Headers.Authorization?.Parameter));
            var (status, body) = script[Math.Min(index++, script.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            });
        });
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
