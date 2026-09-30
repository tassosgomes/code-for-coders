namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CoursePage(IReadOnlyList<CourseSummary> Data, CoursePagination Pagination);
