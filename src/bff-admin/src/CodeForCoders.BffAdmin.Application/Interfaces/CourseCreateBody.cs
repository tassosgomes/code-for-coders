namespace CodeForCoders.BffAdmin.Application.Interfaces;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CourseCreateBody(string Title, string? Description);
