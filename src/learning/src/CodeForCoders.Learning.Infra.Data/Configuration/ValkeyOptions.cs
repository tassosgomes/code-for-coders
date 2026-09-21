namespace CodeForCoders.Learning.Infra.Data.Configuration;

public sealed class ValkeyOptions
{
    public const string SectionName = "Valkey";

    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
}
