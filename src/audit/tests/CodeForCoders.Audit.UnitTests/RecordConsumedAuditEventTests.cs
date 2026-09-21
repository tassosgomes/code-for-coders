using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using FluentValidation;
using Xunit;

namespace CodeForCoders.Audit.UnitTests;

public sealed class RecordConsumedAuditEventTests
{
    [Fact]
    public async Task ExecuteAsyncAppendsConsumedEventBeforeCommit()
    {
        var writer = new SpyAuditRecordWriter();
        var unitOfWork = new SpyUnitOfWork(() => writer.Records.Count);
        var useCase = new RecordConsumedAuditEvent(
            writer,
            unitOfWork,
            new RecordConsumedAuditEventInputValidator());
        var auditEvent = new AuditEventV1(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            "media",
            "MediaSmokeV1",
            "{}");

        var output = await useCase.ExecuteAsync(
            new RecordConsumedAuditEventInput(auditEvent),
            CancellationToken.None);

        var record = Assert.Single(writer.Records);
        Assert.Equal(auditEvent.EventId, record.Id);
        Assert.Equal(auditEvent.EventType, record.EventType);
        Assert.Equal(1, unitOfWork.RecordCountAtCommit);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(auditEvent.EventId, output.EventId);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsEmptyEventWithoutWritingOrCommitting()
    {
        var writer = new SpyAuditRecordWriter();
        var unitOfWork = new SpyUnitOfWork(() => writer.Records.Count);
        var useCase = new RecordConsumedAuditEvent(
            writer,
            unitOfWork,
            new RecordConsumedAuditEventInputValidator());

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(
            new RecordConsumedAuditEventInput(new AuditEventV1(
                Guid.Empty,
                Guid.CreateVersion7(),
                DateTimeOffset.UtcNow,
                "media",
                "MediaSmokeV1",
                "{}")),
            CancellationToken.None));

        Assert.Empty(writer.Records);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    private sealed class SpyAuditRecordWriter : IAuditRecordWriter
    {
        public List<AuditRecord> Records { get; } = [];

        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyUnitOfWork(Func<int> recordCount)
        : IUnitOfWork
    {
        public int CommitCount { get; private set; }

        public int RecordCountAtCommit { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            RecordCountAtCommit = recordCount();
            return Task.CompletedTask;
        }
    }
}
