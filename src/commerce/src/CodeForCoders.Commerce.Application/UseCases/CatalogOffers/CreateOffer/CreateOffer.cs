using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.CreateOffer;

public sealed class CreateOffer(ICatalogCourseEditStore edits, IUnitOfWork unitOfWork,
    IValidator<CreateOfferInput> validator, TimeProvider timeProvider) : ICreateOffer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogOfferDetail> ExecuteAsync(CreateOfferInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var courseId = input.CourseId;
        var scope = new CatalogEditScope(input.TenantId, input.ActorId, courseId, OfferRequest.Hash(input.IdempotencyKey));
        await using var transaction = await edits.LockAsync(scope, cancellationToken);
        var receipt = await edits.FindAsync(scope, cancellationToken);
        var canonical = OfferRequest.Canonical(input.Body);
        var requestHash = OfferRequest.Hash($"CreateOffer:{input.CourseId:D}:{canonical}");
        var now = timeProvider.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != requestHash)
                throw new CatalogRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
            return JsonSerializer.Deserialize<CatalogOfferDetail>(receipt.ResponseJson, JsonOptions)!;
        }
        var course = await edits.GetAsync(courseId, cancellationToken) ?? throw new NotFoundException("CATALOG_COURSE_NOT_FOUND");
        var output = CatalogOfferDetail.FromCatalogOffer(course.CreateOffer(OfferRequest.ParseChange(input.Body), now));
        if (receipt is null) { receipt = CatalogEditReceipt.Create(input.TenantId, input.ActorId, scope.Key); edits.Add(receipt); }
        receipt.Store(requestHash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }
}
