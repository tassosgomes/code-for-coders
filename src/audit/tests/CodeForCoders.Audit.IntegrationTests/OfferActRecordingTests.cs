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
public sealed class OfferActRecordingTests(AuditIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(PublicationWithoutReasonIsConformingAndRetainsCourseReference))]
    public async Task PublicationWithoutReasonIsConformingAndRetainsCourseReference()
    {
        var act = Act(); await RecordAsync(act);
        await using var context = Context(); var record = await context.AuditRecords.SingleAsync(row => row.FactId == act.FatoId, TestContext.Current.CancellationToken);
        Assert.Equal("conforming", record.Conformity); Assert.Null(record.Reason); Assert.Equal("oferta", record.TargetType);
        Assert.Contains("curso", record.Complement!, StringComparison.Ordinal); Assert.Empty(record.Reasons);
    }

    [Fact(DisplayName = nameof(RedeliveredContentFactRecordsOnlyOneAct))]
    public async Task RedeliveredContentFactRecordsOnlyOneAct()
    {
        var act = Act(); await RecordAsync(act); await RecordAsync(act);
        await using var context = Context(); Assert.Equal(1, await context.AuditRecords.CountAsync(row => row.FactId == act.FatoId && row.Origin == "catalogo", TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(PublicationFromWrongOriginIsNonConforming))]
    public async Task PublicationFromWrongOriginIsNonConforming()
    {
        var act = Act() with { Origem = "identidade" }; await RecordAsync(act);
        await using var context = Context(); var record = await context.AuditRecords.SingleAsync(row => row.FactId == act.FatoId, TestContext.Current.CancellationToken);
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("tipo-desconhecido", record.Reasons);
    }

    [Fact(DisplayName = nameof(PublicationWithWrongTargetIsNonConformingAndTenantIsPreserved))]
    public async Task PublicationWithWrongTargetIsNonConformingAndTenantIsPreserved()
    {
        var act = Act() with { Alvo = new() { Tipo = "conta-interna", Id = Guid.CreateVersion7() } }; await RecordAsync(act);
        await using var context = Context(); var record = await context.AuditRecords.SingleAsync(row => row.FactId == act.FatoId, TestContext.Current.CancellationToken);
        Assert.Equal(act.TenantId, record.TenantId); Assert.Equal("non_conforming", record.Conformity); Assert.Contains("alvo-ausente", record.Reasons);
    }

    private AuditDbContext Context() => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(fixture.MigrationConnectionString).Options);
    private static AtoPraticado Act() => new()
    {
        FatoId = Guid.CreateVersion7(),
        Origem = "catalogo",
        Tipo = "oferta-publicada",
        TenantId = Guid.CreateVersion7(),
        PraticadoEm = DateTimeOffset.UtcNow,
        Autor = new() { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
        Alvo = new() { Tipo = "oferta", Id = Guid.CreateVersion7() },
        Complemento = new() { ["curso"] = Guid.CreateVersion7().ToString("D") },
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
