using CodeForCoders.BffAdmin.Application.Common;

namespace CodeForCoders.BffAdmin.Api.Security;

public static class BffSessionContext
{
    private const string ItemKey = "CodeForCoders.BffAdmin.OpaqueSession";

    public static OpaqueBffSession? Get(HttpContext context)
        => context.Items.TryGetValue(ItemKey, out var value) ? value as OpaqueBffSession : null;

    public static void Set(HttpContext context, OpaqueBffSession session)
        => context.Items[ItemKey] = session;
}
