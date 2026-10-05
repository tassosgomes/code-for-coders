using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.StartOrderPayment;

public interface IStartOrderPayment : IUseCase<StartOrderPaymentInput, BillingPaymentSession>;
