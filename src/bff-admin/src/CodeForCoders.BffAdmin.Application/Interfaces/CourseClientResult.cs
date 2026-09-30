namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseClientResult(int Status, CoursePage? Page, CourseDetail? Course, string? Code, System.Text.Json.JsonElement? Errors, string? Location = null, CourseVersion? Version = null, System.Text.Json.JsonElement? Pendencies = null, CourseReferencePage? References = null, CourseVersionSummaryPage? Versions = null);
