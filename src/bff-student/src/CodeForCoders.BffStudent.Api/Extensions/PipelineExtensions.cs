using CodeForCoders.BffStudent.Api.Security;
using Scalar.AspNetCore;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;
            if (http.Response.StatusCode == 400 && (http.Request.Path.StartsWithSegments("/api/v1/orders")
                || http.Request.Path.StartsWithSegments("/api/v1/offers")))
                await Results.Problem(statusCode: 400, title: "Invalid purchase request.",
                    extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_ERROR", ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier }).ExecuteAsync(http);
        });
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseRouting();
        app.UseCors("StudentSpa");
        app.UseRateLimiter();
        app.UseBffSecurity();
        return app;
    }
}
