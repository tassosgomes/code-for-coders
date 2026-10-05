namespace CodeForCoders.Billing.Application.Interfaces;

public interface IPaymentTransaction : IAsyncDisposable { Task CompleteAsync(CancellationToken cancellationToken); }
