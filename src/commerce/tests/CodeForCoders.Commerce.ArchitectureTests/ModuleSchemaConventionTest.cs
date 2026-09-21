using CodeForCoders.Commerce.Infra.Data.Configuration;
using Xunit;

namespace CodeForCoders.Commerce.ArchitectureTests;

public sealed class ModuleSchemaConventionTest
{
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
