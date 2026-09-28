namespace CodeForCoders.Audit.Application.UseCases.Audit.GetAuditRecord;

public sealed record GetAuditRecordInput(Guid TenantId, Guid RecordId);
