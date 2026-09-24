using CodeForCoders.BffStudent.Api.ExceptionHandlers;
using CodeForCoders.BffStudent.Application.Exceptions;
using CodeForCoders.BffStudent.Domain.SeedWork;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.ExceptionHandlers;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_MapsValidationExceptionToBadRequestWithGroupedErrors()
    {
        var writer = new CapturingProblemDetailsService();
        var context = CreateContext("/api/v1/platform/heartbeats");
        var exception = new ValidationException(
        [
            new ValidationFailure("TenantId", "Tenant is required."),
            new ValidationFailure("TenantId", "Tenant must be a UUIDv7."),
            new ValidationFailure("Name", "Name is required."),
        ]);

        var handled = await CreateHandler(writer).TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var problem = writer.Single();
        Assert.Equal(400, problem.Status);
        Assert.Equal("/problems/validation-error", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Detail);
        Assert.Equal("/api/v1/platform/heartbeats", problem.Instance);
        Assert.Equal(
            System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "trace-123",
            problem.Extensions["traceId"]);
        var errors = Assert.IsType<Dictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal(["Tenant is required.", "Tenant must be a UUIDv7."], errors["TenantId"]);
        Assert.Equal(["Name is required."], errors["Name"]);
    }

    [Theory]
    [InlineData("not-found", 404, "/problems/not-found", "Resource not found")]
    [InlineData("entity-validation", 422, "/problems/business-rule-violation", "Business rule violation")]
    [InlineData("related-aggregate", 422, "/problems/business-rule-violation", "Business rule violation")]
    public async Task TryHandleAsync_MapsBusinessExceptionsWithTheirMessage(
        string kind,
        int expectedStatus,
        string expectedType,
        string expectedTitle)
    {
        var writer = new CapturingProblemDetailsService();
        var context = CreateContext("/api/v1/resource");
        Exception exception = kind switch
        {
            "not-found" => new NotFoundException("Resource 42 was not found."),
            "entity-validation" => new EntityValidationException("Resource 42 was not found."),
            _ => new RelatedAggregateException("Resource 42 was not found."),
        };

        var handled = await CreateHandler(writer).TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        var problem = writer.Single();
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(expectedType, problem.Type);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal("Resource 42 was not found.", problem.Detail);
        Assert.False(problem.Extensions.ContainsKey("errors"));
    }

    [Fact]
    public async Task TryHandleAsync_HidesInfrastructureDetailOnUnexpectedException()
    {
        var writer = new CapturingProblemDetailsService();
        var context = CreateContext("/api/v1/student-sessions");

        var handled = await CreateHandler(writer).TryHandleAsync(
            context,
            new InvalidOperationException("Valkey connection string: valkey:6379,password=secret"),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var problem = writer.Single();
        Assert.Equal("/problems/unexpected-error", problem.Type);
        Assert.Equal("An unexpected error occurred.", problem.Detail);
        Assert.DoesNotContain("secret", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReportsNotHandledWhenProblemDetailsCannotBeWritten()
    {
        var writer = new CapturingProblemDetailsService(canWrite: false);
        var context = CreateContext("/api/v1/resource");

        var handled = await CreateHandler(writer).TryHandleAsync(
            context,
            new NotFoundException("missing"),
            TestContext.Current.CancellationToken);

        Assert.False(handled);
    }

    private static GlobalExceptionHandler CreateHandler(IProblemDetailsService writer)
        => new(NullLogger<GlobalExceptionHandler>.Instance, writer);

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Request.Path = path;
        return context;
    }

    private sealed class CapturingProblemDetailsService(bool canWrite = true) : IProblemDetailsService
    {
        private readonly List<ProblemDetails> written = [];

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            written.Add(context.ProblemDetails);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            written.Add(context.ProblemDetails);
            return ValueTask.FromResult(canWrite);
        }

        public ProblemDetails Single() => Assert.Single(written);
    }
}
