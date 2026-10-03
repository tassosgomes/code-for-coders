namespace CodeForCoders.Media.Application.Interfaces;

public interface IAccessDecisionClient
{
    Task<StudentAccessDecision?> DecideAsync(AccessDecisionQuery query, CancellationToken cancellationToken);
}
