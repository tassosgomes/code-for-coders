using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Showcase.RegisterPurchaseIntent;

public sealed class RegisterPurchaseIntent(IPurchaseIntentStore store, ITenantContext tenantContext,
    IValidator<RegisterPurchaseIntentInput> validator, TimeProvider timeProvider) : IRegisterPurchaseIntent
{
    public const string NotFoundCode = "OFFER_NOT_AVAILABLE";

    public async Task<PurchaseIntentAccepted> ExecuteAsync(RegisterPurchaseIntentInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        using var activity = CommerceTelemetry.ActivitySource.StartActivity("registerPurchaseIntent");
        activity?.SetTag("offer.id", input.OfferId);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.IdempotencyKey)));
        var result = await store.RegisterAsync(new(tenantContext.TenantId!.Value, input.OfferId, hash, timeProvider.GetUtcNow()), cancellationToken);
        switch (result)
        {
            case PurchaseIntentResult.Unavailable:
                CommerceTelemetry.PurchaseIntentsRefused.Add(1);
                throw new NotFoundException(NotFoundCode);
            case PurchaseIntentResult.Repeated:
                CommerceTelemetry.PurchaseIntentsRepeated.Add(1);
                break;
            case PurchaseIntentResult.Counted:
                CommerceTelemetry.PurchaseIntentsCounted.Add(1);
                break;
        }
        return new();
    }
}
