namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ISalesPaymentSink { Task ApplyAsync(PaymentConfirmedFact fact, CancellationToken cancellationToken); }
