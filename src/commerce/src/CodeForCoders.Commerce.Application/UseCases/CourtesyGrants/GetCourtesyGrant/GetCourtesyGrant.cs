using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GetCourtesyGrant;

public sealed class GetCourtesyGrant(ICourtesyGrantStore store, TimeProvider clock) : IGetCourtesyGrant
{
    public async Task<CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common.CourtesyGrant> ExecuteAsync(Guid input, CancellationToken cancellationToken)
    {
        var grant = await store.GetAsync(input, cancellationToken);
        if (grant is null || grant.Origin != "courtesy") throw new CodeForCoders.Commerce.Application.Exceptions.NotFoundException("GRANT_NOT_FOUND");
        var title = await store.FindCourseTitleAsync(grant.CourseId, cancellationToken) ?? "";
        return CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common.CourtesyGrant.FromAccessGrant(grant, title, clock.GetUtcNow());
    }
}
