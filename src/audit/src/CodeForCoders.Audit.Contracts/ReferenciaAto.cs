namespace CodeForCoders.Audit.Contracts;

public sealed record ReferenciaAto
{
    public string? Tipo { get; init; }

    public Guid? Id { get; init; }
}
