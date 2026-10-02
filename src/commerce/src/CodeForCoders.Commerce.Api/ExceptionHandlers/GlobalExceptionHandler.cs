using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.UseCases.Showcase.GetShowcaseCourse;
using CodeForCoders.Commerce.Domain.SeedWork;
using CodeForCoders.Commerce.Application.UseCases.Showcase.RegisterPurchaseIntent;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeForCoders.Commerce.Api.ExceptionHandlers;

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
            StudentAccountCheckUnavailableException => (503, "about:blank", "Student account check unavailable", "Student account confirmation is unavailable."),
            EntitlementRuleException => (422, "about:blank", "Courtesy rejected", exception.Message),
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "/problems/validation-error",
                "Validation failed",
                "One or more validation errors occurred."),
            NotFoundException { Message: RegisterPurchaseIntent.NotFoundCode } => (
                StatusCodes.Status404NotFound, "about:blank", "Oferta não disponível.", "Oferta não disponível."),
            NotFoundException { Message: GetShowcaseCourse.NotFoundCode } => (
                StatusCodes.Status404NotFound,
                "about:blank",
                "Curso não disponível.",
                "Curso não disponível."),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "/problems/not-found",
                "Resource not found",
                exception.Message),
            CatalogRuleException or EntityValidationException or RelatedAggregateException => (
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

        if (status >= StatusCodes.Status500InternalServerError && exception is not StudentAccountCheckUnavailableException)
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
        if (exception is StudentAccountCheckUnavailableException) problemDetails.Extensions["code"] = "STUDENT_ACCOUNT_CHECK_UNAVAILABLE";
        if (exception is EntitlementRuleException entitlement) problemDetails.Extensions["code"] = entitlement.Code;
        if (exception is NotFoundException { Message: "GRANT_NOT_FOUND" }) problemDetails.Extensions["code"] = "GRANT_NOT_FOUND";
        if (exception is CatalogRuleException catalogRule) problemDetails.Extensions["code"] = catalogRule.Code;
        if (exception is NotFoundException && exception.Message is "CATALOG_COURSE_NOT_FOUND" or "OFFER_NOT_FOUND" or GetShowcaseCourse.NotFoundCode or RegisterPurchaseIntent.NotFoundCode)
            problemDetails.Extensions["code"] = exception.Message;
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["code"] = "INVALID_REQUEST";
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
