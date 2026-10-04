using CodeForCoders.Media.Application.Common;
using System.Text;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetPlaybackResource;

public sealed class GetPlaybackResource(IPlaybackRepository repository, IPlaybackPlaylistStore playlists,
    IVideoKeyProtector keys, ISegmentDeliveryPort delivery, TimeProvider clock) : IGetPlaybackResource
{
    public async Task<PlaybackResourceOutput> ExecuteAsync(GetPlaybackResourceInput input, CancellationToken cancellationToken)
    {
        var session = await repository.FindSessionAsync(input.SessionId, cancellationToken);
        if (session is null || session.StudentId != input.StudentId)
            throw new MediaApiException(404, "PLAYBACK_SESSION_NOT_FOUND", "Sessão de reprodução não encontrada.");
        if (session.ExpiresAt <= clock.GetUtcNow())
            throw new MediaApiException(410, "PLAYBACK_SESSION_EXPIRED", "A sessão de reprodução terminou.");
        if (input.Resource == "key")
        {
            var key = await repository.FindVideoKeyAsync(session.VideoId, cancellationToken)
                ?? throw new MediaApiException(404, "PLAYBACK_SESSION_NOT_FOUND", "Sessão de reprodução não encontrada.");
            return new(keys.Unprotect(key.VideoId, key.MasterKeyId, key.Ciphertext), "application/octet-stream");
        }
        if (input.Resource == "variant" && (input.Quality is null || !PlaylistRewriter.IsQuality(input.Quality)))
            throw new MediaApiException(404, "PLAYBACK_SESSION_NOT_FOUND", "Sessão de reprodução não encontrada.");
        var prefix = $"{session.TenantId:D}/{session.VideoId:D}/hls/";
        var original = await playlists.ReadAsync(prefix + (input.Resource == "variant" ? $"{input.Quality}.m3u8" : "master.m3u8"), cancellationToken);
        var segmentBase = delivery.CreateSegmentAccess(prefix, session.ExpiresAt).BaseAddress;
        return new(Encoding.UTF8.GetBytes(PlaylistRewriter.Rewrite(original, session.VideoId, segmentBase)), "application/vnd.apple.mpegurl");
    }
}
