using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Domain.SeedWork;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeForCoders.Identity.Api.ExceptionHandlers;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, type, title, detail, code) = exception switch
        {
            StudentRegistrationException registrationException => (
                registrationException.StatusCode,
                "/problems/student-registration",
                registrationException.Title,
                registrationException.Message,
                registrationException.Code),
            StudentConfirmationException confirmationException => (
                confirmationException.StatusCode,
                "/problems/student-confirmation",
                confirmationException.Title,
                confirmationException.Message,
                confirmationException.Code),
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "/problems/validation-error",
                "Validation failed",
                "One or more validation errors occurred.",
                "INVALID_REQUEST"),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "/problems/not-found",
                "Resource not found",
                exception.Message,
                "NOT_FOUND"),
            EntityValidationException or RelatedAggregateException => (
                StatusCodes.Status422UnprocessableEntity,
                "/problems/business-rule-violation",
                "Business rule violation",
                exception.Message,
                "BUSINESS_RULE_VIOLATION"),
            _ => (
                StatusCodes.Status500InternalServerError,
                "/problems/unexpected-error",
                "Unexpected error",
                "An unexpected error occurred.",
                "INTERNAL_ERROR")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}.", httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected with status {StatusCode} at {Path}.", status, httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["code"] = code;
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });
    }
}
