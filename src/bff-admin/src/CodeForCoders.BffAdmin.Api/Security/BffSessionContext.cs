using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Security;

public static class BffSessionContext
{
    private const string ItemKey = "CodeForCoders.BffAdmin.OpaqueSession";
    private const string ValidatedSessionKey = "CodeForCoders.BffAdmin.ValidatedStaffSession";

    public static OpaqueBffSession? Get(HttpContext context)
        => context.Items.TryGetValue(ItemKey, out var value) ? value as OpaqueBffSession : null;

    public static void Set(HttpContext context, OpaqueBffSession session)
        => context.Items[ItemKey] = session;

    public static StaffSessionValidatedV1? GetValidatedSession(HttpContext context)
        => context.Items.TryGetValue(ValidatedSessionKey, out var value) ? value as StaffSessionValidatedV1 : null;

    public static void SetValidatedSession(HttpContext context, StaffSessionValidatedV1 session)
        => context.Items[ValidatedSessionKey] = session;
}
