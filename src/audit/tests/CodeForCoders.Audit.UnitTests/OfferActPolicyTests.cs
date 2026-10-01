using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Audit.UnitTests;

public sealed class OfferActPolicyTests
{
    private static AdministrativeAct Act(string type = "oferta-publicada") => new(
        Guid.CreateVersion7(), "catalogo", type, Guid.CreateVersion7(), DateTimeOffset.UtcNow,
        new("conta-interna", Guid.CreateVersion7()), new("oferta", Guid.CreateVersion7()),
        new Dictionary<string, string> { ["curso"] = Guid.CreateVersion7().ToString("D") }, null);

    [Theory(DisplayName = nameof(AllOfferActsAreConformingWithoutReason))]
    [InlineData("oferta-publicada")]
    [InlineData("oferta-alterada")]
    [InlineData("oferta-despublicada")]
    public void AllOfferActsAreConformingWithoutReason(string type)
    {
        var record = AuditRecord.Create(Act(type), DateTimeOffset.UtcNow);
        Assert.Equal("conforming", record.Conformity); Assert.Null(record.Reason); Assert.Empty(record.Reasons);
        Assert.Equal("oferta", record.TargetType); Assert.NotNull(record.AuthorId);
    }

    [Fact(DisplayName = nameof(OfferTypeFromAnotherOriginIsNonConforming))]
    public void OfferTypeFromAnotherOriginIsNonConforming()
    {
        var record = AuditRecord.Create(Act() with { Origin = "conteudo" }, DateTimeOffset.UtcNow);
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("tipo-desconhecido", record.Reasons);
    }

    [Theory(DisplayName = nameof(CatalogOriginRequiresOfferTargetForEveryType))]
    [InlineData("oferta-publicada")]
    [InlineData("oferta-alterada")]
    [InlineData("oferta-despublicada")]
    [InlineData("versao-publicada")]
    public void CatalogOriginRequiresOfferTargetForEveryType(string type)
    {
        var record = AuditRecord.Create(Act(type) with { Target = new("curso", Guid.CreateVersion7()) }, DateTimeOffset.UtcNow);
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("alvo-ausente", record.Reasons);
    }
}
