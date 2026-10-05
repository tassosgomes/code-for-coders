using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.UseCases.Sales.Common;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.GetStudentOrder;

public sealed class GetStudentOrder(IOrderStore store) : IGetStudentOrder
{
    public async Task<StudentOrder> ExecuteAsync(GetStudentOrderInput input, CancellationToken cancellationToken) =>
        StudentOrder.FromOrder(await store.FindAsync(input.StudentId, input.OrderId, cancellationToken) ?? throw new NotFoundException("ORDER_NOT_FOUND"));
}
