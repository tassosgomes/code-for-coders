namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record CoursePage(IReadOnlyList<CourseSummary> Data, CoursePagination Pagination);
