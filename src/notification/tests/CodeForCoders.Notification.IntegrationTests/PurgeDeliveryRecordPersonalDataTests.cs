using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;
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

    private IHost CreateHost()
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
            })
            .Build();
    }

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
}
