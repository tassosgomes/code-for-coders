using CodeForCoders.BffStudent.Application.Interfaces;
using CodeForCoders.BffStudent.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.BffStudent.Tests.Common;
using FluentValidation;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests;

public sealed class RecordPlatformHeartbeatTests
{
    [Fact]
    public async Task ExecuteAsync_AppendsHeartbeatToOutboxBeforeCommit()
    {
        var writer = new SpyOutboxMessageWriter();
        var unitOfWork = new SpyUnitOfWork(() => writer.Messages.Count);
        var useCase = new RecordPlatformHeartbeat(
            writer,
            unitOfWork,
            new RecordPlatformHeartbeatInputValidator());
        var input = BffStudentTestData.NewHeartbeatInput();

        var output = await useCase.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(writer.Messages);
        var message = writer.Messages[0];
        Assert.Equal(input.TenantId, message.TenantId);
        Assert.Equal(RecordPlatformHeartbeat.EventType, message.Type);
        Assert.Equal(RecordPlatformHeartbeat.RoutingKey, message.RoutingKey);
        Assert.Equal(output.EventId, message.Id);
        Assert.Equal(1, unitOfWork.MessageCountAtCommit);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(input.TenantId, output.TenantId);
        Assert.Equal(RecordPlatformHeartbeat.RoutingKey, output.RoutingKey);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyTenantWithoutWritingOrCommitting()
    {
        var writer = new SpyOutboxMessageWriter();
        var unitOfWork = new SpyUnitOfWork(() => writer.Messages.Count);
        var useCase = new RecordPlatformHeartbeat(
            writer,
            unitOfWork,
            new RecordPlatformHeartbeatInputValidator());

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new RecordPlatformHeartbeatInput(Guid.Empty), CancellationToken.None));

        Assert.Empty(writer.Messages);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    private sealed class SpyOutboxMessageWriter : IOutboxMessageWriter
    {
        public List<OutboxMessageDraft> Messages { get; } = [];

        public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyUnitOfWork(Func<int> messageCount)
        : IUnitOfWork
    {
        public int CommitCount { get; private set; }

        public int MessageCountAtCommit { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            MessageCountAtCommit = messageCount();
            return Task.CompletedTask;
        }
    }
}
