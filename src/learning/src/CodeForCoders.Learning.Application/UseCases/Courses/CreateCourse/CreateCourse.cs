using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.Repositories;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateCourse;

public sealed class CreateCourse(
    ICourseRepository courses,
    ICourseCreationStore receipts,
    IUnitOfWork unitOfWork,
    IValidator<CreateCourseInput> validator,
    TimeProvider timeProvider) : ICreateCourse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CourseDetailOutput> ExecuteAsync(CreateCourseInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input.Title, input.Description })));
        var scope = new CourseCreationScope(input.TenantId, input.ActorId, input.IdempotencyKey);
        await using var transaction = await receipts.LockAsync(scope, cancellationToken);
        var receipt = await receipts.FindAsync(scope, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != hash) throw new CourseRuleException("IDEMPOTENCY_KEY_REUSED");
            return JsonSerializer.Deserialize<CourseDetailOutput>(receipt.ResponseJson, JsonOptions)!;
        }

        var course = Course.Create(new CourseCreation(input.TenantId, input.ActorId, input.ActorName, input.Title, input.Description, now));
        await courses.AddAsync(course, cancellationToken);
        var output = CourseDetailOutput.FromCourse(course);
        if (receipt is null)
        {
            receipt = CourseCreationReceipt.Create(input.TenantId, input.ActorId, input.IdempotencyKey);
            receipts.Add(receipt);
        }
        receipt.Store(hash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }
}
