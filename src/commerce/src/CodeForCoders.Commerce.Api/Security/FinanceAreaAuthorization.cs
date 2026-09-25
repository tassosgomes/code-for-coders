namespace CodeForCoders.Commerce.Api.Security;

public static class FinanceAreaAuthorization
{
    public const string PolicyName = "FinanceAreaRead";
    public const string PermissionClaim = "permissions";
    public const string RequiredPermission = "financeiro.ler";
}
