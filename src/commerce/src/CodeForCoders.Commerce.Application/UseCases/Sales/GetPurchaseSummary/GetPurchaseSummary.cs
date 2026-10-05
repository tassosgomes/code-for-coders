using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.UseCases.Sales.Common;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.GetPurchaseSummary;

public sealed class GetPurchaseSummary(IPurchaseOfferReader catalog, IExistingCourseAccessReader entitlement,
    IOrderStore orders) : IGetPurchaseSummary
{
    public async Task<PurchaseSummary> ExecuteAsync(GetPurchaseSummaryInput input, CancellationToken cancellationToken)
    {
        var offer = await catalog.FindAsync(input.OfferId, cancellationToken) ?? throw new NotFoundException("OFFER_NOT_AVAILABLE");
        ExistingCourseAccess? access = null;
        var checkedAccess = true;
        try { access = await entitlement.FindAsync(input.StudentId, offer.CourseId, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            checkedAccess = false;
            System.Diagnostics.Activity.Current?.AddEvent(new("existing-access-unavailable"));
        }
        var pending = await orders.FindPendingAsync(input.StudentId, input.OfferId, cancellationToken);
        return new(new(offer.CourseId, offer.Title), new(offer.OfferId, offer.Name, offer.PriceCents, offer.Currency, offer.AccessPeriod),
            pending?.Id, checkedAccess, access);
    }
}
