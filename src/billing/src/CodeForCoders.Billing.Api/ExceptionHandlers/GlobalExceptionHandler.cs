using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Domain.SeedWork;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeForCoders.Billing.Api.ExceptionHandlers;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, type, title, detail) = exception switch
        {
            GatewayUnavailableException or GatewayInboxUnavailableException => (503, "about:blank", "Payment temporarily unavailable", "Please retry later."),
            GatewaySignatureException => (400, "about:blank", "Invalid signature", "Gateway signature or event is invalid."),
            PaymentRuleException { Code: "INVALID_REQUEST" } => (StatusCodes.Status400BadRequest, "about:blank", "Invalid request", exception.Message),
            PaymentRuleException => (422, "about:blank", "Payment not available", exception.Message),
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "/problems/validation-error",
                "Validation failed",
                "One or more validation errors occurred."),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "/problems/not-found",
                "Resource not found",
                exception.Message),
            EntityValidationException or RelatedAggregateException => (
                StatusCodes.Status422UnprocessableEntity,
                "/problems/business-rule-violation",
                "Business rule violation",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "/problems/unexpected-error",
                "Unexpected error",
                "An unexpected error occurred.")
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
        problemDetails.Extensions["code"] = exception switch
        {
            PaymentRuleException rule => rule.Code,
            GatewayUnavailableException => "GATEWAY_UNAVAILABLE",
            GatewayInboxUnavailableException => "TEMPORARILY_UNAVAILABLE",
            GatewaySignatureException => "SIGNATURE_INVALID",
            ValidationException => "INVALID_REQUEST",
            _ => "UNEXPECTED_ERROR"
        };
        problemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;
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
