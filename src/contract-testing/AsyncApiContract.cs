using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace CodeForCoders.ContractTesting;

/// <summary>
/// Validates a real message payload against the AsyncAPI document in contracts/ (the current contract),
/// resolving the message by the channel address the application sends to.
/// </summary>
public sealed class AsyncApiContract
{
    private const string ContractsFolder = "Contracts";
    private static readonly Dictionary<string, JsonNode> Cache = [];

    private readonly string _path;
    private readonly JsonNode _document;

    private AsyncApiContract(string path, JsonNode document)
    {
        _path = path;
        _document = document;
    }

    public string Version => _document["info"]!["version"]!.GetValue<string>();

    /// <param name="relativePath">Path inside contracts/, e.g. "identity/asyncapi.yaml".</param>
    public static AsyncApiContract Load(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ContractsFolder, relativePath));
        return new AsyncApiContract(path, ReadYaml(path));
    }

    /// <summary>Asserts that <paramref name="payload"/> matches a message of a send operation on <paramref name="address"/>.</summary>
    public void AssertSends(string address, JsonElement payload)
    {
        var schemas = SendPayloadSchemas(address);
        Assert.True(schemas.Count > 0, $"{Describe()} has no send operation on channel '{address}'.");
        var errors = new List<string>();
        foreach (var schema in schemas)
        {
            var result = JsonSchema.FromText(schema.ToJsonString())
                .Evaluate(payload, new EvaluationOptions { RequireFormatValidation = true, OutputFormat = OutputFormat.List });
            if (result.IsValid) return;
            errors.AddRange((result.Details ?? []).Where(detail => detail.Errors is { Count: > 0 })
                .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation}: {error.Value}")));
        }

        Assert.Fail($"Payload sent to '{address}' does not match {Describe()}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    public void AssertSends(string address, string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);
        AssertSends(address, payload.RootElement);
    }

    private List<JsonNode> SendPayloadSchemas(string address)
    {
        var channels = _document["channels"]!.AsObject();
        var schemas = new List<JsonNode>();
        foreach (var (_, operation) in _document["operations"]!.AsObject())
        {
            if (operation!["action"]!.GetValue<string>() != "send") continue;
            var channelName = operation["channel"]!["$ref"]!.GetValue<string>().Split('/')[^1];
            var channel = channels[channelName]!;
            if (channel["address"]!.GetValue<string>() != address) continue;
            // The operation may narrow the channel to some of its messages; otherwise any channel message applies.
            var messages = operation["messages"] is JsonArray declared
                ? declared.Select(message => message!)
                : channel["messages"]!.AsObject().Select(message => message.Value!);
            foreach (var message in messages)
            {
                var (resolved, path) = Resolve(message, _path);
                schemas.Add(Inline(resolved["payload"]!, path));
            }
        }

        return schemas;
    }

    private string Describe() => $"contracts/{Path.GetRelativePath(Path.Combine(AppContext.BaseDirectory, ContractsFolder), _path)} {Version}";

    private static (JsonNode Node, string Path) Resolve(JsonNode node, string path)
    {
        while (node is JsonObject obj && obj["$ref"] is { } reference)
        {
            var parts = reference.GetValue<string>().Split('#', 2);
            if (parts[0].Length > 0) path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, parts[0]));
            var target = ReadYaml(path);
            foreach (var segment in parts[1].Split('/', StringSplitOptions.RemoveEmptyEntries)) target = target[segment]!;
            node = target;
        }

        return (node, path);
    }

    private static JsonNode Inline(JsonNode node, string path)
    {
        var (resolved, resolvedPath) = Resolve(node, path);
        return resolved switch
        {
            JsonObject obj => new JsonObject(obj.Select(property =>
                KeyValuePair.Create(property.Key, property.Value is null ? null : Inline(property.Value, resolvedPath)))),
            JsonArray array => new JsonArray(array.Select(item => item is null ? null : Inline(item, resolvedPath)).ToArray()),
            _ => resolved.DeepClone(),
        };
    }

    private static JsonNode ReadYaml(string path)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(path, out var document))
            {
                var data = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build()
                    .Deserialize<object>(File.ReadAllText(path));
                document = JsonNode.Parse(new SerializerBuilder().JsonCompatible().Build().Serialize(data))!;
                Cache[path] = document;
            }

            return document.DeepClone();
        }
    }
}
