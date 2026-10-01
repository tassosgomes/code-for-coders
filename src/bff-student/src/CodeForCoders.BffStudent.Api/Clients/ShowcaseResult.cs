namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record ShowcaseResult<T>(int StatusCode, string? Code, T? Body = default)
    where T : class;
