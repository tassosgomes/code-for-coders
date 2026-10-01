using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record ShowcaseListResult(int StatusCode, string? Code, ShowcaseCoursePageV1? Page = null);
