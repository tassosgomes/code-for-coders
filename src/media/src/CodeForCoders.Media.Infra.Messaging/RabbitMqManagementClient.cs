using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class RabbitMqManagementClient
{
    private readonly HttpClient httpClient;
    private readonly Uri queueUri;
    private readonly Uri queuesInVirtualHostUri;
    private readonly AuthenticationHeaderValue authorization;
    private readonly string queueName;

    public RabbitMqManagementClient(HttpClient httpClient, IOptions<RabbitMqOptions> rabbitMqOptions)
    {
        this.httpClient = httpClient;
        var options = rabbitMqOptions.Value;
        queueName = $"{options.HeartbeatQueue}.dlq";
        queueUri = CreateQueueUri(options.ManagementUri, options.VirtualHost, queueName);
        queuesInVirtualHostUri = CreateQueuesInVirtualHostUri(options.ManagementUri, options.VirtualHost, queueName);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
        authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<long> GetDeadLetterQueueMessageCountAsync(CancellationToken cancellationToken)
    {
        using var queue = await GetJsonAsync(queueUri, cancellationToken);
        var messageCount = TryReadQueueMessageCount(queue.RootElement, queueName);
        if (messageCount is not null)
        {
            return messageCount.Value;
        }

        using var queues = await GetJsonAsync(queuesInVirtualHostUri, cancellationToken);
        messageCount = TryReadQueueMessageCount(queues.RootElement, queueName);
        if (messageCount is not null)
        {
            return messageCount.Value;
        }

        var responseFields = queues.RootElement.ValueKind == JsonValueKind.Object
            ? string.Join(", ", queues.RootElement.EnumerateObject().Select(property => property.Name))
            : queues.RootElement.ValueKind.ToString();
        throw new JsonException($"RabbitMQ Management API response did not contain a valid message count. Response fields: {responseFields}.");
    }

    private async Task<JsonDocument> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = authorization;
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
    }

    private static long? TryReadQueueMessageCount(JsonElement response, string queueName)
    {
        if (response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            return TryReadQueueMessageCount(items, queueName);
        }

        if (response.ValueKind == JsonValueKind.Array)
        {
            foreach (var queue in response.EnumerateArray())
            {
                var count = TryReadQueueMessageCount(queue, queueName);
                if (count is not null)
                {
                    return count;
                }
            }

            return null;
        }

        if (response.ValueKind != JsonValueKind.Object
            || !response.TryGetProperty("name", out var name)
            || !string.Equals(name.GetString(), queueName, StringComparison.Ordinal)
            || !response.TryGetProperty("messages", out var messages)
            || !messages.TryGetInt64(out var messageCount)
            || messageCount < 0)
        {
            return null;
        }

        return messageCount;
    }

    private static Uri CreateQueueUri(string managementUri, string virtualHost, string queueName)
    {
        var baseUri = CreateBaseUri(managementUri);
        var relativePath = $"api/queues/{Uri.EscapeDataString(virtualHost)}/{Uri.EscapeDataString(queueName)}";
        return new Uri(baseUri, relativePath);
    }

    private static Uri CreateQueuesInVirtualHostUri(string managementUri, string virtualHost, string queueName)
    {
        var baseUri = CreateBaseUri(managementUri);
        var relativePath = $"api/queues/{Uri.EscapeDataString(virtualHost)}?page=1&page_size=1&name={Uri.EscapeDataString(queueName)}&use_regex=false&pagination=true&disable_stats=true&enable_queue_totals=true";
        return new Uri(baseUri, relativePath);
    }

    private static Uri CreateBaseUri(string managementUri)
        => new(managementUri.EndsWith("/", StringComparison.Ordinal) ? managementUri : $"{managementUri}/", UriKind.Absolute);
}
