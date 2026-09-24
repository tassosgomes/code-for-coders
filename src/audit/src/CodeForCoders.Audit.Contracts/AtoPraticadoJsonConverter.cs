using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeForCoders.Audit.Contracts;

public sealed class AtoPraticadoJsonConverter : JsonConverter<AtoPraticado>
{
    public override AtoPraticado Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind is not JsonValueKind.Object)
        {
            throw new JsonException("An administrative act must be a JSON object.");
        }

        var factId = FindProperty(root, "fatoId");
        var origin = FindProperty(root, "origem");
        var type = FindProperty(root, "tipo");
        var tenantId = FindProperty(root, "tenantId");
        var practicedOn = FindProperty(root, "praticadoEm");
        var author = FindProperty(root, "autor");
        var target = FindProperty(root, "alvo");
        var complement = FindProperty(root, "complemento");
        var reason = FindProperty(root, "motivo");

        var parsedComplement = ReadComplement(complement, out var complementInvalid, out var complementFingerprint);

        return new AtoPraticado
        {
            FatoId = ReadGuid(factId),
            Origem = ReadString(origin),
            Tipo = ReadType(type),
            TenantId = ReadGuid(tenantId),
            PraticadoEm = ReadDateTimeOffset(practicedOn),
            Autor = ReadReference(author),
            Alvo = ReadReference(target),
            Complemento = parsedComplement,
            Motivo = ReadString(reason),
            ComplementoInvalido = complement.ValueKind is not JsonValueKind.Undefined && complementInvalid,
            ComplementoOriginalCanonico = complement.ValueKind is not JsonValueKind.Undefined
                && complementInvalid
                ? complementFingerprint
                : null,
        };
    }

    public override void Write(Utf8JsonWriter writer, AtoPraticado value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("fatoId", value.FatoId);
        WriteString(writer, "origem", value.Origem);
        WriteString(writer, "tipo", value.Tipo);
        writer.WriteString("tenantId", value.TenantId);
        if (value.PraticadoEm is not null)
        {
            writer.WriteString("praticadoEm", value.PraticadoEm.Value);
        }

        WriteReference(writer, "autor", value.Autor, options);
        WriteReference(writer, "alvo", value.Alvo, options);
        if (value.Complemento is not null)
        {
            writer.WritePropertyName("complemento");
            JsonSerializer.Serialize(writer, value.Complemento, options);
        }

        WriteString(writer, "motivo", value.Motivo);
        writer.WriteEndObject();
    }

    private static JsonElement FindProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return default;
    }

    private static Guid ReadGuid(JsonElement element)
        => element.ValueKind is JsonValueKind.String
            && Guid.TryParse(element.GetString(), out var value)
                ? value
                : Guid.Empty;

    private static string? ReadString(JsonElement element)
        => element.ValueKind is JsonValueKind.String ? element.GetString() : null;

    private static string? ReadType(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element)
        => element.ValueKind is JsonValueKind.String
            && element.TryGetDateTimeOffset(out var value)
                ? value
                : null;

    private static ReferenciaAto? ReadReference(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        var type = FindProperty(element, "tipo");
        var id = FindProperty(element, "id");
        return new ReferenciaAto
        {
            Tipo = type.ValueKind switch
            {
                JsonValueKind.String => type.GetString(),
                JsonValueKind.Undefined or JsonValueKind.Null => null,
                _ => type.GetRawText(),
            },
            Id = ReadNullableGuid(id),
        };
    }

    private static Guid? ReadNullableGuid(JsonElement element)
        => element.ValueKind is JsonValueKind.String
            && Guid.TryParse(element.GetString(), out var value)
                ? value
                : null;

    private static Dictionary<string, string>? ReadComplement(
        JsonElement element,
        out bool invalid,
        out string? fingerprint)
    {
        invalid = false;
        fingerprint = null;
        if (element.ValueKind is JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind is not JsonValueKind.Object)
        {
            invalid = true;
            fingerprint = Canonicalize(element);
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value.ValueKind is JsonValueKind.String
                ? property.Value.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(property.Name)
                || value is null
                || value.Length > 100
                || values.ContainsKey(property.Name))
            {
                invalid = true;
                fingerprint = Canonicalize(element);
                return null;
            }

            values.Add(property.Name, value);
        }

        return values;
    }

    private static string Canonicalize(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(
                ",",
                element.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonicalize(property.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonicalize)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "null",
        };

    private static void WriteString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static void WriteReference(
        Utf8JsonWriter writer,
        string propertyName,
        ReferenciaAto? value,
        JsonSerializerOptions options)
    {
        if (value is not null)
        {
            writer.WritePropertyName(propertyName);
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
