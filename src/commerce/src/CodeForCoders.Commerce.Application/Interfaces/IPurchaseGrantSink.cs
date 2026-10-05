namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IPurchaseGrantSink { Task ApplyAsync(PurchaseCompletedFact fact, CancellationToken cancellationToken); }
