using System.Text.Json.Serialization;

namespace CodeForCoders.Audit.Contracts;

/// <summary>
/// Integration contract for a completed administrative act.
/// </summary>
[JsonConverter(typeof(AtoPraticadoJsonConverter))]
public sealed record AtoPraticado
{
    public Guid FatoId { get; init; }

    public string? Origem { get; init; }

    public string? Tipo { get; init; }

    public Guid TenantId { get; init; }

    public DateTimeOffset? PraticadoEm { get; init; }

    public ReferenciaAto? Autor { get; init; }

    public ReferenciaAto? Alvo { get; init; }

    public Dictionary<string, string>? Complemento { get; init; }

    public string? Motivo { get; init; }

    [JsonIgnore]
    public bool ComplementoInvalido { get; internal init; }

    [JsonIgnore]
    public string? ComplementoOriginalCanonico { get; internal init; }
}
