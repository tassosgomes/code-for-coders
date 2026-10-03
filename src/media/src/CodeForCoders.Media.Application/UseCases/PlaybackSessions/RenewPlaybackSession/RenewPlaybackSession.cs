using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.Common;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.RenewPlaybackSession;

public sealed class RenewPlaybackSession(IPlaybackRepository repository, IAccessDecisionClient decisions,
    ISegmentDeliveryPort delivery, IUnitOfWork unitOfWork, TimeProvider clock) : IRenewPlaybackSession
{
    public async Task<PlaybackSessionOutput> ExecuteAsync(RenewPlaybackSessionInput input, CancellationToken cancellationToken)
    {
        var session = await repository.FindSessionAsync(input.SessionId, cancellationToken);
        if (session is null || session.StudentId != input.StudentId)
            throw new MediaApiException(404, "PLAYBACK_SESSION_NOT_FOUND", "Sessão de reprodução não encontrada.");
        if (session.ExpiresAt <= clock.GetUtcNow()) throw Expired();
        var decision = await decisions.DecideFreshAsync(new(session.TenantId, input.StudentId, session.CourseId), cancellationToken)
            ?? throw new MediaApiException(503, "ACCESS_DECISION_UNAVAILABLE", "Não foi possível verificar o acesso à aula.");
        if (decision.Decision != "allowed")
            throw new MediaApiException(403, "ACCESS_DENIED", "Você não tem acesso a esta aula.")
            {
                Reason = decision.DeniedReason,
                AccessEndedAt = decision.LastExpiredAt,
            };
        if (string.IsNullOrWhiteSpace(input.Email))
            throw new MediaApiException(422, "WATERMARK_UNAVAILABLE", "Não foi possível iniciar a aula.");
        // Recheck after the decision: a slow dependency must not revive an expired session.
        if (!session.TryRenew(clock.GetUtcNow())) throw Expired();
        var access = delivery.CreateSegmentAccess($"{session.TenantId:D}/{session.VideoId:D}/hls/", session.ExpiresAt);
        await unitOfWork.CommitAsync(cancellationToken);
        return new(session.SessionId, session.LessonId, session.ExpiresAt, session.ExpiresAt.AddSeconds(-90),
            new(input.Email, 30), new(30, 10), access.Access);
    }

    private static MediaApiException Expired() => new(410, "PLAYBACK_SESSION_EXPIRED", "A sessão de reprodução terminou.");
}
