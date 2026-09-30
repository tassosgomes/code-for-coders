namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record CourseVersionSummaryPage(IReadOnlyList<CourseVersionSummary> Data, CoursePagination Pagination);
