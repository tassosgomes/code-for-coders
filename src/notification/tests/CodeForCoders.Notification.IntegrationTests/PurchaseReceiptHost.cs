using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Notification.Api.Extensions;
using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Data;
using CodeForCoders.Notification.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

internal sealed class PurchaseReceiptHost : IAsyncDisposable
{
    private readonly IHost host;
    private readonly RSA key = RSA.Create(2048);
    public Guid TenantId { get; } = Guid.CreateVersion7();
    public Guid StudentId { get; } = Guid.CreateVersion7();
    public string ProcessingNamespace { get; } = "it-" + Guid.CreateVersion7().ToString("N");
    public string Email { get; } = $"{Guid.CreateVersion7()}@receipt.test";
    public ConcurrentQueue<string> Logs { get; } = new();
    public ReceiptContactHandler Contacts { get; } = new();
    public ReceiptEmailSender Sender { get; } = new();
    public IHost Host => host;
    public string Exchange => RabbitMqResourceNames.Compose("notification.receipt.events", ProcessingNamespace);

    public PurchaseReceiptHost(NotificationIntegrationFixture fixture)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["Notification:Namespace"] = ProcessingNamespace,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "notification.receipt.events",
            ["RabbitMq:DeadLetterExchange"] = "notification.receipt.dlx",
            ["RabbitMq:HeartbeatQueue"] = "notification.receipt.heartbeat",
            ["RabbitMq:SendRequestQueue"] = "notification.receipt.send-request",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Delivery:PollingIntervalSeconds"] = "1",
            ["Delivery:InitialBackoffMilliseconds"] = "100",
            ["Valkey:ConnectionString"] = "localhost:6379,abortConnect=false",
            ["StudentContactIdentity:BaseUrl"] = "http://identity.test/",
            ["StudentContactIdentity:SigningKeyBase64"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            ["Email:SchoolTimeZone"] = "America/Fortaleza",
        };
        Contacts.Response = _ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new StudentContact(StudentId, Email, "Ana Receipt", "active")) };
        host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().UseEnvironment("IntegrationTest")
            .ConfigureLogging(logging => logging.AddProvider(new ReceiptLogProvider(Logs)))
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values))
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddStudentContactClientConfiguration(context.Configuration);
                services.AddHttpClient<IStudentContactClient, CodeForCoders.Notification.Api.Clients.StudentContactClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => Contacts);
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                services.AddMessagingConfiguration(context.Configuration);
                services.AddNotificationMessageHandlers();
                services.AddSingleton<ITransactionalEmailSender>(Sender);
            }).Build();
    }

    public Task StartAsync() => host.StartAsync(TestContext.Current.CancellationToken);

    public NotificationSendRequestedV1 Request() => new(Guid.CreateVersion7(), TenantId, null, "comprovante-de-compra",
        "comprovante-de-compra", new(null, "http://localhost:8082/student/pedidos/01900000-0000-7000-8000-000000000001",
            NumeroPedido: "000123", Curso: ".NET <Avançado>", Opcao: "12 meses", ValorCentavos: 49700,
            Meio: "card", PagoEm: DateTimeOffset.Parse("2026-10-05T14:21:02Z"), Vigencia: new("months", 12)),
        DateTimeOffset.UtcNow, new("conta-aluno", StudentId));

    public async Task PublishAsync(NotificationSendRequestedV1 request)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var channel = await host.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(cancellation);
        await channel.BasicPublishAsync(Exchange, "notificacao.envio-solicitado.v1", false,
            new BasicProperties { ContentType = "application/json", CorrelationId = "receipt-proof" },
            JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellation);
    }

    public async Task<DeliveryRecord> WaitAsync(Guid requestId, Func<DeliveryRecord, bool> predicate)
    {
        var cancellation = TestContext.Current.CancellationToken;
        for (var i = 0; i < 180; i++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var record = await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().DeliveryRecords.AsNoTracking()
                .SingleOrDefaultAsync(r => r.RequestId == requestId, cancellation);
            if (record is not null && predicate(record)) return record;
            await Task.Delay(100, cancellation);
        }
        throw new TimeoutException("Receipt did not reach the expected delivery state.");
    }

    public async Task AssertEventAsync(string routingKey, Guid requestId, Action<JsonElement> assert)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var message = await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().OutboxMessages.AsNoTracking()
            .SingleAsync(m => m.RoutingKey == routingKey, TestContext.Current.CancellationToken);
        NotificationMessages.AssertSends(message.RoutingKey, message.Payload);
        using var json = JsonDocument.Parse(message.Payload);
        Assert.Equal(requestId, json.RootElement.GetProperty("pedidoId").GetGuid());
        assert(json.RootElement);
    }

    public void AssertAssertions()
    {
        Assert.NotEmpty(Contacts.Tokens);
        var jtis = new HashSet<string>();
        foreach (var token in Contacts.Tokens)
        {
            var parts = token.Split('.');
            Assert.True(key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            using var payload = JsonDocument.Parse(Decode(parts[1]));
            Assert.Equal("notification", payload.RootElement.GetProperty("iss").GetString());
            Assert.Equal("identity-internal", payload.RootElement.GetProperty("aud").GetString());
            Assert.Equal("student-contact:read", payload.RootElement.GetProperty("scope").GetString());
            Assert.Equal(TenantId, payload.RootElement.GetProperty("tenantId").GetGuid());
            Assert.True(jtis.Add(payload.RootElement.GetProperty("jti").GetString()!));
        }
    }

    private static byte[] Decode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(normalized.PadRight((normalized.Length + 3) / 4 * 4, '='));
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(CancellationToken.None);
        host.Dispose(); key.Dispose();
    }
}
