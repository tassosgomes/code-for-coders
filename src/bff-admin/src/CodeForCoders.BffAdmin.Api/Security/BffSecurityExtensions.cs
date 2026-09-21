namespace CodeForCoders.BffAdmin.Api.Security;

public static class BffSecurityExtensions
{
    public static IApplicationBuilder UseBffSecurity(this IApplicationBuilder app)
        => app.UseMiddleware<BffSecurityMiddleware>();
}
