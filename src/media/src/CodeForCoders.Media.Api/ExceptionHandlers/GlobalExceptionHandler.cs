using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Domain.Exceptions;
using CodeForCoders.Media.Domain.SeedWork;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeForCoders.Media.Api.ExceptionHandlers;

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
            MediaApiException mediaException => (
                mediaException.StatusCode,
                "/problems/media-operation-error",
                mediaException.Title,
                mediaException.Detail),
            StorageUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "/problems/storage-unavailable",
                "Media storage is temporarily unavailable",
                "Retry the request later."),
            MultipartUploadNotFoundException => (
                StatusCodes.Status404NotFound,
                "/problems/upload-not-found",
                "The video upload was not found",
                null),
            VideoUploadRuleViolationException ruleViolation => (
                StatusCodes.Status422UnprocessableEntity,
                "/problems/video-upload-rule-violation",
                ruleViolation.Message,
                null),
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
        problemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;
        var code = exception switch
        {
            MediaApiException mediaException => mediaException.Code,
            StorageUnavailableException => "STORAGE_UNAVAILABLE",
            MultipartUploadNotFoundException => "UPLOAD_NOT_FOUND",
            VideoUploadRuleViolationException ruleViolation => ruleViolation.Code,
            _ => null,
        };
        if (code is not null)
        {
            problemDetails.Extensions["code"] = code;
        }
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
