using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.Common;
using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;

public sealed class OpenPlaybackSession(IPlaybackRepository repository, IAccessDecisionClient decisions,
    ISegmentDeliveryPort delivery, IUnitOfWork unitOfWork, TimeProvider clock) : IOpenPlaybackSession
{
    public async Task<PlaybackSessionOutput> ExecuteAsync(OpenPlaybackSessionInput input, CancellationToken cancellationToken)
    {
        var lesson = await repository.FindLessonAsync(input.LessonId, cancellationToken);
        if (lesson is null)
        {
            MediaTelemetry.RecordPlaybackRejected("referencia_ausente");
            throw new MediaApiException(404, "LESSON_NOT_AVAILABLE", "Esta aula não está disponível.");
        }
        if (lesson.Status != "ready")
        {
            MediaTelemetry.RecordPlaybackRejected("video_nao_pronto");
            throw new MediaApiException(409, "MEDIA_NOT_READY", "Esta aula está indisponível no momento.");
        }
        var decision = await decisions.DecideAsync(new(input.TenantId, input.StudentId, lesson.CourseId), cancellationToken);
        if (decision is null)
        {
            MediaTelemetry.RecordPlaybackRejected("indisponivel");
            throw new MediaApiException(503, "ACCESS_DECISION_UNAVAILABLE", "Não foi possível verificar o acesso à aula.");
        }
        if (decision.Decision != "allowed")
        {
            MediaTelemetry.RecordPlaybackRejected("negada");
            throw new MediaApiException(403, "ACCESS_DENIED", "Você não tem acesso a esta aula.");
        }
        if (string.IsNullOrWhiteSpace(input.Email))
        {
            MediaTelemetry.RecordPlaybackRejected("sem_email");
            throw new MediaApiException(422, "WATERMARK_UNAVAILABLE", "Não foi possível iniciar a aula.");
        }
        var session = PlaybackSession.Create(new(input.TenantId, input.StudentId, input.LessonId, lesson.CourseId, lesson.VideoId, clock.GetUtcNow()));
        var access = delivery.CreateSegmentAccess($"{input.TenantId:D}/{lesson.VideoId:D}/hls/", session.ExpiresAt);
        repository.Add(session);
        await unitOfWork.CommitAsync(cancellationToken);
        MediaTelemetry.RecordPlaybackOpened();
        return new(session.SessionId, session.LessonId, session.ExpiresAt, session.ExpiresAt.AddSeconds(-90),
            new(input.Email, 30), new(30, 10), access.Access);
    }
}
