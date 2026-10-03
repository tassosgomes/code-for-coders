namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentAccessGrantsQuery(Guid StudentId, int Page, int Size);
