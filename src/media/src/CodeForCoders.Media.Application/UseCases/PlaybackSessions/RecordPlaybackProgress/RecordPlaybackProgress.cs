using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.RecordPlaybackProgress;

public sealed class RecordPlaybackProgress(
    IPlaybackRepository repository,
    IOutboxMessageWriter outboxWriter,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<RecordPlaybackProgress>? logger = null) : IRecordPlaybackProgress
{
    private const int GraceMinutes = 60;
    private const int MinGapSeconds = 10;

    public async Task<RecordPlaybackProgressOutput> ExecuteAsync(RecordPlaybackProgressInput input, CancellationToken cancellationToken)
    {
        var session = await repository.FindSessionAsync(input.SessionId, cancellationToken);
        if (session is null || session.StudentId != input.StudentId || session.TenantId != input.TenantId)
        {
            throw new MediaApiException(404, "PLAYBACK_SESSION_NOT_FOUND", "Sessão de reprodução não encontrada.");
        }

        var now = clock.GetUtcNow();
        if (now > session.ExpiresAt.AddMinutes(GraceMinutes))
        {
            throw new MediaApiException(410, "PLAYBACK_SESSION_EXPIRED", "A sessão de reprodução terminou.");
        }

        var duration = await repository.FindVideoDurationAsync(session.VideoId, cancellationToken);
        if (duration.HasValue)
        {
            if (input.PositionSeconds > duration.Value + 10)
            {
                throw new MediaApiException(400, "VALIDATION_ERROR", "Requisição inválida.");
            }
        }
        else
        {
            // Decisão registrada: vídeos sem DurationSeconds persistido (ex.: testes ou metadados de preparação legados)
            // não aplicam a restrição relativa à duração, dependendo unicamente da validação absoluta (máx. 12h) no endpoint.
            logger?.LogWarning("Playback session {SessionId} video {VideoId} has no duration recorded; relative duration validation skipped.",
                session.SessionId, session.VideoId);
        }

        if (!session.TryRecordProgress(input.Sequence, now, input.PositionSeconds, MinGapSeconds))
        {
            return new RecordPlaybackProgressOutput(Recorded: false);
        }

        var eventId = PlaybackSession.CreateDeterministicEventId(session.SessionId, input.Sequence);
        var payload = new
        {
            eventId,
            tenantId = session.TenantId,
            sessionId = session.SessionId,
            studentId = session.StudentId,
            courseId = session.CourseId,
            lessonId = session.LessonId,
            sequence = input.Sequence,
            positionSeconds = input.PositionSeconds,
            reason = input.Reason,
            occurredAt = now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };

        var draft = new OutboxMessageDraft(
            eventId,
            session.TenantId,
            "midia.reproducao-avancou.v1",
            "midia.reproducao-avancou.v1",
            payload,
            now,
            input.TraceParent);

        await outboxWriter.AppendAsync(draft, cancellationToken);
        var committed = await unitOfWork.TryCommitAsync(cancellationToken);

        return new RecordPlaybackProgressOutput(Recorded: committed);
    }
}
