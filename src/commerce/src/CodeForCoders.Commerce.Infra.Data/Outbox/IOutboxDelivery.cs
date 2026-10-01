namespace CodeForCoders.Commerce.Infra.Data.Outbox;

public interface IOutboxDelivery
{
    Guid Id { get; }
    string Type { get; }
    string RoutingKey { get; }
    string Payload { get; }
    string? TraceParent { get; }
    void MarkProcessed();
    void RegisterFailure(Exception exception);
}
