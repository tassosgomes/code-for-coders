using CodeForCoders.Learning.Infra.Data.Configuration;
using Xunit;

namespace CodeForCoders.Learning.ArchitectureTests;

public sealed class ModuleSchemaConventionTest
{
    [Fact(DisplayName = nameof(LearningDeclaresItsModulesAndSchemas))]
    [Trait("Architecture", "Modules and schemas")]
    public void LearningDeclaresItsModulesAndSchemas()
    {
        Assert.Equal(["Content", "Progress"], LearningModules.All);
        Assert.Equal(["content", "progress"], LearningSchemas.All);
    }
}
