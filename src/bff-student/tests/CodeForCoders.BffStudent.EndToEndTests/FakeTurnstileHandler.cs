using System.Net;
using System.Text;

namespace CodeForCoders.BffStudent.EndToEndTests;

/// <summary>Fake of the Cloudflare siteverify: the verdict comes from the token value, so parallel tests never share state.</summary>
public sealed class FakeTurnstileHandler : HttpMessageHandler
{
    public const string ValidToken = "valid-token";
    public const string InvalidToken = "invalid-token";
    public const string UnavailableToken = "unavailable-token";

    private readonly List<(string Response, string? RemoteIp)> calls = [];

    public IReadOnlyList<(string Response, string? RemoteIp)> Calls
    {
        get
        {
            lock (calls)
            {
                return calls.ToList();
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var form = (await request.Content!.ReadAsStringAsync(cancellationToken))
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts.ElementAtOrDefault(1) ?? string.Empty));
        var token = form.GetValueOrDefault("response") ?? string.Empty;
        lock (calls)
        {
            calls.Add((token, form.GetValueOrDefault("remoteip")));
        }

        return token switch
        {
            ValidToken => Json(HttpStatusCode.OK, """{"success":true}"""),
            UnavailableToken => Json(HttpStatusCode.ServiceUnavailable, string.Empty),
            _ => Json(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}"""),
        };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
