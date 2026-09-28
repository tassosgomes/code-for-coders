using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Contracts;

namespace CodeForCoders.Audit.Application.Interfaces;

public interface IAuditComplementRecorder
{
    Task<AuditComplementRecordStatus> RecordAsync(
        ComplementoConfirmadoV1 complement,
        CancellationToken cancellationToken);
}
