namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentOrderQuery(Guid StudentId, int Page, int Size);
