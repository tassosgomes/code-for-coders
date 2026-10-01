using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.DeleteOffer;

public sealed class DeleteOffer(ICatalogCourseEditStore edits, IUnitOfWork unitOfWork,
    IValidator<DeleteOfferInput> validator, TimeProvider timeProvider) : IDeleteOffer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> ExecuteAsync(DeleteOfferInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var courseId = (await edits.FindOfferCourseAsync(input.OfferId, cancellationToken)) ?? Guid.Empty;
        var scope = new CatalogEditScope(input.TenantId, input.ActorId, courseId, OfferRequest.Hash(input.IdempotencyKey));
        await using var transaction = await edits.LockAsync(scope, cancellationToken);
        var receipt = await edits.FindAsync(scope, cancellationToken);
        var canonical = "";
        var requestHash = OfferRequest.Hash($"DeleteOffer:{input.OfferId:D}:{canonical}");
        var now = timeProvider.GetUtcNow();
        // Deletion replay must succeed after the offer has disappeared.
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != requestHash)
                throw new CatalogRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
            return JsonSerializer.Deserialize<bool>(receipt.ResponseJson, JsonOptions)!;
        }
        var course = await edits.GetAsync(courseId, cancellationToken) ?? throw new NotFoundException("OFFER_NOT_FOUND");
        if (!course.Offers.Any(offer => offer.OfferId == input.OfferId)) throw new NotFoundException("OFFER_NOT_FOUND");
        course.DeleteOffer(input.OfferId);
        const bool output = true;
        if (receipt is null) { receipt = CatalogEditReceipt.Create(input.TenantId, input.ActorId, scope.Key); edits.Add(receipt); }
        receipt.Store(requestHash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }
}
