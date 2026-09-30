namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseClientResult(int Status, CoursePage? Page, CourseDetail? Course, string? Code, System.Text.Json.JsonElement? Errors, string? Location = null);
