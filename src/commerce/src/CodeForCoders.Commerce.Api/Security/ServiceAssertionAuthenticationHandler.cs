using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Api.Security;

/// <summary>Authenticates trusted service issuers; each route enforces its own scope.</summary>
public sealed class ServiceAssertionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ServiceAssertionVerifier verifier,
    ITenantContext tenantContext)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    public const string SchemeName = "ServiceAssertion";
    public const string ScopeClaim = "scope";
    public const string TenantClaim = "tenantId";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadBearerToken();
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var assertion = await verifier.VerifyAsync(token, Context.RequestAborted);
        if (assertion is null)
        {
            return AuthenticateResult.Fail("The service assertion is invalid.");
        }

        // The tenant comes from the verified assertion, never from a request parameter (ADR-0009).
        tenantContext.Set(assertion.TenantId);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, assertion.Issuer),
            new(TenantClaim, assertion.TenantId.ToString("D")),
        };
        claims.AddRange(assertion.GrantedScopes.Select(scope => new Claim(ScopeClaim, scope)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Record("unauthorized");
        return SecurityProblem.WriteAsync(
            Context,
            StatusCodes.Status401Unauthorized,
            IsEntitlementRead() ? "SERVICE_UNAUTHORIZED" : "SERVICE_ASSERTION_INVALID",
            "Credencial de serviço inválida.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Record("forbidden");
        return SecurityProblem.WriteAsync(
            Context,
            StatusCodes.Status403Forbidden,
            "SCOPE_DENIED",
            "Escopo insuficiente.");
    }

    private string? ReadBearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && header.Length > prefix.Length
            ? header[prefix.Length..].Trim()
            : null;
    }

    private void Record(string result)
    {
        var tags = new TagList
        {
            { "operation", Context.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? "unknown" },
            { "result", result },
        };
        if (!IsEntitlementRead()) CommerceTelemetry.ShowcaseReads.Add(1, tags);
    }

    private bool IsEntitlementRead()
        => Context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(policy => policy.Policy == AccessDecisionPolicies.Read || policy.Policy == StudentCourseAccessPolicies.Read) == true;
}
