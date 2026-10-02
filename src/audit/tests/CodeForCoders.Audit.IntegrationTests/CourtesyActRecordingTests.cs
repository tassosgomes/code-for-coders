using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Application.UseCases.Audit.RecordAdministrativeAct;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

[Collection(AuditIntegrationCollection.Name)]
public sealed class CourtesyActRecordingTests(AuditIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(CourtesyIsConformingAndRedeliveryRecordsItOnce))]
    public async Task CourtesyIsConformingAndRedeliveryRecordsItOnce()
    {
        var act = Act(); await RecordAsync(act); await RecordAsync(act);
        await using var context = Context(); var records = await context.AuditRecords.Where(row => row.FactId == act.FatoId).ToListAsync(TestContext.Current.CancellationToken);
        var record = Assert.Single(records); Assert.Equal("conforming", record.Conformity); Assert.Equal(act.Motivo, record.Reason);
        Assert.Equal("conta-aluno", record.TargetType); Assert.Contains("concessao", record.Complement!);
    }
    [Fact(DisplayName = nameof(NonConformingCourtesyIsPersistedRatherThanDiscarded))]
    public async Task NonConformingCourtesyIsPersistedRatherThanDiscarded()
    {
        var act = Act() with { Motivo = null, Origem = "catalogo", Alvo = new() { Tipo = "curso", Id = Guid.CreateVersion7() } };
        await RecordAsync(act); await using var context = Context(); var record = await context.AuditRecords.SingleAsync(row => row.FactId == act.FatoId, TestContext.Current.CancellationToken);
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("motivo-ausente", record.Reasons); Assert.Contains("tipo-desconhecido", record.Reasons); Assert.Contains("alvo-ausente", record.Reasons);
    }

    private AuditDbContext Context() => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(fixture.MigrationConnectionString).Options);
    private static AtoPraticado Act() => new()
    {
        FatoId = Guid.CreateVersion7(),
        Origem = "matricula",
        Tipo = "cortesia-concedida",
        TenantId = Guid.CreateVersion7(),
        PraticadoEm = DateTimeOffset.UtcNow,
        Autor = new() { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
        Alvo = new() { Tipo = "conta-aluno", Id = Guid.CreateVersion7() },
        Motivo = "Bolsa de mentoria",
        Complemento = new() { ["curso"] = Guid.CreateVersion7().ToString("D"), ["concessao"] = Guid.CreateVersion7().ToString("D"), ["vigencia"] = "6m" },
    };

    private async Task RecordAsync(AtoPraticado act)
    {
        using var host = Host.CreateDefaultBuilder().UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = fixture.RuntimeConnectionString,
                ["AuditDatabase:WriterRole"] = AuditIntegrationFixture.WriterRole,
                ["AuditSnapshots:ConnectionString"] = "localhost:6379,abortConnect=false",
            }))
            .ConfigureServices((context, services) => { services.AddApplicationConfiguration(); services.AddDataConfiguration(context.Configuration, context.HostingEnvironment); }).Build();
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRecordAdministrativeAct>().ExecuteAsync(new(act), TestContext.Current.CancellationToken);
    }
}
