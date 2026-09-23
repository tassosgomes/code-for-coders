using CodeForCoders.BffStudent.Application.Common;

namespace CodeForCoders.BffStudent.Api.Security;

public static class BffSessionContext
{
    private const string ItemKey = "CodeForCoders.BffStudent.OpaqueSession";
    private const string AccessTokenItemKey = "CodeForCoders.BffStudent.StudentAccessToken";

    public static OpaqueBffSession? Get(HttpContext context)
        => context.Items.TryGetValue(ItemKey, out var value) ? value as OpaqueBffSession : null;

    public static void Set(HttpContext context, OpaqueBffSession session)
        => context.Items[ItemKey] = session;

    public static string? GetAccessToken(HttpContext context)
        => context.Items.TryGetValue(AccessTokenItemKey, out var value) ? value as string : null;

    public static void SetAccessToken(HttpContext context, string? accessToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            context.Items[AccessTokenItemKey] = accessToken;
        }
    }
}
