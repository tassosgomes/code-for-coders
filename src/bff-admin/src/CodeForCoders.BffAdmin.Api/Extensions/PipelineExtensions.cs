using CodeForCoders.BffAdmin.Api.Security;
using Scalar.AspNetCore;

namespace CodeForCoders.BffAdmin.Api.Extensions;

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
        app.UseBffSecurity();
        return app;
    }
}
