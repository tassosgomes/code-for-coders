using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeForCoders.Learning.Api.ExceptionHandlers;

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
            StudentLessonException { Code: "LESSON_NOT_AVAILABLE" } => (404, "about:blank", "Esta aula não está disponível.", "Esta aula não está disponível."),
            StudentLessonException { Code: "ACCESS_DENIED" } => (403, "about:blank", "Você não tem acesso a este curso.", "Você não tem acesso a este curso."),
            StudentLessonException => (503, "about:blank", "Não foi possível confirmar seu acesso agora.", "Não foi possível confirmar seu acesso agora."),
            DraftChangedException => (409, "/problems/draft-changed", "Draft changed", exception.Message),
            CourseRuleException { Code: "COURSE_ALREADY_PUBLISHED" } => (409, "/problems/course-already-published", "Course already published", exception.Message),
            CourseRuleException { Code: "COURSE_NEVER_PUBLISHED" } => (409, "/problems/course-never-published", "Course never published", exception.Message),
            CourseIncompleteException => (422, "/problems/course-incomplete", "Course incomplete", exception.Message),
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "/problems/validation-error",
                "Validation failed",
                "One or more validation errors occurred."),
            CourseItemNotFoundException or NotFoundException => (
                StatusCodes.Status404NotFound,
                "/problems/not-found",
                "Resource not found",
                exception.Message),
            CourseRuleException or EntityValidationException or RelatedAggregateException => (
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
            if (httpContext.Request.Path.Value?.EndsWith("/versions", StringComparison.Ordinal) == true)
                logger.LogError("Publication failed with {ErrorType}.", exception.GetType().Name);
            else logger.LogError(exception, "Unhandled exception while processing {Path}.", httpContext.Request.Path);
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
            StudentLessonException lesson => lesson.Code,
            DraftChangedException => "DRAFT_CHANGED",
            CourseIncompleteException => "COURSE_INCOMPLETE",
            CourseItemNotFoundException item => item.Code,
            CourseRuleException rule => rule.Code,
            NotFoundException => "COURSE_NOT_FOUND",
            ValidationException => "INVALID_REQUEST",
            _ => "UNEXPECTED_ERROR",
        };
        if (exception is StudentLessonException lessonError)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            if (lessonError.Reason is not null) problemDetails.Extensions["reason"] = lessonError.Reason;
            if (lessonError.AccessEndedAt.HasValue) problemDetails.Extensions["accessEndedAt"] = lessonError.AccessEndedAt.Value;
        }
        if (exception is CourseIncompleteException incomplete) problemDetails.Extensions["pendencies"] = incomplete.Pendencies;
        if (exception is CourseRuleException { Field: not null } fieldError)
            problemDetails.Extensions["errors"] = new Dictionary<string, string[]> { [fieldError.Field] = [fieldError.Message] };
        if (exception is CourseRuleException { Code: "TITLE_REQUIRED" })
            problemDetails.Extensions["errors"] = new Dictionary<string, string[]> { ["title"] = ["Informe o título do curso."] };
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
