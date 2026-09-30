using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.Repositories;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed class CourseEditSession(
    ICourseRepository courses, ICourseEditStore edits,
    IUnitOfWork unitOfWork, IValidator<CourseWriteContext> validator, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CourseEditOutput> ExecuteAsync(CourseWriteContext input, string operation,
        Func<Course, Guid?> edit, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var transaction = await edits.LockAsync(input.CourseId, cancellationToken);
        var course = await courses.GetAsync(input.CourseId, cancellationToken);
        NotFoundException.ThrowIfNull(course, "Course not found.");
        var key = "edit:" + Hash($"{input.CourseId:D}:{operation}:{input.IdempotencyKey}");
        var scope = new CourseEditScope(input.TenantId, input.ActorId, key);
        var receipt = await edits.FindAsync(scope, cancellationToken);
        var hash = Hash(CanonicalJson(input.RequestJson));
        var now = timeProvider.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != hash) throw new CourseRuleException("IDEMPOTENCY_KEY_REUSED");
            return JsonSerializer.Deserialize<CourseEditOutput>(receipt.ResponseJson, JsonOptions)!;
        }
        var createdId = edit(course!);
        course!.RecordEdit(new CourseCreation(input.TenantId, input.ActorId, input.ActorName, course.Title, course.Description, now));
        var output = new CourseEditOutput(CourseDetailOutput.FromCourse(course), createdId);
        if (receipt is null) { receipt = CourseEditReceipt.Create(input.TenantId, input.ActorId, key); edits.Add(receipt); }
        receipt.Store(hash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }

    private static string CanonicalJson(string requestJson)
    {
        if (requestJson.Length == 0) return requestJson;
        using var document = JsonDocument.Parse(requestJson);
        return JsonSerializer.Serialize(document.RootElement.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => property.Value));
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
