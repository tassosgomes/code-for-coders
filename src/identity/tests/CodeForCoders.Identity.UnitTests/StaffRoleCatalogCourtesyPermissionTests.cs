using CodeForCoders.Identity.Domain.Entities;
using Xunit;

namespace CodeForCoders.Identity.UnitTests;

public sealed class StaffRoleCatalogCourtesyPermissionTests
{
    [Theory]
    [InlineData(StaffRoleCatalog.Finance, true)]
    [InlineData(StaffRoleCatalog.Teacher, false)]
    [InlineData(StaffRoleCatalog.Support, false)]
    [InlineData(StaffRoleCatalog.Administrator, false)]
    public void OnlyFinanceReceivesCourtesyPermission(string role, bool granted)
    {
        var permissions = StaffRoleCatalog.GetPermissions(role);
        Assert.Equal(granted, permissions.Contains("cortesia.conceder", StringComparer.Ordinal));
        if (granted)
        {
            Assert.Contains(StaffRoleCatalog.ReadFinance, permissions);
            Assert.Contains(StaffRoleCatalog.EditOffers, permissions);
            Assert.Equal(3, permissions.Count);
        }
    }
}
