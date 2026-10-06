using System.Net;
using Polly;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
using CodeForCoders.Notification.Application.Interfaces;

namespace CodeForCoders.Notification.Api.Clients;

public sealed class StudentContactClient(HttpClient client) : IStudentContactClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<StudentContact?> GetAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/student-accounts/{studentId:D}/contact");
        request.Options.Set(StudentContactAssertionHandler.TenantKey, tenantId);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var contact = await response.Content.ReadFromJsonAsync<StudentContact>(JsonOptions, cancellationToken);
            if (contact is null || contact.StudentId != studentId || string.IsNullOrWhiteSpace(contact.Email)
                || string.IsNullOrWhiteSpace(contact.Name) || contact.Status is not ("active" or "disabled"))
                throw new TransactionalEmailSendException(NotificationFailureReasons.IdentityUnavailable, true);
            return contact;
        }
        catch (ExecutionRejectedException) { throw new TransactionalEmailSendException(NotificationFailureReasons.IdentityUnavailable, true); }
        catch (HttpRequestException) { throw new TransactionalEmailSendException(NotificationFailureReasons.IdentityUnavailable, true); }
        catch (JsonException) { throw new TransactionalEmailSendException(NotificationFailureReasons.IdentityUnavailable, true); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TransactionalEmailSendException(NotificationFailureReasons.IdentityUnavailable, true); }
    }
}
