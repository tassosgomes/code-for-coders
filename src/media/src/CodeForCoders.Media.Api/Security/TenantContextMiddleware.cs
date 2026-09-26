using CodeForCoders.Media.Application.Common;

namespace CodeForCoders.Media.Api.Security;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId))
        {
            tenantContext.Set(tenantId);
        }

        await next(context);
    }
}
