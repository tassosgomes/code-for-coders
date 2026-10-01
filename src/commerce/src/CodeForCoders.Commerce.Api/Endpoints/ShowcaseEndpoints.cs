using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.UseCases.Showcase.GetShowcaseCourse;
using CodeForCoders.Commerce.Application.UseCases.Showcase.ListShowcaseCourses;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class ShowcaseEndpoints
{
    public const string Prefix = "/internal/v1/showcase";

    private const int DefaultSize = 12;

    public static void MapShowcaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix)
            .RequireAuthorization(ShowcasePolicies.Read)
            .WithTags("Showcase")
            .AddEndpointFilter(async (context, next) =>
            {
                // Public reads are never cached by this delivery (techspec, decisões técnicas).
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });
        group.MapGet("/courses", ListAsync).WithName("listShowcaseCoursesInternal");
        group.MapGet("/courses/{courseId:guid}", GetAsync).WithName("getShowcaseCourseInternal");
    }

    private static async Task<IResult> ListAsync(
        IListShowcaseCourses useCase,
        CancellationToken cancellationToken,
        string? level = null,
        int _page = 1,
        int _size = DefaultSize)
        => Results.Ok(await useCase.ExecuteAsync(new ListShowcaseCoursesInput(level, _page, _size), cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid courseId,
        IGetShowcaseCourse useCase,
        CancellationToken cancellationToken)
        => Results.Ok(await useCase.ExecuteAsync(new GetShowcaseCourseInput(courseId), cancellationToken));
}
