using Scalar.AspNetCore;

namespace CodeForCoders.Commerce.Api.Extensions;

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
        app.UseAuthorization();
        return app;
    }
}
