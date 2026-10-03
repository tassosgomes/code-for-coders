using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;

public sealed class OpenPlaybackSession(IPlaybackRepository repository, IAccessDecisionClient decisions,
    ISegmentDeliveryPort delivery, IUnitOfWork unitOfWork, TimeProvider clock) : IOpenPlaybackSession
{
    public async Task<PlaybackSessionOutput> ExecuteAsync(OpenPlaybackSessionInput input, CancellationToken cancellationToken)
    {
        var lesson = await repository.FindLessonAsync(input.LessonId, cancellationToken)
            ?? throw new MediaApiException(404, "LESSON_NOT_AVAILABLE", "Esta aula não está disponível.");
        if (lesson.Status != "ready")
            throw new MediaApiException(409, "MEDIA_NOT_READY", "Esta aula está indisponível no momento.");
        var decision = await decisions.DecideAsync(new(input.TenantId, input.StudentId, lesson.CourseId), cancellationToken)
            ?? throw new MediaApiException(503, "ACCESS_DECISION_UNAVAILABLE", "Não foi possível verificar o acesso à aula.");
        if (decision.Decision != "allowed")
            throw new MediaApiException(403, "ACCESS_DENIED", "Você não tem acesso a esta aula.");
        if (string.IsNullOrWhiteSpace(input.Email))
            throw new MediaApiException(422, "WATERMARK_UNAVAILABLE", "Não foi possível iniciar a aula.");
        var session = PlaybackSession.Create(new(input.TenantId, input.StudentId, input.LessonId, lesson.CourseId, lesson.VideoId, clock.GetUtcNow()));
        var access = delivery.CreateSegmentAccess($"{input.TenantId:D}/{lesson.VideoId:D}/hls/", session.ExpiresAt);
        repository.Add(session);
        await unitOfWork.CommitAsync(cancellationToken);
        return new(session.SessionId, session.LessonId, session.ExpiresAt, session.ExpiresAt.AddSeconds(-90),
            new(input.Email, 30), new(30, 10), access.Access);
    }
}
