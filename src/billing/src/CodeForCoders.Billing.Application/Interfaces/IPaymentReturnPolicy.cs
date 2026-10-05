namespace CodeForCoders.Billing.Application.Interfaces;

public interface IPaymentReturnPolicy { bool Allows(string url); }
