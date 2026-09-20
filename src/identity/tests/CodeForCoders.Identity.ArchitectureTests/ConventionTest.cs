using ArchUnitNET.xUnitV3;
using CodeForCoders.Identity.Application.UseCases;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeForCoders.Identity.ArchitectureTests.ProjectArchitecture;

namespace CodeForCoders.Identity.ArchitectureTests;

public sealed class ConventionTest
{
    [Fact(DisplayName = nameof(UseCasesAreSealedAndLiveInUseCasesNamespace))]
    [Trait("Architecture", "Conventions")]
    public void UseCasesAreSealedAndLiveInUseCasesNamespace()
        => Classes().That().ImplementInterface(typeof(IUseCase<,>)).Or().ImplementInterface(typeof(IUseCase<>))
            .Should().BeSealed()
            .AndShould().Be(UseCases)
            .Check(Architecture);

    [Fact(DisplayName = nameof(NoMediatR))]
    [Trait("Architecture", "Conventions")]
    public void NoMediatR()
        => Types().That().Are(DomainLayer).Or().Are(ApplicationLayer).Or().Are(InfraLayer).Or().Are(ApiLayer)
            .Should().NotDependOnAny(MediatR)
            .Check(Architecture);

    [Fact(DisplayName = nameof(EndpointsDoNotUsePersistencePorts))]
    [Trait("Architecture", "Conventions")]
    public void EndpointsDoNotUsePersistencePorts()
        => Types().That().Are(Endpoints)
            .Should().NotDependOnAny(PersistencePorts)
            .Check(Architecture);

    [Fact(DisplayName = nameof(HttpContractsDoNotExposeDomainEntities))]
    [Trait("Architecture", "Conventions")]
    public void HttpContractsDoNotExposeDomainEntities()
        => Types().That().Are(ApiContracts)
            .Should().NotDependOnAny(DomainEntities)
            .Check(Architecture);
}
