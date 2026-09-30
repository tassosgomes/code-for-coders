using CodeForCoders.Identity.Domain.Entities;
using Xunit;

namespace CodeForCoders.Identity.UnitTests;

public sealed class StaffRoleCatalogOfferPermissionTests
{
    [Fact(DisplayName = nameof(FinanceCanEditOffersAndStillReadFinance))]
    public void FinanceCanEditOffersAndStillReadFinance()
    {
        var permissions = StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Finance);
        Assert.Contains("oferta.editar", permissions);
        Assert.Contains("financeiro.ler", permissions);
    }

    [Theory(DisplayName = nameof(OtherRolesDoNotReceiveOfferPermission))]
    [InlineData("administrador")]
    [InlineData("professor")]
    [InlineData("suporte")]
    public void OtherRolesDoNotReceiveOfferPermission(string role)
        => Assert.DoesNotContain("oferta.editar", StaffRoleCatalog.GetPermissions(role));

    [Fact(DisplayName = nameof(UnknownRoleHasNoPermissions))]
    public void UnknownRoleHasNoPermissions() => Assert.Empty(StaffRoleCatalog.GetPermissions("unknown"));
}
