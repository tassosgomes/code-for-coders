using CodeForCoders.BffStudent.Application.Common;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Captcha;

public sealed class CaptchaEndpointFilter(CaptchaFailurePolicy failurePolicy) : IEndpointFilter
{
    public const string HeaderName = "X-Captcha-Token";
    private const int MaxTokenLength = 2048;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var options = http.RequestServices.GetRequiredService<IOptions<TurnstileOptions>>().Value;
        if (!options.Enabled)
        {
            Record("disabled");
            return await next(context);
        }

        var token = http.Request.Headers[HeaderName].ToString().Trim();
        if (token.Length == 0)
        {
            Record("missing");
            return Problem(http, StatusCodes.Status400BadRequest, "CAPTCHA_REQUIRED", "Captcha obrigatório.");
        }

        if (token.Length > MaxTokenLength)
        {
            Record("failed");
            return Problem(http, StatusCodes.Status403Forbidden, "CAPTCHA_INVALID", "Verificação de captcha reprovada.");
        }

        var verifier = http.RequestServices.GetRequiredService<ITurnstileVerifier>();
        var result = await verifier.VerifyAsync(token, ClientIpResolver.Resolve(http), http.RequestAborted);
        switch (result.Outcome)
        {
            case TurnstileOutcome.Passed:
                Record("passed");
                return await next(context);
            case TurnstileOutcome.Failed:
                Record("failed");
                return Problem(http, StatusCodes.Status403Forbidden, "CAPTCHA_INVALID", "Verificação de captcha reprovada.");
            default:
                Record("unavailable");
                var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<CaptchaEndpointFilter>();
                if (failurePolicy == CaptchaFailurePolicy.FailOpen)
                {
                    logger.LogWarning("Captcha verification unavailable on {Path}; request allowed (fail-open)", http.Request.Path.Value);
                    return await next(context);
                }

                logger.LogWarning("Captcha verification unavailable on {Path}; request rejected (fail-closed)", http.Request.Path.Value);
                return Problem(http, StatusCodes.Status503ServiceUnavailable, "CAPTCHA_UNAVAILABLE", "Verificação de captcha indisponível.");
        }
    }

    private static void Record(string result)
        => BffStudentTelemetry.CaptchaVerifications.Add(1, new KeyValuePair<string, object?>("result", result));

    private static IResult Problem(HttpContext http, int status, string code, string title)
    {
        http.Response.Headers.CacheControl = "no-store";
        return Results.Problem(
            type: "about:blank",
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
            });
    }
}
