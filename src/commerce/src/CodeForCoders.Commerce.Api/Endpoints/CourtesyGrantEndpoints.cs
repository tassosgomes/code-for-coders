using CodeForCoders.Commerce.Api.ApiModels;
using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GrantCourtesy;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GetCourtesyGrant;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.PreviewCourtesyTerm;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class CourtesyGrantEndpoints
{
    public static void MapCourtesyGrantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1").RequireAuthorization(CourtesyPolicies.Grant).WithTags("Courtesies");
        group.MapGet("/courtesy-term-preview", PreviewAsync).WithName("previewCourtesyTermInternal");
        group.MapPost("/courtesy-grants", GrantAsync).WithName("grantCourtesyInternal");
        group.MapGet("/courtesy-grants/{grantId:guid}", GetAsync).WithName("getCourtesyGrantInternal");
    }

    private static async Task<IResult> PreviewAsync(int months, HttpContext context, ITenantContext tenant,
        IPreviewCourtesyTerm useCase, CancellationToken cancellationToken)
    {
        if (!SetTenant(context, tenant)) return Problem(401, "TOKEN_INVALID");
        return Results.Ok(await useCase.ExecuteAsync(months, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid grantId, HttpContext context, ITenantContext tenant,
        IGetCourtesyGrant useCase, CancellationToken cancellationToken)
    {
        if (!SetTenant(context, tenant)) return Problem(401, "TOKEN_INVALID");
        return Results.Ok(await useCase.ExecuteAsync(grantId, cancellationToken));
    }

    private static async Task<IResult> GrantAsync(CourtesyGrantRequest body, HttpContext context, ITenantContext tenant,
        IGrantCourtesy useCase, CancellationToken cancellationToken)
    {
        if (!SetTenant(context, tenant) || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var actor) || actor == Guid.Empty)
            return Problem(401, "TOKEN_INVALID");
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128) return Problem(400, "INVALID_REQUEST");
        var result = await useCase.ExecuteAsync(new(tenant.TenantId!.Value, actor, body.StudentId, body.CourseId, body.AccessPeriod?.ToInput(),
            body.Reason, key, System.Diagnostics.Activity.Current?.Id ?? context.Request.Headers["traceparent"].FirstOrDefault()), cancellationToken);
        return result.Replayed ? Results.Ok(result.Grant) : Results.Created($"/internal/v1/courtesy-grants/{result.Grant.GrantId:D}", result.Grant);
    }

    private static bool SetTenant(HttpContext context, ITenantContext tenant)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var id) || id == Guid.Empty) return false;
        tenant.Set(id); return true;
    }
    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status, title: "Courtesy request rejected.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
