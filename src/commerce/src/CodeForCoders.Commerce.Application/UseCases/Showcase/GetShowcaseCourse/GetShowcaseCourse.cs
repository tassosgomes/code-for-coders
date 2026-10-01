using System.Diagnostics;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.Showcase.GetShowcaseCourse;

public sealed class GetShowcaseCourse(IShowcaseQueries queries) : IGetShowcaseCourse
{
    public const string NotFoundCode = "SHOWCASE_COURSE_NOT_FOUND";

    private const string Operation = "getShowcaseCourse";

    public async Task<ShowcaseCourseDetail> ExecuteAsync(GetShowcaseCourseInput input, CancellationToken cancellationToken)
    {
        var course = await queries.GetCourseAsync(input.CourseId, cancellationToken);
        if (course is null)
        {
            // Outside the showcase, unknown and another school's course are the same answer (RN-O02).
            CommerceTelemetry.ShowcaseReads.Add(1, Tags("not_found"));
            throw new NotFoundException(NotFoundCode);
        }

        CommerceTelemetry.ShowcaseReads.Add(1, Tags("ok"));
        return course;
    }

    private static TagList Tags(string result) => new()
    {
        { "operation", Operation },
        { "result", result },
    };
}
