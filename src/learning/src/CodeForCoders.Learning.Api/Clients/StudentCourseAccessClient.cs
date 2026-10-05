using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Learning.Api.Clients;

public sealed class StudentCourseAccessClient(HttpClient client, AccessDecisionAssertionFactory assertions,
    IOptions<AccessDecisionOptions> options) : IStudentCourseAccessClient
{
    private const int MaximumCourses = 500;

    public async Task<StudentCourseAccessList?> ListAsync(StudentCourseAccessQuery input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SigningKeyBase64)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/course-access?studentId={input.StudentId:D}");
        request.Headers.Authorization = new("Bearer", assertions.Create(input.TenantId, "course-access:read"));
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<StudentCourseAccessList>(cancellationToken);
            return IsValid(result) ? result : null;
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    private static bool IsValid(StudentCourseAccessList? result)
        => result?.Data is { Count: <= MaximumCourses } data && data.All(item => item is not null
            && item.CourseId != Guid.Empty && !string.IsNullOrWhiteSpace(item.Status)
            && (item.Status != "active" || item.Since.HasValue)
            && (item.Status != "ended" || item.EndedOn.HasValue && item.EndedAt.HasValue && !string.IsNullOrWhiteSpace(item.EndedReason)))
            && data.Select(item => item.CourseId).Distinct().Count() == data.Count;
}
