using CodeForCoders.Audit.Contracts;

namespace CodeForCoders.Audit.Application.Interfaces;

public interface IAuditActRecorder
{
    Task RecordAsync(AtoPraticado act, CancellationToken cancellationToken);
}
