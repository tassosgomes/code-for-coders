namespace CodeForCoders.Media.Application.Interfaces;

public interface IAccessDecisionClient
{
    Task<StudentAccessDecision?> DecideAsync(AccessDecisionQuery query, CancellationToken cancellationToken);
    Task<StudentAccessDecision?> DecideFreshAsync(AccessDecisionQuery query, CancellationToken cancellationToken);
}
