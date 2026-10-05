namespace CodeForCoders.BffStudent.Api.Clients;

public interface IOrdersCommerceClient
{
    Task<OrderProxyResult> SendAsync(OrderProxyRequest input, CancellationToken cancellationToken);
}
