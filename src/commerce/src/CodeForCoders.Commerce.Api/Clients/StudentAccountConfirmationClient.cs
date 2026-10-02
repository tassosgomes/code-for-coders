using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Api.Clients;

public sealed class StudentAccountConfirmationClient(HttpClient client, StudentAccountAssertionTokenFactory assertions) : IStudentAccountConfirmationClient
{
    public async Task<bool?> ConfirmAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/student-account-confirmations") { Content = JsonContent.Create(new { studentId }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertions.Create(tenantId));
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Count() == 1
                && root.TryGetProperty("eligible", out var eligible) && eligible.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? eligible.GetBoolean() : null;
        }
        catch (JsonException) { return null; }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }
}
