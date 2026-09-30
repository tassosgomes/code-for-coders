namespace CodeForCoders.Learning.Infra.Messaging.Configuration;

public sealed class CatalogInitialLoadOptions
{
    public const string SectionName = "CatalogInitialLoad";

    public bool Enabled { get; init; }
}
