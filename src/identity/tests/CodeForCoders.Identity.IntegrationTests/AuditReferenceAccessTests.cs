using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Endpoints;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

public sealed class AuditReferenceAccessTests
{
    private const string Scope = "audit-references:read";
    private static readonly Guid ReferenceId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    [Fact(DisplayName = nameof(AuditReferenceAccess_ValidAdministratorAssertionResolvesWithinItsTenant))]
    public async Task AuditReferenceAccess_ValidAdministratorAssertionResolvesWithinItsTenant()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var validator = new FixedStaffSessionValidator(isAdministrator: true);
        var resolver = new CapturingAuditIdentityReferenceResolver();
        await using var server = await CreateServer(signingKey, tenantId, validator, resolver);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, CreateAssertion(signingKey, tenantId, Scope), sessionId,
            new AuditIdentityReferenceLookupRequestV1([new AuditIdentityReferenceV1("conta-interna", ReferenceId)]));
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, validator.LastInput?.TenantId);
        Assert.Equal(sessionId, validator.LastInput?.SessionId);
        Assert.Equal(tenantId, resolver.LastInput?.TenantId);
        Assert.Equal(ReferenceId, resolver.LastInput?.References.Single().Id);
        Assert.Equal("Internal Actor", document.RootElement.GetProperty("data")[0].GetProperty("label").GetString());
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RejectsMissingAssertion))]
    public async Task AuditReferenceAccess_RejectsMissingAssertion()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        await using var server = await CreateServer(signingKey, tenantId);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, null, Guid.CreateVersion7(), ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED");
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RejectsAssertionWithInvalidSignature))]
    public async Task AuditReferenceAccess_RejectsAssertionWithInvalidSignature()
    {
        using var signingKey = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        await using var server = await CreateServer(signingKey, tenantId);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, CreateAssertion(otherKey, tenantId, Scope), Guid.CreateVersion7(), ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED");
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RejectsAssertionFromAnotherTenant))]
    public async Task AuditReferenceAccess_RejectsAssertionFromAnotherTenant()
    {
        using var signingKey = RSA.Create(2048);
        var configuredTenantId = Guid.CreateVersion7();
        var unapprovedTenantId = Guid.CreateVersion7();
        await using var server = await CreateServer(signingKey, configuredTenantId);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, CreateAssertion(signingKey, unapprovedTenantId, Scope), Guid.CreateVersion7(), ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED");
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RequiresTheAllowedScope))]
    public async Task AuditReferenceAccess_RequiresTheAllowedScope()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        var validator = new FixedStaffSessionValidator(isAdministrator: true);
        await using var server = await CreateServer(signingKey, tenantId, validator);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client,
            CreateAssertion(signingKey, tenantId, "staff-members:read"),
            Guid.CreateVersion7(),
            ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Null(validator.LastInput);
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RequiresACurrentStaffSession))]
    public async Task AuditReferenceAccess_RequiresACurrentStaffSession()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        var validator = new FixedStaffSessionValidator(isAdministrator: true);
        await using var server = await CreateServer(signingKey, tenantId, validator);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, CreateAssertion(signingKey, tenantId, Scope), null, ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        Assert.Null(validator.LastInput);
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RejectsRevokedStaffSession))]
    public async Task AuditReferenceAccess_RejectsRevokedStaffSession()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        var validator = new FixedStaffSessionValidator(isAdministrator: true, isCurrent: false);
        await using var server = await CreateServer(signingKey, tenantId, validator);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, CreateAssertion(signingKey, tenantId, Scope), Guid.CreateVersion7(), ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RequiresAdministratorRole))]
    public async Task AuditReferenceAccess_RequiresAdministratorRole()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        var validator = new FixedStaffSessionValidator(isAdministrator: false);
        var resolver = new CapturingAuditIdentityReferenceResolver();
        await using var server = await CreateServer(signingKey, tenantId, validator, resolver);
        using var client = server.GetTestClient();

        using var response = await SendAsync(client, CreateAssertion(signingKey, tenantId, Scope), Guid.CreateVersion7(), ValidRequest());

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Null(resolver.LastInput);
    }

    [Fact(DisplayName = nameof(AuditReferenceAccess_RejectsDuplicateReferencesAndOversizedBatches))]
    public async Task AuditReferenceAccess_RejectsDuplicateReferencesAndOversizedBatches()
    {
        using var signingKey = RSA.Create(2048);
        var tenantId = Guid.CreateVersion7();
        await using var server = await CreateServer(signingKey, tenantId);
        using var client = server.GetTestClient();
        var assertion = CreateAssertion(signingKey, tenantId, Scope);
        var sessionId = Guid.CreateVersion7();

        using var duplicateResponse = await SendAsync(client, assertion, sessionId,
            new AuditIdentityReferenceLookupRequestV1([
                new AuditIdentityReferenceV1("conta-interna", ReferenceId),
                new AuditIdentityReferenceV1("conta-interna", ReferenceId),
            ]));
        using var oversizedResponse = await SendAsync(client, assertion, sessionId,
            new AuditIdentityReferenceLookupRequestV1(Enumerable.Range(0, 51)
                .Select(_ => new AuditIdentityReferenceV1("conta-interna", Guid.CreateVersion7()))
                .ToArray()));

        await AssertProblemAsync(duplicateResponse, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await AssertProblemAsync(oversizedResponse, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    private static async Task<WebApplication> CreateServer(
        RSA signingKey,
        Guid allowedTenantId,
        FixedStaffSessionValidator? validator = null,
        CapturingAuditIdentityReferenceResolver? resolver = null)
    {
        var settings = new ServiceAssertionOptions
        {
            Audience = "identity",
            Issuers = new Dictionary<string, ServiceAssertionIssuerOptions>(StringComparer.Ordinal)
            {
                ["bff-admin"] = new()
                {
                    PublicKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["test-key"] = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo()),
                    },
                    AllowedScopes = [Scope],
                    AllowedTenantIds = [allowedTenantId.ToString("D")],
                },
            },
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IOptions<ServiceAssertionOptions>>(Options.Create(settings));
        builder.Services.AddSingleton<IServiceAssertionReplayStore, AcceptingServiceAssertionReplayStore>();
        builder.Services.AddSingleton<ITenantContext, TenantContext>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ServiceAssertionVerifier>();
        builder.Services.AddSingleton<IValidateStaffSession>(validator ?? new FixedStaffSessionValidator(isAdministrator: true));
        builder.Services.AddSingleton<IResolveAuditIdentityReferences>(resolver ?? new CapturingAuditIdentityReferenceResolver());
        var app = builder.Build();
        app.MapAuditIdentityReferenceEndpoints();
        await app.StartAsync();
        return app;
    }

    private static AuditIdentityReferenceLookupRequestV1 ValidRequest()
        => new([new AuditIdentityReferenceV1("conta-interna", ReferenceId)]);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string? assertion,
        Guid? staffSessionId,
        AuditIdentityReferenceLookupRequestV1 body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/audit-identity-reference-lookups")
        {
            Content = JsonContent.Create(body),
        };
        if (assertion is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
        }

        if (staffSessionId is not null)
        {
            request.Headers.Add("X-Staff-Session", staffSessionId.Value.ToString("D"));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode statusCode, string code)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private static string CreateAssertion(RSA signingKey, Guid tenantId, string scope)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = "test-key", typ = "JWT" }));
        var claims = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = "bff-admin",
            aud = "identity",
            sub = "bff-admin",
            tenantId,
            jti = Guid.CreateVersion7(),
            scope,
            iat = now,
            nbf = now,
            exp = now + 30,
        }));
        var signingInput = $"{header}.{claims}";
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Encode(signature)}";
    }

    private static string Encode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class AcceptingServiceAssertionReplayStore : IServiceAssertionReplayStore
    {
        public Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class FixedStaffSessionValidator(bool isAdministrator, bool isCurrent = true) : IValidateStaffSession
    {
        public ValidateStaffSessionInput? LastInput { get; private set; }

        public Task<ValidateStaffSessionOutput?> ExecuteAsync(ValidateStaffSessionInput input, CancellationToken cancellationToken)
        {
            LastInput = input;
            if (!isCurrent)
            {
                return Task.FromResult<ValidateStaffSessionOutput?>(null);
            }

            IReadOnlyList<string> roles = isAdministrator ? [StaffRoleCatalog.Administrator] : ["professor"];
            var session = new StaffSessionDetails(
                input.SessionId,
                Guid.CreateVersion7(),
                "Internal Actor",
                roles,
                [],
                DateTimeOffset.UtcNow.AddMinutes(30));
            return Task.FromResult<ValidateStaffSessionOutput?>(new ValidateStaffSessionOutput(session));
        }
    }

    private sealed class CapturingAuditIdentityReferenceResolver : IResolveAuditIdentityReferences
    {
        public ResolveAuditIdentityReferencesInput? LastInput { get; private set; }

        public Task<ResolveAuditIdentityReferencesOutput> ExecuteAsync(
            ResolveAuditIdentityReferencesInput input,
            CancellationToken cancellationToken)
        {
            LastInput = input;
            return Task.FromResult(new ResolveAuditIdentityReferencesOutput(input.References
                .Select(reference => new ResolvedAuditIdentityReference(reference.Type, reference.Id, "Internal Actor"))
                .ToArray()));
        }
    }
}
