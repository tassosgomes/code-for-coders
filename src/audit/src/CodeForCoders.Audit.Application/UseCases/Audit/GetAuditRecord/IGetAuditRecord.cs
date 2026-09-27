using CodeForCoders.Audit.Application.UseCases;

namespace CodeForCoders.Audit.Application.UseCases.Audit.GetAuditRecord;

public interface IGetAuditRecord : IUseCase<GetAuditRecordInput, AuditRecordDetailOutput?>;
