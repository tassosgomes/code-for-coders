using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class VideoTitleNormalizationTests
{
    [Fact]
    public void CreateStoresTrimmedAndAccentFreeSearchTitle()
    {
        var video = Create("  Injeção de Dependência  ");
        Assert.Equal("Injeção de Dependência", video.Title);
        Assert.Equal("injecao de dependencia", video.NormalizedTitle);
    }

    [Fact]
    public void UpdateTitleKeepsVideoIdentityAndStatus()
    {
        var video = Create("Primeira aula");
        var videoId = video.VideoId;
        var uploadedAt = video.UploadedAt;
        video.UpdateTitle("  Álgebra Básica  ");
        Assert.Equal("Álgebra Básica", video.Title);
        Assert.Equal("algebra basica", video.NormalizedTitle);
        Assert.Equal(videoId, video.VideoId);
        Assert.Equal(uploadedAt, video.UploadedAt);
        Assert.Equal("received", video.Status);
    }

    [Fact]
    public void UpdateTitleRejectsWhitespaceWithoutChangingStoredTitle()
    {
        var video = Create("Original");
        Assert.Throws<EntityValidationException>(() => video.UpdateTitle("   "));
        Assert.Equal("Original", video.Title);
        Assert.Equal("original", video.NormalizedTitle);
    }

    private static Video Create(string title)
        => Video.Create(Guid.CreateVersion7(), title, Guid.CreateVersion7(), "Professora", DateTimeOffset.UtcNow);
}
