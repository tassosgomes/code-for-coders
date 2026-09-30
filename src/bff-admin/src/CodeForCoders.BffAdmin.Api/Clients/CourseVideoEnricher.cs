using System.Text.Json;
using System.Text.Json.Nodes;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CourseVideoEnricher(IVideoLibraryClient media, IStaffSessionIdentityClient identity) : IDisposable
{
    private const int MaxConcurrentRequests = 4;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);
    private readonly SemaphoreSlim slots = new(MaxConcurrentRequests);

    public async Task<CourseDetail> EnrichAsync(CourseDetail course, Guid sessionId, CancellationToken cancellationToken)
    {
        if (!course.Modules.SelectMany(module => module.GetProperty("lessons").EnumerateArray())
            .Any(lesson => lesson.TryGetProperty("video", out var video) && video.ValueKind == JsonValueKind.Object)) return course;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Budget);
        try
        {
            var validated = await identity.ValidateSessionAsync(sessionId, "media", deadline.Token);
            if (validated.StatusCode != 200 || string.IsNullOrEmpty(validated.Session?.AccessToken)) return course;
            var modules = course.Modules.Select(module => JsonNode.Parse(module.GetRawText())!.AsObject()).ToArray();
            var videos = modules.SelectMany(module => module["lessons"]!.AsArray())
                .Select(lesson => lesson?["video"] as JsonObject).OfType<JsonObject>().ToArray();
            var ids = videos.Select(video => video["videoId"]!.GetValue<Guid>()).Distinct().ToArray();
            var results = await Task.WhenAll(ids.Select(id => ReadAsync(id, validated.Session.AccessToken, deadline.Token)));
            var available = results.Where(result => result.Video is not null).ToDictionary(result => result.Id, result => result.Video!);
            foreach (var video in videos)
            {
                if (!available.TryGetValue(video["videoId"]!.GetValue<Guid>(), out var details)) continue;
                video["title"] = details.Title;
                if (details.DurationSeconds.HasValue) video["durationSeconds"] = details.DurationSeconds.Value;
            }
            return course with { Modules = modules.Select(module => JsonSerializer.SerializeToElement(module)).ToArray() };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return course; }
        catch (HttpRequestException) { return course; }
    }

    private async Task<(Guid Id, ApiModels.VideoResponse? Video)> ReadAsync(Guid id, string token, CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken);
        try
        {
            var response = await media.GetVideoAsync(id, token, cancellationToken);
            return (id, response.StatusCode == System.Net.HttpStatusCode.OK && response.Video is { Status: "ready" } video && video.VideoId == id ? video : null);
        }
        finally { slots.Release(); }
    }

    public void Dispose() => slots.Dispose();
}
