using CodeForCoders.Commerce.Domain.Entities;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class CourtesyTitleSearchTests
{
    // The first two cases are the same conformance examples used by Learning.
    [Theory(DisplayName = nameof(NormalizationMatchesTheLearningConvention))]
    [InlineData("AÇÃO À Ê Í Ó Ú Ü", "acao a e i o u u")]
    [InlineData("Fundaméntos", "fundamentos")]
    [InlineData("Fundamentos de C#", "fundamentos de c#")]
    [InlineData("ÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ", "aaaaaeeeeiiiiooooouuuuc")]
    public void NormalizationMatchesTheLearningConvention(string title, string expected)
        => Assert.Equal(expected, CourtesyTitleSearch.Normalize(title));
}
