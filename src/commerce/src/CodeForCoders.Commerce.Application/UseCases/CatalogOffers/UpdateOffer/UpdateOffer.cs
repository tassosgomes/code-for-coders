using System.Text.Json;
using System.Globalization;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UpdateOffer;

public sealed class UpdateOffer(ICatalogCourseEditStore edits, IUnitOfWork unitOfWork,
    IValidator<UpdateOfferInput> validator, TimeProvider timeProvider, ICatalogOutboxMessageWriter outbox) : IUpdateOffer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogOfferDetail> ExecuteAsync(UpdateOfferInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var courseId = (await edits.FindOfferCourseAsync(input.OfferId, cancellationToken)) ?? Guid.Empty;
        var scope = new CatalogEditScope(input.TenantId, input.ActorId, courseId, OfferRequest.Hash(input.IdempotencyKey));
        await using var transaction = await edits.LockAsync(scope, cancellationToken);
        var receipt = await edits.FindAsync(scope, cancellationToken);
        var canonical = OfferRequest.Canonical(input.Body);
        var requestHash = OfferRequest.Hash($"UpdateOffer:{input.OfferId:D}:{canonical}");
        var now = timeProvider.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != requestHash)
                throw new CatalogRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
            return JsonSerializer.Deserialize<CatalogOfferDetail>(receipt.ResponseJson, JsonOptions)!;
        }
        var course = await edits.GetAsync(courseId, cancellationToken) ?? throw new NotFoundException("OFFER_NOT_FOUND");
        if (!course.Offers.Any(offer => offer.OfferId == input.OfferId)) throw new NotFoundException("OFFER_NOT_FOUND");
        var fact = course.UpdateOffer(input.OfferId, OfferRequest.ParseChange(input.Body), now);
        var output = CatalogOfferDetail.FromCatalogOffer(course.Offers.Single(offer => offer.OfferId == input.OfferId));
        if (fact is not null)
        {
            await outbox.AppendAsync(new(fact.EventId, input.TenantId, "OfertaAlterada", "catalogo.oferta-alterada.v1",
                fact, now, input.TraceParent), cancellationToken);
            var complement = new Dictionary<string, string> { ["curso"] = courseId.ToString("D") };
            if (fact.Previous.PriceCents != fact.PriceCents)
            {
                complement["precoAnterior"] = fact.Previous.PriceCents.ToString(CultureInfo.InvariantCulture);
                complement["precoNovo"] = fact.PriceCents.ToString(CultureInfo.InvariantCulture);
            }
            if (fact.Previous.AccessPeriod != fact.AccessPeriod)
            {
                complement["vigenciaAnterior"] = FormatPeriod(fact.Previous.AccessPeriod);
                complement["vigenciaNova"] = FormatPeriod(fact.AccessPeriod);
            }
            var act = new
            {
                fatoId = fact.EventId,
                origem = "catalogo",
                tipo = "oferta-alterada",
                tenantId = input.TenantId,
                praticadoEm = now,
                autor = new { tipo = "conta-interna", id = input.ActorId },
                alvo = new { tipo = "oferta", id = input.OfferId },
                complemento = complement
            };
            await outbox.AppendAsync(new(Guid.CreateVersion7(), input.TenantId, "AtoPraticado", "auditoria.ato-praticado.v1",
                act, now, input.TraceParent), cancellationToken);
        }
        if (receipt is null) { receipt = CatalogEditReceipt.Create(input.TenantId, input.ActorId, scope.Key); edits.Add(receipt); }
        receipt.Store(requestHash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return output;
    }

    private static string FormatPeriod(Domain.ValueObjects.AccessPeriod period)
        => period.Type == "lifetime" ? "vitalicia" : $"{period.Months!.Value.ToString(CultureInfo.InvariantCulture)}m";
}
