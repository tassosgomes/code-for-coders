using CodeForCoders.Audit.Api.ExceptionHandlers;

namespace CodeForCoders.Audit.Api.Extensions;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandlingConfiguration(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] =
                    System.Diagnostics.Activity.Current?.TraceId.ToString()
                    ?? context.HttpContext.TraceIdentifier;
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
