using CodeForCoders.Audit.Application.UseCases;

namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;

public interface IRecordConsumedAuditEvent
    : IUseCase<RecordConsumedAuditEventInput, RecordConsumedAuditEventOutput>;
