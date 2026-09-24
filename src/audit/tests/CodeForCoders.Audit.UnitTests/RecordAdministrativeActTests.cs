using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.UseCases.Audit.RecordAdministrativeAct;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Audit.UnitTests;

public sealed class RecordAdministrativeActTests
{
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = nameof(ExecuteAsyncAppendsConformingActBeforeCommit))]
    public async Task ExecuteAsyncAppendsConformingActBeforeCommit()
    {
        var writer = new SpyAuditRecordWriter();
        var unitOfWork = new SpyUnitOfWork(() => writer.Records.Count);
        var useCase = new RecordAdministrativeAct(
            writer,
            unitOfWork,
            new FixedTimeProvider(ReceivedOn));

        var output = await useCase.ExecuteAsync(
            new RecordAdministrativeActInput(CreateAct()),
            CancellationToken.None);

        var record = Assert.Single(writer.Records);
        Assert.Equal(Guid.Parse("6a030001-0000-7000-8000-000000000001"), record.FactId);
        Assert.Equal("identidade", record.Origin);
        Assert.Equal("papel-concedido", record.Type);
        Assert.Equal("conforming", record.Conformity);
        Assert.Empty(record.Reasons);
        Assert.Equal(ReceivedOn, record.ReceivedOn);
        Assert.Equal(64, record.Fingerprint.Length);
        Assert.Equal(1, unitOfWork.RecordCountAtCommit);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(ReceivedOn, output.ReceivedOn);
    }

    [Fact(DisplayName = nameof(ExecuteAsyncRejectsRequiredMissingReasonWithoutCommit))]
    public async Task ExecuteAsyncRejectsRequiredMissingReasonWithoutCommit()
    {
        var writer = new SpyAuditRecordWriter();
        var unitOfWork = new SpyUnitOfWork(() => writer.Records.Count);
        var useCase = new RecordAdministrativeAct(
            writer,
            unitOfWork,
            new FixedTimeProvider(ReceivedOn));
        var act = CreateAct() with { Motivo = null };

        await Assert.ThrowsAsync<EntityValidationException>(() => useCase.ExecuteAsync(
            new RecordAdministrativeActInput(act),
            CancellationToken.None));

        Assert.Empty(writer.Records);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    private static AtoPraticado CreateAct()
        => new()
        {
            FatoId = Guid.Parse("6a030001-0000-7000-8000-000000000001"),
            Origem = "identidade",
            Tipo = "papel-concedido",
            TenantId = Guid.Parse("6a030001-0000-7000-8000-000000000002"),
            PraticadoEm = new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
            Autor = new ReferenciaAto
            {
                Tipo = "conta-interna",
                Id = Guid.Parse("6a030001-0000-7000-8000-000000000003"),
            },
            Alvo = new ReferenciaAto
            {
                Tipo = "conta-interna",
                Id = Guid.Parse("6a030001-0000-7000-8000-000000000004"),
            },
            Complemento = new Dictionary<string, string> { ["papel"] = "professor" },
            Motivo = "Motivo unitário",
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
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

    private sealed class SpyUnitOfWork(Func<int> recordCount) : IUnitOfWork
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
