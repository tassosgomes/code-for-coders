namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ISalesAccessSink { Task ApplyAsync(PurchaseAccessFact fact, CancellationToken cancellationToken); }
