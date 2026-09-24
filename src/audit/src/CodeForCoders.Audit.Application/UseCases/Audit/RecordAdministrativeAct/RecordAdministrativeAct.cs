using System.Diagnostics;
using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.Exceptions;
using CodeForCoders.Audit.Domain.ValueObjects;
using CodeForCoders.Audit.Contracts;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordAdministrativeAct;

public sealed class RecordAdministrativeAct(
    IAuditRecordWriter recordWriter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RecordAdministrativeAct> logger) : IRecordAdministrativeAct, IAuditActRecorder
{
    public Task RecordAsync(AtoPraticado act, CancellationToken cancellationToken)
        => ExecuteAsync(new RecordAdministrativeActInput(act), cancellationToken);

    public async Task<RecordAdministrativeActOutput> ExecuteAsync(
        RecordAdministrativeActInput input,
        CancellationToken cancellationToken)
    {
        var administrativeAct = Map(input.Act);
        var receivedOn = timeProvider.GetUtcNow();
        var record = AuditRecord.CreateConforming(administrativeAct, receivedOn);

        using var activity = AuditTelemetry.ActivitySource.StartActivity("audit.acts.recorded");
        activity?.SetTag("fatoId", administrativeAct.FactId);
        activity?.SetTag("origem", administrativeAct.Origin);
        activity?.SetTag("tipo", administrativeAct.Type);
        activity?.SetTag("tenantId", administrativeAct.TenantId);

        await recordWriter.AppendAsync(record, cancellationToken);
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (AuditRecordAlreadyExistsException)
        {
            var existingFingerprint = await recordWriter.ReadFingerprintAsync(
                record.Origin,
                record.FactId,
                cancellationToken);
            if (existingFingerprint is null)
            {
                throw;
            }

            var outcome = string.Equals(existingFingerprint, record.Fingerprint, StringComparison.Ordinal)
                ? "identical"
                : "divergent";
            AuditTelemetry.ActsRedelivered.Add(
                1,
                new KeyValuePair<string, object?>("outcome", outcome));

            if (outcome == "divergent")
            {
                logger.LogWarning(
                    "Administrative act redelivery has different content {Origem} {FatoId} " +
                    "{OriginalFingerprint} {ReceivedFingerprint}",
                    record.Origin,
                    record.FactId,
                    existingFingerprint,
                    record.Fingerprint);
            }

            return new RecordAdministrativeActOutput(null, null, WasRedelivered: true);
        }

        AuditTelemetry.ActsRecorded.Add(
            1,
            new KeyValuePair<string, object?>("origin", record.Origin),
            new KeyValuePair<string, object?>("type", record.Type),
            new KeyValuePair<string, object?>("conformity", record.Conformity));

        return new RecordAdministrativeActOutput(record.Id, record.ReceivedOn);
    }

    private static AdministrativeAct Map(AtoPraticado act)
        => new(
            act.FatoId,
            act.Origem ?? string.Empty,
            act.Tipo,
            act.TenantId,
            act.PraticadoEm,
            MapReference(act.Autor),
            MapReference(act.Alvo),
            act.Complemento,
            act.Motivo);

    private static AdministrativeActReference? MapReference(ReferenciaAto? reference)
        => reference is null ? null : new AdministrativeActReference(reference.Tipo, reference.Id);
}
