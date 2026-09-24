namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordAdministrativeAct;

public sealed record RecordAdministrativeActOutput(
    Guid? RecordId,
    DateTimeOffset? ReceivedOn,
    bool WasRedelivered = false);
