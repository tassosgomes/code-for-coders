using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.Common;

public sealed record PurchaseSummary(OrderCourse Course, SummaryOffer Offer, Guid? PendingOrderId, bool ExistingAccessChecked, ExistingCourseAccess? ExistingAccess);
