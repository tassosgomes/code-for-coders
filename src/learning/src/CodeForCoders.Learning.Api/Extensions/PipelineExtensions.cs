using Scalar.AspNetCore;

namespace CodeForCoders.Learning.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<CodeForCoders.Learning.Api.Security.TenantContextMiddleware>();
        app.UseAuthorization();
        return app;
    }
}
