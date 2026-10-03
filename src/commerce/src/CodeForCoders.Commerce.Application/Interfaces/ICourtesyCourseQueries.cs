namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICourtesyCourseQueries
{
    Task<CourtesyCoursePage> ListAsync(CourtesyCoursesQuery input, CancellationToken cancellationToken);
}
