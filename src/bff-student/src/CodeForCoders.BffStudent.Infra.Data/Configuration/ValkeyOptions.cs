using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.BffStudent.Infra.Data.Configuration;

public sealed class ValkeyOptions
{
    public const string SectionName = "Valkey";

    [Required]
    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
}
