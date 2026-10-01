using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.UpdateCatalogCourse;

public sealed class UpdateCatalogCourse(ICatalogCourseEditStore edits, ICatalogCourseQueries queries,
    IUnitOfWork unitOfWork, IValidator<UpdateCatalogCourseInput> validator, TimeProvider timeProvider) : IUpdateCatalogCourse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogCourseDetail> ExecuteAsync(UpdateCatalogCourseInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var scope = new CatalogEditScope(input.TenantId, input.ActorId, input.CourseId, Hash(input.IdempotencyKey));
        await using var transaction = await edits.LockAsync(scope, cancellationToken);
        var course = await edits.GetAsync(input.CourseId, cancellationToken)
            ?? throw new NotFoundException("CATALOG_COURSE_NOT_FOUND");
        var receipt = await edits.FindAsync(scope, cancellationToken);
        var canonical = JsonSerializer.Serialize(input.Body.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => property.Value));
        var requestHash = Hash($"updateCatalogCourse:{input.CourseId:D}:{canonical}");
        var now = timeProvider.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != requestHash)
                throw new CatalogRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
            return JsonSerializer.Deserialize<CatalogCourseDetail>(receipt.ResponseJson, JsonOptions)!;
        }
        if (input.Body.TryGetProperty("tagline", out var tagline)) course.UpdateTagline(tagline.GetString());
        // Queries read the persisted projection, so commit the edit inside the still-open transaction first.
        await unitOfWork.CommitAsync(cancellationToken);
        var output = (await queries.GetAsync(input.CourseId, cancellationToken))!;
        if (receipt is null) { receipt = CatalogEditReceipt.Create(input.TenantId, input.ActorId, scope.Key); edits.Add(receipt); }
        receipt.Store(requestHash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
