using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;
using CodeForCoders.Commerce.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GrantCourtesy;

public sealed class GrantCourtesy(ICourtesyGrantStore store, IStudentAccountConfirmationClient identity,
    IUnitOfWork unitOfWork, IEntitlementOutboxMessageWriter outbox, IValidator<GrantCourtesyInput> validator,
    TimeProvider clock, SchoolTimeZone zone) : IGrantCourtesy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GrantCourtesyResult> ExecuteAsync(GrantCourtesyInput input, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        if (!validation.IsValid)
        {
            var field = validation.Errors[0].PropertyName;
            throw new EntitlementRuleException("FIELD_INVALID", $"{char.ToLowerInvariant(field[0])}{field[1..]} is invalid.");
        }
        var scope = new GrantScope(input.TenantId, input.ActorId, Hash(input.IdempotencyKey));
        var requestHash = Hash(JsonSerializer.Serialize(new { input.StudentId, input.CourseId, input.AccessPeriod, input.Reason }, JsonOptions));
        var receipt = await store.FindReceiptAsync(scope, cancellationToken);
        if (Replay(receipt, requestHash) is { } replay) return replay;
        var title = await store.FindCourseTitleAsync(input.CourseId, cancellationToken)
            ?? throw new EntitlementRuleException("COURSE_NOT_ELIGIBLE", "Course is not eligible.");
        var eligible = await identity.ConfirmAsync(input.TenantId, input.StudentId, cancellationToken);
        if (eligible is null) throw new StudentAccountCheckUnavailableException();
        if (!eligible.Value) throw new EntitlementRuleException("STUDENT_ACCOUNT_NOT_ELIGIBLE", "Student account is not eligible.");
        await using var transaction = await store.LockAsync(scope, cancellationToken);
        receipt = await store.FindReceiptAsync(scope, cancellationToken);
        if (Replay(receipt, requestHash) is { } concurrentReplay) return concurrentReplay;
        var now = clock.GetUtcNow();
        var enrollment = await store.GetOrCreateEnrollmentAsync(input.StudentId, input.CourseId, now, cancellationToken);
        var grant = AccessGrant.CreateCourtesy(enrollment, new(input.ActorId, input.Reason!, input.AccessPeriod!.Type, input.AccessPeriod.Months, now), zone.Zone);
        store.Add(grant);
        var output = CourtesyGrant.FromAccessGrant(grant, title, now);
        await AppendMessagesAsync(input, output, cancellationToken);
        if (receipt is null) receipt = GrantReceipt.Create(input.TenantId, input.ActorId, scope.KeyHash);
        receipt.Store(requestHash, JsonSerializer.Serialize(output, JsonOptions), now);
        store.Add(receipt);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return new(output, false);
    }

    private GrantCourtesyResult? Replay(GrantReceipt? receipt, string requestHash)
    {
        if (receipt is null || receipt.ExpiresAt <= clock.GetUtcNow()) return null;
        if (receipt.RequestHash != requestHash)
            throw new EntitlementRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
        return new(JsonSerializer.Deserialize<CourtesyGrant>(receipt.ResponseJson, JsonOptions)!, true);
    }

    private async Task AppendMessagesAsync(GrantCourtesyInput input, CourtesyGrant grant, CancellationToken cancellationToken)
    {
        var eventId = Guid.CreateVersion7();
        var fact = new
        {
            eventId,
            tenantId = input.TenantId,
            grant.GrantId,
            grant.StudentId,
            grant.CourseId,
            grant.Origin,
            originRef = (Guid?)null,
            grant.AccessPeriod,
            grant.GrantedAt,
            grant.EndsOn,
            grant.ExpiresAt,
            occurredAt = grant.GrantedAt
        };
        await outbox.AppendAsync(new(eventId, input.TenantId, "AcessoConcedido", "matricula.acesso-concedido.v1", fact, grant.GrantedAt, input.TraceParent), cancellationToken);
        var act = new
        {
            fatoId = eventId,
            origem = "matricula",
            tipo = "cortesia-concedida",
            tenantId = input.TenantId,
            praticadoEm = grant.GrantedAt,
            autor = new { tipo = "conta-interna", id = input.ActorId },
            alvo = new { tipo = "conta-aluno", id = grant.StudentId },
            motivo = grant.Reason,
            complemento = new
            {
                curso = grant.CourseId.ToString("D"),
                concessao = grant.GrantId.ToString("D"),
                vigencia = grant.AccessPeriod.Type == "lifetime" ? "vitalicia" : $"{grant.AccessPeriod.Months}m"
            }
        };
        await outbox.AppendAsync(new(Guid.CreateVersion7(), input.TenantId, "AtoPraticado", "auditoria.ato-praticado.v1", act, grant.GrantedAt, input.TraceParent), cancellationToken);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
