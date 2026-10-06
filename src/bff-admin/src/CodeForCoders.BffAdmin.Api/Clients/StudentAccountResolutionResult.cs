namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record StudentAccountResolutionResult(int StatusCode, string? Code, IReadOnlyList<ResolvedStudentAccount>? Data);
