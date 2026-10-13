using System.Diagnostics.CodeAnalysis;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Application.UseCases.Transcriptions.ProcessTranscription;

public sealed class ProcessTranscription(
    ITranscriptionWorkerRepository repository,
    ITranscriptionAudioExtractor extractor,
    ITranscriptionServiceClient service,
    IMediaStoragePort storage,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopes,
    IOptions<TranscriptionOptions> options,
    TimeProvider clock,
    ILogger<ProcessTranscription> logger) : ITranscriptionWorkflow
{
    public async Task<bool> ExecuteNextAsync(TranscriptionCycle cycle, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var work = await repository.ClaimNextAsync(new(clock.GetUtcNow(), TimeSpan.FromSeconds(settings.LeaseDurationSeconds),
            cycle.AvailableDiskBytes >= settings.MinimumFreeDiskBytes), cancellationToken);
        if (work is null) return false;
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var renewalStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewal = RenewAsync(work, processing, renewalStop.Token);
        try { await ProcessAsync(work, cycle.WorkDirectory, processing.Token); }
        finally
        {
            await renewalStop.CancelAsync();
            await renewal;
        }
        return true;
    }

    public async Task MaintainAsync(string workDirectory, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await repository.DeleteOldEventsAsync(now.AddDays(-7), cancellationToken);
        foreach (var work in await repository.GetExpiredAsync(now, cancellationToken))
        {
            var recovered = false;
            await repository.WithLeaseAsync(work, async (attempt, token) =>
            {
                if (attempt.LeaseUntil > now) return;
                attempt.RecoverExpiredLease(now);
                await unitOfWork.CommitAsync(token);
                recovered = true;
            }, cancellationToken);
            if (recovered)
            {
                var directory = work.LocalDirectory(workDirectory);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                var parent = Path.GetDirectoryName(directory)!;
                if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
            }
        }
        foreach (var work in await repository.GetAudioCleanupAsync(cancellationToken))
        {
            await storage.DeleteObjectAsync(work.AudioObjectKey, cancellationToken);
            await repository.MarkCleanupAsync(work.Attempt.TranscriptionId, now, cancellationToken);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed attempt is isolated; exception payloads are deliberately excluded from telemetry.")]
    private async Task ProcessAsync(TranscriptionWorkItem work, string workDirectory, CancellationToken cancellationToken)
    {
        try
        {
            if (work.Video.DurationSeconds > 7200)
            {
                await FailAsync(work, "duration-limit", cancellationToken);
                return;
            }
            if (work.Attempt.WhisperJobId is null)
            {
                await extractor.ExtractAndUploadAsync(work, workDirectory, cancellationToken);
                await SubmitAsync(work, cancellationToken);
                return;
            }
            if (work.Attempt.DeadlineAt <= clock.GetUtcNow())
            {
                await FailAsync(work, "timed-out", cancellationToken);
                return;
            }
            await PollAsync(work, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ConcurrencyConflictException) { }
        catch (Exception)
        {
            // Do not attach an exception: upstream errors may echo a URL, body or credential.
            logger.LogWarning("A transcription stage failed; the attempt will be finalized without publishing content.");
            await FailAsync(work, "processing-failed", cancellationToken);
        }
    }

    private async Task SubmitAsync(TranscriptionWorkItem work, CancellationToken cancellationToken)
    {
        using var activity = MediaTelemetry.ActivitySource.StartActivity("media.transcription.submit");
        var source = await storage.CreateTranscriptionReadUriAsync(work.AudioObjectKey,
            clock.GetUtcNow().AddMinutes(options.Value.SourceUrlLifetimeMinutes), cancellationToken);
        var jobId = await service.SubmitAsync(new(work.Attempt.TranscriptionId, work.Attempt.Submissions + 1, source), cancellationToken);
        var now = clock.GetUtcNow();
        await ChangeAsync(work, (attempt, _) =>
        {
            attempt.MarkSubmitted(jobId, now, TimeSpan.FromHours(options.Value.MaxWaitHours), options.Value.PollDelay(now, now));
            return Task.CompletedTask;
        }, cancellationToken);
    }

    private async Task PollAsync(TranscriptionWorkItem work, CancellationToken cancellationToken)
    {
        string? status;
        using (MediaTelemetry.ActivitySource.StartActivity("media.transcription.poll"))
            status = await service.GetStatusAsync(work.Attempt.WhisperJobId!, cancellationToken);
        var audioDeleted = work.Attempt.AudioDeletedAt is not null;
        if (!audioDeleted && status is "queued" or "processing" or "completed" or "failed")
        {
            await storage.DeleteObjectAsync(work.AudioObjectKey, cancellationToken);
            audioDeleted = true;
        }
        if (status == "completed")
        {
            await CompleteAsync(work, audioDeleted, cancellationToken);
            return;
        }
        if (status is "failed" or null)
        {
            await FailAsync(work, "processing-failed", cancellationToken);
            return;
        }
        if (status is not ("queued" or "downloading" or "processing"))
            logger.LogWarning("The transcription service returned an unknown state; polling will continue.");
        var now = clock.GetUtcNow();
        await ChangeAsync(work, (attempt, _) =>
        {
            if (audioDeleted) attempt.MarkAudioDeleted(now);
            attempt.SchedulePoll(now.Add(options.Value.PollDelay(attempt.SubmittedAt!.Value, now)));
            return Task.CompletedTask;
        }, cancellationToken);
    }

    private async Task CompleteAsync(TranscriptionWorkItem work, bool audioDeleted, CancellationToken cancellationToken)
    {
        TranscriptionContent content;
        using (MediaTelemetry.ActivitySource.StartActivity("media.transcription.fetch-result"))
        {
            var result = await service.GetResultAsync(work.Attempt.WhisperJobId!, cancellationToken);
            content = TranscriptionResultValidation.Validate(work.Attempt.TranscriptionId, work.Attempt.TenantId, result ?? []);
        }
        if (!audioDeleted) await storage.DeleteObjectAsync(work.AudioObjectKey, cancellationToken);
        await ChangeAsync(work, async (attempt, token) =>
        {
            await repository.ReplaceContentAsync(content, token);
            attempt.MarkAudioDeleted(clock.GetUtcNow());
            attempt.MarkReady(content, clock.GetUtcNow());
        }, cancellationToken);
    }

    private async Task FailAsync(TranscriptionWorkItem work, string reason, CancellationToken cancellationToken)
    {
        var changed = await ChangeAsync(work, (attempt, _) =>
        {
            attempt.MarkFailed(reason);
            return Task.CompletedTask;
        }, cancellationToken);
        if (!changed) return;
        await storage.DeleteObjectAsync(work.AudioObjectKey, cancellationToken);
        await repository.MarkCleanupAsync(work.Attempt.TranscriptionId, clock.GetUtcNow(), cancellationToken);
    }

    private Task<bool> ChangeAsync(TranscriptionWorkItem work, Func<Transcription, CancellationToken, Task> change, CancellationToken cancellationToken)
        => repository.WithLeaseAsync(work, async (attempt, token) =>
        {
            if (attempt.LeaseUntil <= clock.GetUtcNow()) throw new ConcurrencyConflictException();
            await change(attempt, token);
            await unitOfWork.CommitAsync(token);
        }, cancellationToken);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Loss of a renewal stops processing without exposing downstream exception payloads.")]
    private async Task RenewAsync(TranscriptionWorkItem work, CancellationTokenSource processing, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.LeaseRenewalIntervalSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await using var scope = scopes.CreateAsyncScope();
                var now = clock.GetUtcNow();
                var renewed = await scope.ServiceProvider.GetRequiredService<ITranscriptionWorkerRepository>()
                    .RenewLeaseAsync(new(work.Attempt.TranscriptionId, work.Attempt.LeaseId!.Value, now,
                        now.AddSeconds(options.Value.LeaseDurationSeconds)), cancellationToken);
                if (!renewed) { await processing.CancelAsync(); return; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) { await processing.CancelAsync(); }
    }
}
