using CodeForCoders.BffAdmin.Application.UseCases;

namespace CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;

public interface IConfirmAuditRecordComplement : IUseCase<ConfirmAuditRecordComplementInput, ConfirmAuditRecordComplementOutput>
{
    Task<ConfirmAuditRecordComplementOutput?> TryReplayAsync(
        ConfirmAuditRecordComplementInput input,
        CancellationToken cancellationToken);
}
