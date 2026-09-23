namespace CodeForCoders.BffStudent.Api.ApiModels;

public sealed record StudentSessionResponse(Guid AccountId, string Name, string CsrfToken);
