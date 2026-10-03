using System.Net.Http.Json;
using System.Security.Cryptography;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.CourseReferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class PlaybackTestContext(VideoLibraryApiFactory factory)
{
    public Guid Tenant { get; } = Guid.CreateVersion7();
    public Guid Student { get; } = Guid.CreateVersion7();
    public Guid Lesson { get; } = Guid.CreateVersion7();
    public Guid Course { get; } = Guid.CreateVersion7();
    public Guid VideoId { get; } = Guid.CreateVersion7();
    public byte[] VideoKey { get; } = RandomNumberGenerator.GetBytes(16);
    public string Prefix => $"{Tenant:D}/{VideoId:D}/hls/";

    public async Task SeedAsync(bool ready, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>(); tenant.Set(Tenant);
        var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var video = Video.Create(new VideoCreateInput(VideoId, Tenant, "Video", Guid.CreateVersion7(), "Actor",
            DateTimeOffset.UtcNow, $"{Tenant:D}/{VideoId:D}/original", 1024, null));
        if (ready)
        {
            var lease = Guid.CreateVersion7(); video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
            var key = scope.ServiceProvider.GetRequiredService<IVideoKeyProtector>().Protect(VideoId, VideoKey);
            video.MarkReady(lease, 60, 1024, key.Ciphertext, key.MasterKeyId);
        }
        context.Videos.Add(video);
        context.CourseVideoReferences.Add(new CourseVideoReference { TenantId = Tenant, CourseId = Course, LessonId = Lesson, VideoId = VideoId });
        await context.SaveChangesAsync(cancellationToken);
        var directory = Path.Combine(Path.GetTempPath(), "playback-" + Guid.CreateVersion7());
        Directory.CreateDirectory(Path.Combine(directory, "480p"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "master.m3u8"), "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1000000\n480p.m3u8\n", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "480p.m3u8"), $"#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"c4c-key:{VideoId:D}\"\n#EXTINF:6,\n480p/segment_00001.ts\n#EXT-X-ENDLIST\n", cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "480p", "segment_00001.ts"), [1, 2, 3, 4], cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IMediaStoragePort>().UploadDirectoryAsync(directory, Prefix, cancellationToken);
        }
        finally { Directory.Delete(directory, true); }
    }

    public HttpClient Client(Guid? student = null, string? email = "student@example.com", string audience = "media")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateStudentToken(Tenant, student ?? Student, email, audience));
        return client;
    }

    public async Task<Guid> OpenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync($"/internal/v1/lessons/{Lesson:D}/playback-sessions", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken);
        return body.GetProperty("sessionId").GetGuid();
    }

    public async Task<int> CountSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant);
        return await scope.ServiceProvider.GetRequiredService<MediaDbContext>().PlaybackSessions.CountAsync(cancellationToken);
    }

    public async Task ExpireAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant);
        await scope.ServiceProvider.GetRequiredService<MediaDbContext>().PlaybackSessions.Where(session => session.SessionId == sessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), cancellationToken);
    }
}
