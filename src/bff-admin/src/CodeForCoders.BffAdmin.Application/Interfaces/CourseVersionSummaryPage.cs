namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseVersionSummaryPage(IReadOnlyList<CourseVersionSummary> Data, CoursePagination Pagination);
