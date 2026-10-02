using CodeForCoders.Commerce.Infra.Data.Configuration;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Application.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Commerce.ArchitectureTests;

public sealed class ModuleSchemaConventionTest
{
    [Fact(DisplayName = nameof(CatalogAndSalesOutboxesHaveSeparateModuleSchemas))]
    [Obsolete]
    public void CatalogAndSalesOutboxesHaveSeparateModuleSchemas()
    {
        using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture").Options, new TenantContext());
        var catalog = db.Model.FindEntityType(typeof(CatalogOutboxMessage))!;
        var entitlement = db.Model.FindEntityType(typeof(EntitlementOutboxMessage))!;
        Assert.Equal(CommerceSchemas.Entitlement, entitlement.GetSchema());
        Assert.Equal("outbox_messages", entitlement.GetTableName());
        foreach (var type in db.Model.GetEntityTypes().Where(type => type.GetSchema() == CommerceSchemas.Entitlement))
            Assert.NotNull(type.GetQueryFilter());
        var sales = db.Model.FindEntityType(typeof(OutboxMessage))!;
        Assert.Equal(CommerceSchemas.Catalog, catalog.GetSchema());
        Assert.Equal(CommerceSchemas.Sales, sales.GetSchema());
        Assert.Equal("outbox_messages", catalog.GetTableName());
        Assert.Equal("outbox_messages", sales.GetTableName());
    }

    [Fact(DisplayName = nameof(CommerceDeclaresItsModulesAndSchemas))]
    [Trait("Architecture", "Modules and schemas")]
    public void CommerceDeclaresItsModulesAndSchemas()
    {
        Assert.Equal(["Catalog", "Sales", "Entitlement"], CommerceModules.All);
        Assert.Equal(["catalog", "sales", "entitlement"], CommerceSchemas.All);
        Assert.True(CommerceModules.EntitlementIsExtractionCandidate);
        Assert.Contains("BA04", CommerceModules.EntitlementExtractionCandidateReason);
    }
}
