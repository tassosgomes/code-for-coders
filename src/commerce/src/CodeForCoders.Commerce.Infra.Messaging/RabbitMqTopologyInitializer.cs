using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CodeForCoders.Commerce.Infra.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
        await channel.ExchangeDeclareAsync(
            settings.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            settings.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        foreach (var exchange in settings.RoutingExchanges.Values.Distinct())
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, true, false, null, cancellationToken: cancellationToken);

        var deadLetterQueue = $"{settings.HeartbeatQueue}.dlq";
        await channel.QueueDeclareAsync(
            deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            deadLetterQueue,
            settings.DeadLetterExchange,
            settings.HeartbeatQueue,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            settings.HeartbeatQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = settings.HeartbeatQueue,
                ["x-delivery-limit"] = settings.DeliveryLimit,
            },
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(settings.EntitlementFactRetentionQueue, true, false, false,
            new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-max-length"] = settings.EntitlementFactRetentionMaxLength,
                ["x-message-ttl"] = settings.EntitlementFactRetentionTtlMilliseconds,
                ["x-overflow"] = "drop-head"
            }, cancellationToken: cancellationToken);
        foreach (var key in new[] { "matricula.acesso-concedido.v1", "matricula.acesso-expirado.v1" })
            await channel.QueueBindAsync(settings.EntitlementFactRetentionQueue, settings.Exchange, key, null, cancellationToken: cancellationToken);
        await DeclareCatalogAsync(channel, settings, cancellationToken);
        await DeclareEntitlementAsync(channel, settings, cancellationToken);
        await DeclarePurchasesAsync(channel, settings, cancellationToken);
        await channel.ExchangeDeclareAsync(settings.AuditExchange, ExchangeType.Topic, durable: true,
            autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(settings.OfferRetentionQueue, durable: true, exclusive: false,
            autoDelete: false, arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-max-length"] = settings.OfferRetentionMaxLength,
                ["x-message-ttl"] = settings.OfferRetentionTtlMilliseconds,
                ["x-overflow"] = "drop-head"
            }, cancellationToken: cancellationToken);
        foreach (var key in new[] { "catalogo.oferta-publicada.v1", "catalogo.oferta-alterada.v1", "catalogo.oferta-despublicada.v1" })
            await channel.QueueBindAsync(settings.OfferRetentionQueue, settings.Exchange, key,
                arguments: null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            settings.HeartbeatQueue,
            settings.Exchange,
            "commerce.platform.heartbeat.v1",
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private static async Task DeclareCatalogAsync(IChannel channel, RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(settings.LearningExchange, ExchangeType.Topic, true, false, null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync($"{settings.CatalogCourseQueue}.dlq", true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync($"{settings.CatalogCourseQueue}.dlq", settings.DeadLetterExchange,
            settings.CatalogCourseQueue, null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(settings.CatalogCourseQueue, true, false, false,
            new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = settings.CatalogCourseQueue,
                ["x-delivery-limit"] = settings.DeliveryLimit,
            }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(settings.CatalogCourseQueue, settings.LearningExchange,
            PublishedCourseFact.RoutingKey, null, cancellationToken: cancellationToken);
    }

    private static async Task DeclareEntitlementAsync(IChannel channel, RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(settings.LearningExchange, ExchangeType.Topic, true, false, null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync($"{settings.EntitlementCourseQueue}.dlq", true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync($"{settings.EntitlementCourseQueue}.dlq", settings.DeadLetterExchange,
            settings.EntitlementCourseQueue, null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(settings.EntitlementCourseQueue, true, false, false,
            new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = settings.EntitlementCourseQueue,
                ["x-delivery-limit"] = settings.DeliveryLimit,
            }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(settings.EntitlementCourseQueue, settings.LearningExchange,
            PublishedCourseFact.RoutingKey, null, cancellationToken: cancellationToken);
    }

    private static async Task DeclarePurchasesAsync(IChannel channel, RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(settings.BillingExchange, ExchangeType.Topic, true, false, null, cancellationToken: cancellationToken);
        foreach (var (queue, exchange, key) in new[] {
            (settings.SalesPaymentsQueue, settings.BillingExchange, "cobranca.pagamento-confirmado.v1"),
            (settings.EntitlementPurchasesQueue, settings.Exchange, "vendas.compra-concluida.v1"),
            (settings.SalesAccessGrantedQueue, settings.Exchange, "matricula.acesso-concedido.v1") })
        {
            await channel.QueueDeclareAsync($"{queue}.dlq", true, false, false,
                new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
            await channel.QueueBindAsync($"{queue}.dlq", settings.DeadLetterExchange, queue, null, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(queue, true, false, false, new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue,
                ["x-delivery-limit"] = settings.DeliveryLimit
            }, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queue, exchange, key, null, cancellationToken: cancellationToken);
        }
        await channel.QueueBindAsync(settings.SalesPaymentsQueue, settings.BillingExchange, "cobranca.pagamento-aguardando.v1", null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(settings.SalesPaymentsQueue, settings.BillingExchange, "cobranca.pagamento-nao-confirmado.v1", null, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
