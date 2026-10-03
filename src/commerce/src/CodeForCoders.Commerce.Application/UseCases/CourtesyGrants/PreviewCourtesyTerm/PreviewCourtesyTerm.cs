using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.PreviewCourtesyTerm;

public sealed class PreviewCourtesyTerm(TimeProvider clock, SchoolTimeZone zone) : IPreviewCourtesyTerm
{
    public Task<CourtesyTermPreview> ExecuteAsync(int input, CancellationToken cancellationToken)
    {
        if (input is < 1 or > 60) throw new FluentValidation.ValidationException("Months must be between 1 and 60.");
        var now = clock.GetUtcNow();
        var term = CodeForCoders.Commerce.Domain.Entities.AccessTerm.Calculate(now, "months", input, zone.Zone);
        return Task.FromResult(new CourtesyTermPreview(input, now, term.EndsOn!.Value, term.ExpiresAt!.Value));
    }
}
