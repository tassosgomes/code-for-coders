using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
using Xunit;
namespace CodeForCoders.Audit.UnitTests;

public sealed class CourtesyActPolicyTests
{
    private static AdministrativeAct Act() => new(Guid.CreateVersion7(), "matricula", "cortesia-concedida", Guid.CreateVersion7(), DateTimeOffset.UtcNow,
        new("conta-interna", Guid.CreateVersion7()), new("conta-aluno", Guid.CreateVersion7()),
        new Dictionary<string, string> { ["curso"] = Guid.CreateVersion7().ToString("D"), ["concessao"] = Guid.CreateVersion7().ToString("D"), ["vigencia"] = "6m" }, "Bolsa de mentoria");
    [Theory(DisplayName = nameof(ValidCourtesyPeriodsAreConforming))]
    [InlineData("1m")]
    [InlineData("60m")]
    [InlineData("vitalicia")]
    public void ValidCourtesyPeriodsAreConforming(string period)
    {
        var act = Act(); var complement = new Dictionary<string, string>(act.Complement!) { ["vigencia"] = period };
        var record = AuditRecord.Create(act with { Complement = complement }, DateTimeOffset.UtcNow); Assert.Equal("conforming", record.Conformity);
    }
    [Theory(DisplayName = nameof(InvalidCourtesyActsAreRetainedAsNonConforming))]
    [InlineData("reason")]
    [InlineData("origin")]
    [InlineData("target")]
    [InlineData("course")]
    [InlineData("grant")]
    [InlineData("period")]
    public void InvalidCourtesyActsAreRetainedAsNonConforming(string field)
    {
        var act = Act(); var complement = new Dictionary<string, string>(act.Complement!);
        if (field == "course") complement["curso"] = "invalid";
        if (field == "grant") complement["concessao"] = Guid.Empty.ToString();
        if (field == "period") complement["vigencia"] = "61m";
        act = act with
        {
            Complement = complement,
            Reason = field == "reason" ? "   " : act.Reason,
            Origin = field == "origin" ? "catalogo" : act.Origin,
            Target = field == "target" ? new("curso", Guid.CreateVersion7()) : act.Target
        };
        Assert.Equal("non_conforming", AuditRecord.Create(act, DateTimeOffset.UtcNow).Conformity);
    }
}
