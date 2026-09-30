using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

internal sealed record CourseOperation(string Path, CourseCreateBody? Body);
