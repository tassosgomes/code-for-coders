using CodeForCoders.Audit.Domain.Entities;
using Xunit;

namespace CodeForCoders.Audit.UnitTests;

public sealed class OfferChangedActPolicyTests
{
    private static AuditRecord Record(Dictionary<string, string> pairs)
    {
        pairs["curso"] = Guid.CreateVersion7().ToString("D");
        return AuditRecord.Create(new(Guid.CreateVersion7(), "catalogo", "oferta-alterada", Guid.CreateVersion7(), DateTimeOffset.UtcNow,
            new("conta-interna", Guid.CreateVersion7()), new("oferta", Guid.CreateVersion7()), pairs, null), DateTimeOffset.UtcNow);
    }

    [Theory(DisplayName = nameof(CompleteChangedPairsAreConformingWithoutReason))]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CompleteChangedPairsAreConformingWithoutReason(bool price, bool period)
    {
        var pairs = new Dictionary<string, string>();
        if (price) { pairs["precoAnterior"] = "49700"; pairs["precoNovo"] = "39700"; }
        if (period) { pairs["vigenciaAnterior"] = "12m"; pairs["vigenciaNova"] = "vitalicia"; }
        var record = Record(pairs);
        Assert.Equal("conforming", record.Conformity); Assert.Null(record.Reason); Assert.Empty(record.Reasons);
        foreach (var (key, value) in pairs) Assert.Contains($"\"{key}\":\"{value}\"", record.Complement!);
    }

    [Theory(DisplayName = nameof(IncompletePairIsNonConforming))]
    [InlineData("precoAnterior", "49700")]
    [InlineData("precoNovo", "39700")]
    [InlineData("vigenciaAnterior", "12m")]
    [InlineData("vigenciaNova", "vitalicia")]
    public void IncompletePairIsNonConforming(string key, string value)
    {
        var record = Record(new() { [key] = value });
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("complemento-invalido", record.Reasons);
    }

    [Theory(DisplayName = nameof(InvalidPeriodFormatIsNonConforming))]
    [InlineData("12 meses")]
    [InlineData("lifetime")]
    [InlineData("0m")]
    [InlineData("61m")]
    [InlineData("01m")]
    public void InvalidPeriodFormatIsNonConforming(string value)
    {
        var record = Record(new() { ["vigenciaAnterior"] = value, ["vigenciaNova"] = "vitalicia" });
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("complemento-invalido", record.Reasons);
    }

    [Theory(DisplayName = nameof(PricePairUsesPositiveWholeCentsInText))]
    [InlineData("0")]
    [InlineData("497,00")]
    [InlineData("-1")]
    [InlineData("049700")]
    public void PricePairUsesPositiveWholeCentsInText(string value)
    {
        var record = Record(new() { ["precoAnterior"] = value, ["precoNovo"] = "39700" });
        Assert.Equal("non_conforming", record.Conformity); Assert.Contains("complemento-invalido", record.Reasons);
    }
}
