namespace CodeForCoders.Identity.Domain.Entities;

public static class StaffRoleCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PermissionsByRole =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Administrator] = [ManageAccess],
            [Finance] = [ReadFinance],
            [Teacher] = [ReadAuthoring],
            [Support] = [HandleSupport],
        };

    public const string Administrator = "administrador";
    public const string Finance = "financeiro";
    public const string Teacher = "professor";
    public const string Support = "suporte";

    public const string ManageAccess = "acesso.gerir";
    public const string ReadFinance = "financeiro.ler";
    public const string ReadAuthoring = "autoria.ler";
    public const string HandleSupport = "suporte.atender";

    public static bool Contains(string role) => PermissionsByRole.ContainsKey(role);

    public static IReadOnlyList<string> GetPermissions(string role)
        => PermissionsByRole.TryGetValue(role, out var permissions) ? permissions : [];
}
