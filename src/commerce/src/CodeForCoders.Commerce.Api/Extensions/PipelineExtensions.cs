using Scalar.AspNetCore;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;
            if (http.Response.StatusCode == 400 && (http.Request.Path.StartsWithSegments("/internal/v1/orders")
                || http.Request.Path.StartsWithSegments("/internal/v1/offers")))
                await Results.Problem(statusCode: 400, title: "Invalid purchase request.",
                    extensions: new Dictionary<string, object?> { ["code"] = "INVALID_REQUEST", ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier }).ExecuteAsync(http);
        });
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
