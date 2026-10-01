using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UnpublishOffer;

public sealed class UnpublishOffer(ICatalogCourseEditStore edits, IUnitOfWork unitOfWork,
    IValidator<UnpublishOfferInput> validator, TimeProvider timeProvider, ICatalogOutboxMessageWriter outbox) : IUnpublishOffer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogOfferDetail> ExecuteAsync(UnpublishOfferInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var courseId = (await edits.FindOfferCourseAsync(input.OfferId, cancellationToken)) ?? Guid.Empty;
        var scope = new CatalogEditScope(input.TenantId, input.ActorId, courseId, OfferRequest.Hash(input.IdempotencyKey));
        await using var transaction = await edits.LockAsync(scope, cancellationToken);
        var receipt = await edits.FindAsync(scope, cancellationToken);
        var requestHash = OfferRequest.Hash($"UnpublishOffer:{input.OfferId:D}");
        var now = timeProvider.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != requestHash)
                throw new CatalogRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
            return JsonSerializer.Deserialize<CatalogOfferDetail>(receipt.ResponseJson, JsonOptions)!;
        }
        var course = await edits.GetAsync(courseId, cancellationToken) ?? throw new NotFoundException("OFFER_NOT_FOUND");
        if (!course.Offers.Any(offer => offer.OfferId == input.OfferId)) throw new NotFoundException("OFFER_NOT_FOUND");
        var fact = course.UnpublishOffer(input.OfferId, now);
        var output = CatalogOfferDetail.FromCatalogOffer(course.Offers.Single(offer => offer.OfferId == input.OfferId));
        await outbox.AppendAsync(new(fact.EventId, input.TenantId, "OfertaDespublicada", "catalogo.oferta-despublicada.v1",
            fact, now, input.TraceParent), cancellationToken);
        var act = new
        {
            fatoId = fact.EventId,
            origem = "catalogo",
            tipo = "oferta-despublicada",
            tenantId = input.TenantId,
            praticadoEm = now,
            autor = new { tipo = "conta-interna", id = input.ActorId },
            alvo = new { tipo = "oferta", id = input.OfferId },
            complemento = new { curso = courseId.ToString("D") }
        };
        await outbox.AppendAsync(new(Guid.CreateVersion7(), input.TenantId, "AtoPraticado", "auditoria.ato-praticado.v1",
            act, now, input.TraceParent), cancellationToken);
        if (receipt is null) { receipt = CatalogEditReceipt.Create(input.TenantId, input.ActorId, scope.Key); edits.Add(receipt); }
        receipt.Store(requestHash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }
}
