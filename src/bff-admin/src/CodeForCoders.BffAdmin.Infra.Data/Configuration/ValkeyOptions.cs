using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.BffAdmin.Infra.Data.Configuration;

public sealed class ValkeyOptions
{
    public const string SectionName = "Valkey";

    [Required]
    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
}
