using System.Text.Json;

namespace CodeForCoders.Commerce.Api.Security;

public static class SecurityProblem
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, int statusCode, string code, string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(
            new Body(
                "about:blank",
                title,
                statusCode,
                code,
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier),
            JsonOptions,
            cancellationToken: context.RequestAborted);
    }

    private sealed record Body(string Type, string Title, int Status, string Code, string TraceId);
}
