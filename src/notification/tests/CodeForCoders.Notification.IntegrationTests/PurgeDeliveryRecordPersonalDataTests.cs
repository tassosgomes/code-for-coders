using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;
using CodeForCoders.Notification.Application.UseCases.Notifications.DeliverAcceptedNotification;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Data;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

[Collection(NotificationIntegrationCollection.Name)]
public sealed class PurgeDeliveryRecordPersonalDataTests(NotificationIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(PurgeRemovesPersonalDataAndPreservesOutcomeCounter))]
    public async Task PurgeRemovesPersonalDataAndPreservesOutcomeCounter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        var request = new NotificationSendRequestedV1(
            requestId,
            tenantId,
            "student@example.com",
            NotificationPurposes.AccountConfirmation,
            "modelo-inexistente",
            new NotificationTemplateDataV1(
                "Ana Souza",
                "https://accounts.example.invalid/confirm?token=abc123"),
            DateTimeOffset.UtcNow);
        using var host = CreateHost();

        await host.StartAsync(cancellationToken);
        try
        {
            Guid deliveryRecordId;
            DateOnly outcomeDay;
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var useCase = scope.ServiceProvider.GetRequiredService<IAcceptNotificationSendRequest>();
                var output = await useCase.ExecuteAsync(
                    new AcceptNotificationSendRequestInput(request, "integration-purge"),
                    cancellationToken);

                Assert.Equal(DeliveryStatus.Refused, output.Status);
                Assert.NotNull(output.RefusedOn);
                deliveryRecordId = output.DeliveryRecordId;
                outcomeDay = DateOnly.FromDateTime(output.RefusedOn!.Value.UtcDateTime);
            }

            var counterBeforePurge = await ReadCounterAsync(
                tenantId,
                request.Finalidade!,
                DeliveryStatus.Refused,
                outcomeDay,
                cancellationToken);
            Assert.Equal(1, counterBeforePurge.Count);

            var expiredOn = DateTimeOffset.UtcNow.AddDays(-2);
            await MoveRefusalBeforeCutoffAsync(deliveryRecordId, expiredOn, cancellationToken);

            var purgedRecord = await WaitForPurgedRecordAsync(
                tenantId,
                deliveryRecordId,
                cancellationToken);

            Assert.Equal(DeliveryStatus.Refused, purgedRecord.Status);
            Assert.Equal(request.Finalidade, purgedRecord.Purpose);
            Assert.NotNull(purgedRecord.RefusedOn);
            Assert.InRange(
                purgedRecord.RefusedOn!.Value,
                expiredOn - TimeSpan.FromMilliseconds(1),
                expiredOn + TimeSpan.FromMilliseconds(1));
            Assert.Null(purgedRecord.Recipient);
            Assert.Null(purgedRecord.RecipientName);
            Assert.Null(purgedRecord.Link);
            Assert.Null(purgedRecord.Reason);

            var counterAfterPurge = await ReadCounterAsync(
                tenantId,
                request.Finalidade!,
                DeliveryStatus.Refused,
                outcomeDay,
                cancellationToken);
            Assert.Equal(counterBeforePurge.Count, counterAfterPurge.Count);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = nameof(PurgeRemovesDeliveredRecordPersonalDataAndPreservesOutcomeCounter))]
    public async Task PurgeRemovesDeliveredRecordPersonalDataAndPreservesOutcomeCounter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        using var host = CreateHost(new SuccessfulEmailSender());

        await host.StartAsync(cancellationToken);
        try
        {
            Guid deliveryRecordId;
            DateTimeOffset deliveredOn;
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var request = CreateAcceptedRequest(tenantId, requestId);
                var accept = scope.ServiceProvider.GetRequiredService<IAcceptNotificationSendRequest>();
                var accepted = await accept.ExecuteAsync(
                    new AcceptNotificationSendRequestInput(request, "integration-purge-delivered"),
                    cancellationToken);
                Assert.Equal(DeliveryStatus.Accepted, accepted.Status);

                var deliver = scope.ServiceProvider.GetRequiredService<IDeliverAcceptedNotification>();
                var delivered = await deliver.ExecuteAsync(
                    new DeliverAcceptedNotificationInput(accepted.DeliveryRecordId),
                    cancellationToken);
                Assert.True(delivered.Delivered);

                deliveryRecordId = accepted.DeliveryRecordId;
                deliveredOn = delivered.DeliveredOn!.Value;
                var record = await scope.ServiceProvider
                    .GetRequiredService<NotificationDbContext>()
                    .DeliveryRecords
                    .AsNoTracking()
                    .SingleAsync(item => item.Id == deliveryRecordId, cancellationToken);
                Assert.Equal(DeliveryStatus.Delivered, record.Status);
                Assert.Null(record.Link);
            }

            var outcomeDay = DateOnly.FromDateTime(deliveredOn.UtcDateTime);
            var counterBeforePurge = await ReadCounterAsync(
                tenantId,
                NotificationPurposes.AccountConfirmation,
                DeliveryStatus.Delivered,
                outcomeDay,
                cancellationToken);
            Assert.Equal(1, counterBeforePurge.Count);

            var expiredOn = DateTimeOffset.UtcNow.AddDays(-2);
            await MoveFinalOutcomeBeforeCutoffAsync(
                deliveryRecordId,
                DeliveryStatus.Delivered,
                expiredOn,
                cancellationToken);

            var purgedRecord = await WaitForPurgedRecordAsync(
                tenantId,
                deliveryRecordId,
                cancellationToken);

            Assert.Equal(DeliveryStatus.Delivered, purgedRecord.Status);
            Assert.Equal(NotificationPurposes.AccountConfirmation, purgedRecord.Purpose);
            Assert.NotNull(purgedRecord.DeliveredOn);
            Assert.InRange(
                purgedRecord.DeliveredOn!.Value,
                expiredOn - TimeSpan.FromMilliseconds(1),
                expiredOn + TimeSpan.FromMilliseconds(1));
            Assert.Null(purgedRecord.Recipient);
            Assert.Null(purgedRecord.RecipientName);
            Assert.Null(purgedRecord.Link);
            Assert.Null(purgedRecord.Reason);

            var counterAfterPurge = await ReadCounterAsync(
                tenantId,
                NotificationPurposes.AccountConfirmation,
                DeliveryStatus.Delivered,
                outcomeDay,
                cancellationToken);
            Assert.Equal(counterBeforePurge.Count, counterAfterPurge.Count);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = nameof(PurgeRemovesFailedRecordPersonalDataAndPreservesOutcomeCounter))]
    public async Task PurgeRemovesFailedRecordPersonalDataAndPreservesOutcomeCounter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        using var host = CreateHost(new PermanentFailureEmailSender());

        await host.StartAsync(cancellationToken);
        try
        {
            Guid deliveryRecordId;
            DateTimeOffset failedOn;
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var request = CreateAcceptedRequest(tenantId, requestId);
                var accept = scope.ServiceProvider.GetRequiredService<IAcceptNotificationSendRequest>();
                var accepted = await accept.ExecuteAsync(
                    new AcceptNotificationSendRequestInput(request, "integration-purge-failed"),
                    cancellationToken);
                Assert.Equal(DeliveryStatus.Accepted, accepted.Status);

                var deliver = scope.ServiceProvider.GetRequiredService<IDeliverAcceptedNotification>();
                var failed = await deliver.ExecuteAsync(
                    new DeliverAcceptedNotificationInput(accepted.DeliveryRecordId),
                    cancellationToken);
                Assert.False(failed.Delivered);

                deliveryRecordId = accepted.DeliveryRecordId;
                var record = await scope.ServiceProvider
                    .GetRequiredService<NotificationDbContext>()
                    .DeliveryRecords
                    .AsNoTracking()
                    .SingleAsync(item => item.Id == deliveryRecordId, cancellationToken);
                Assert.Equal(DeliveryStatus.Failed, record.Status);
                Assert.NotNull(record.FailedOn);
                Assert.Equal(
                    NotificationFailureReasons.PermanentProviderFailure,
                    record.Reason);
                Assert.Null(record.Link);
                failedOn = record.FailedOn!.Value;
            }

            var outcomeDay = DateOnly.FromDateTime(failedOn.UtcDateTime);
            var counterBeforePurge = await ReadCounterAsync(
                tenantId,
                NotificationPurposes.AccountConfirmation,
                DeliveryStatus.Failed,
                outcomeDay,
                cancellationToken);
            Assert.Equal(1, counterBeforePurge.Count);

            var expiredOn = DateTimeOffset.UtcNow.AddDays(-2);
            await MoveFinalOutcomeBeforeCutoffAsync(
                deliveryRecordId,
                DeliveryStatus.Failed,
                expiredOn,
                cancellationToken);

            var purgedRecord = await WaitForPurgedRecordAsync(
                tenantId,
                deliveryRecordId,
                cancellationToken);

            Assert.Equal(DeliveryStatus.Failed, purgedRecord.Status);
            Assert.Equal(NotificationPurposes.AccountConfirmation, purgedRecord.Purpose);
            Assert.NotNull(purgedRecord.FailedOn);
            Assert.InRange(
                purgedRecord.FailedOn!.Value,
                expiredOn - TimeSpan.FromMilliseconds(1),
                expiredOn + TimeSpan.FromMilliseconds(1));
            Assert.Null(purgedRecord.Recipient);
            Assert.Null(purgedRecord.RecipientName);
            Assert.Null(purgedRecord.Link);
            Assert.Null(purgedRecord.Reason);

            var counterAfterPurge = await ReadCounterAsync(
                tenantId,
                NotificationPurposes.AccountConfirmation,
                DeliveryStatus.Failed,
                outcomeDay,
                cancellationToken);
            Assert.Equal(counterBeforePurge.Count, counterAfterPurge.Count);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private IHost CreateHost(ITransactionalEmailSender? emailSender = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["Retention:RetentionDays"] = "1",
            ["Retention:PollingIntervalMilliseconds"] = "5",
            ["Retention:BatchSize"] = "10",
            ["Valkey:ConnectionString"] = "localhost:6379,abortConnect=false",
        };

        return Host.CreateDefaultBuilder()
            .UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(configurationValues))
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                if (emailSender is not null)
                {
                    services.AddSingleton(emailSender);
                }
                services.AddSingleton<ITransactionalEmailRetryPolicy, TestRetryPolicy>();
            })
            .Build();
    }

    private static NotificationSendRequestedV1 CreateAcceptedRequest(Guid tenantId, Guid requestId)
        => new(
            requestId,
            tenantId,
            "student@example.com",
            NotificationPurposes.AccountConfirmation,
            NotificationPurposes.AccountConfirmation,
            new NotificationTemplateDataV1(
                "Ana Souza",
                $"https://accounts.example.invalid/confirm?token={requestId:N}"),
            DateTimeOffset.UtcNow);

    private async Task MoveRefusalBeforeCutoffAsync(
        Guid deliveryRecordId,
        DateTimeOffset expiredOn,
        CancellationToken cancellationToken)
    {
        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new NotificationDbContext(options, tenantContext);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"UPDATE {NotificationSchema.Name}.delivery_records SET refused_on = @p0 WHERE id = @p1",
            [expiredOn, deliveryRecordId],
            cancellationToken);
    }

    private async Task MoveFinalOutcomeBeforeCutoffAsync(
        Guid deliveryRecordId,
        DeliveryStatus status,
        DateTimeOffset expiredOn,
        CancellationToken cancellationToken)
    {
        var updateSql = status switch
        {
            DeliveryStatus.Refused
                => $"UPDATE {NotificationSchema.Name}.delivery_records SET refused_on = @p0 WHERE id = @p1",
            DeliveryStatus.Delivered
                => $"UPDATE {NotificationSchema.Name}.delivery_records SET delivered_on = @p0 WHERE id = @p1",
            DeliveryStatus.Failed
                => $"UPDATE {NotificationSchema.Name}.delivery_records SET failed_on = @p0 WHERE id = @p1",
            _ => throw new Xunit.Sdk.XunitException($"Unsupported final status: {status}"),
        };
        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new NotificationDbContext(options, tenantContext);
        await dbContext.Database.ExecuteSqlRawAsync(
            updateSql,
            [expiredOn, deliveryRecordId],
            cancellationToken);
    }

    private async Task<DeliveryRecord> WaitForPurgedRecordAsync(
        Guid tenantId,
        Guid deliveryRecordId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var tenantContext = new TenantContext();
            tenantContext.Set(tenantId);
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(fixture.PostgreSql.GetConnectionString())
                .Options;
            await using var dbContext = new NotificationDbContext(options, tenantContext);
            var record = await dbContext.DeliveryRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == deliveryRecordId, cancellationToken);
            if (record is not null
                && record.Recipient is null
                && record.RecipientName is null
                && record.Link is null
                && record.Reason is null)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException(
            $"Delivery record {deliveryRecordId} was not purged before the timeout.");
    }

    private async Task<DeliveryOutcomeCounter> ReadCounterAsync(
        Guid tenantId,
        string purpose,
        DeliveryStatus status,
        DateOnly outcomeDay,
        CancellationToken cancellationToken)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new NotificationDbContext(options, tenantContext);
        var counter = await dbContext.DeliveryOutcomeCounters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Purpose == purpose
                    && item.Status == status
                    && item.OutcomeDay == outcomeDay,
                cancellationToken);
        return counter
            ?? throw new Xunit.Sdk.XunitException(
                $"No outcome counter exists for {purpose}/{status}/{outcomeDay}.");
    }

    private sealed class SuccessfulEmailSender : ITransactionalEmailSender
    {
        public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class TestRetryPolicy : ITransactionalEmailRetryPolicy
    {
        public int MaxAttempts => 3;

        public TimeSpan GetBackoff(int attemptNumber)
            => TimeSpan.Zero;
    }

    private sealed class PermanentFailureEmailSender : ITransactionalEmailSender
    {
        public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
            => Task.FromException(
                new TransactionalEmailSendException(
                    NotificationFailureReasons.PermanentProviderFailure,
                    isTransient: false));
    }
}
