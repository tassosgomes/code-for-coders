namespace CodeForCoders.Learning.Infra.Data.Catalog;

public sealed class CatalogInitialLoadExecution
{
    public const string ExecutionName = "catalogo-carga-inicial";

    private CatalogInitialLoadExecution() { }

    public string Name { get; private set; } = ExecutionName;
    public DateTimeOffset CompletedAt { get; private set; }
    public int CourseCount { get; private set; }

    public static CatalogInitialLoadExecution Complete(int courseCount)
        => new() { CompletedAt = DateTimeOffset.UtcNow, CourseCount = courseCount };
}
