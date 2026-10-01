using System.Diagnostics;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Showcase.ListShowcaseCourses;

public sealed class ListShowcaseCourses(IShowcaseQueries queries, IValidator<ListShowcaseCoursesInput> validator)
    : IListShowcaseCourses
{
    private const string Operation = "listShowcaseCourses";

    public async Task<ShowcaseCoursePage> ExecuteAsync(ListShowcaseCoursesInput input, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        if (!validation.IsValid)
        {
            CommerceTelemetry.ShowcaseReads.Add(1, Tags("invalid_request"));
            throw new ValidationException(validation.Errors);
        }

        var page = await queries.ListAsync(input.Level, input.Page, input.Size, cancellationToken);
        CommerceTelemetry.ShowcaseReads.Add(1, Tags("ok"));
        return page;
    }

    private static TagList Tags(string result) => new()
    {
        { "operation", Operation },
        { "result", result },
    };
}
