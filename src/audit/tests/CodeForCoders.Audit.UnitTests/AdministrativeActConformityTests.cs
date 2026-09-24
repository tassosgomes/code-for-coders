using System.Text.Json;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Audit.UnitTests;

public sealed class AdministrativeActConformityTests
{
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = nameof(UnknownTypeIsRetainedAndMarkedNonConforming))]
    public void UnknownTypeIsRetainedAndMarkedNonConforming()
    {
        var record = CreateRecord(CreateAct() with { Type = "papel-alterado" });

        Assert.Equal("papel-alterado", record.Type);
        Assert.Equal(AuditRecord.NonConforming, record.Conformity);
        Assert.Equal(new[] { "tipo-desconhecido" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(MissingTypeIsStoredAsNullAndMarkedUnknown))]
    public void MissingTypeIsStoredAsNullAndMarkedUnknown()
    {
        var record = CreateRecord(CreateAct() with { Type = null });

        Assert.Null(record.Type);
        Assert.Equal(new[] { "tipo-desconhecido" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(MissingAuthorIsRecordedWithoutReferenceFields))]
    public void MissingAuthorIsRecordedWithoutReferenceFields()
    {
        var record = CreateRecord(CreateAct() with { Author = null });

        Assert.Null(record.AuthorType);
        Assert.Null(record.AuthorId);
        Assert.Equal(new[] { "autor-ausente" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(InvalidAuthorReferenceIsPreservedAndMarkedMissing))]
    public void InvalidAuthorReferenceIsPreservedAndMarkedMissing()
    {
        var record = CreateRecord(CreateAct() with
        {
            Author = new AdministrativeActReference("Conta Interna", Guid.Empty),
        });

        Assert.Equal("Conta Interna", record.AuthorType);
        Assert.Equal(Guid.Empty, record.AuthorId);
        Assert.Equal(new[] { "autor-ausente" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(MissingTargetIsRecordedWithoutReferenceFields))]
    public void MissingTargetIsRecordedWithoutReferenceFields()
    {
        var record = CreateRecord(CreateAct() with { Target = null });

        Assert.Null(record.TargetType);
        Assert.Null(record.TargetId);
        Assert.Equal(new[] { "alvo-ausente" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(MissingRequiredReasonIsRetainedAsNull))]
    public void MissingRequiredReasonIsRetainedAsNull()
    {
        var record = CreateRecord(CreateAct() with { Reason = null });

        Assert.Null(record.Reason);
        Assert.Equal(new[] { "motivo-ausente" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(MissingPracticedOnIsStoredAsNull))]
    public void MissingPracticedOnIsStoredAsNull()
    {
        var record = CreateRecord(CreateAct() with { PracticedOn = null });

        Assert.Null(record.PracticedOn);
        Assert.Equal(new[] { "momento-ausente" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(InvalidComplementIsOmittedFromRecord))]
    public void InvalidComplementIsOmittedFromRecord()
    {
        var record = CreateRecord(CreateAct() with
        {
            Complement = new Dictionary<string, string>
            {
                ["papel"] = new('x', AuditRecord.ComplementValueMaxLength + 1),
            },
        });

        Assert.Null(record.Complement);
        Assert.Equal(new[] { "complemento-invalido" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(InvalidJsonOptionalFieldsBecomeConformityReasons))]
    public void InvalidJsonOptionalFieldsBecomeConformityReasons()
    {
        const string json = """
            {
              "fatoId": "6a030001-0000-7000-8000-000000000001",
              "origem": "identidade",
              "tipo": "papel-concedido",
              "tenantId": "6a030001-0000-7000-8000-000000000002",
              "praticadoEm": "not-a-date",
              "autor": { "tipo": "conta-interna", "id": "6a030001-0000-7000-8000-000000000003" },
              "alvo": { "tipo": "conta-interna", "id": "6a030001-0000-7000-8000-000000000004" },
              "complemento": { "papel": 12 },
              "motivo": "Motivo unitário"
            }
            """;

        var message = JsonSerializer.Deserialize<AtoPraticado>(json)!;
        var record = CreateRecord(Map(message));

        Assert.Null(message.PraticadoEm);
        Assert.True(message.ComplementoInvalido);
        Assert.Null(record.PracticedOn);
        Assert.Null(record.Complement);
        Assert.Equal(new[] { "momento-ausente", "complemento-invalido" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(OverlongReasonIsStoredWholeAndMarked))]
    public void OverlongReasonIsStoredWholeAndMarked()
    {
        var reason = new string('x', AuditRecord.ReasonMaxLength + 1);
        var record = CreateRecord(CreateAct() with { Reason = reason });

        Assert.Equal(reason, record.Reason);
        Assert.Equal(new[] { "motivo-excede-limite" }, record.Reasons);
    }

    [Fact(DisplayName = nameof(CombinedDeficienciesProduceEveryApplicableReasonInOrder))]
    public void CombinedDeficienciesProduceEveryApplicableReasonInOrder()
    {
        var record = CreateRecord(CreateAct() with
        {
            Type = "papel-alterado",
            Author = null,
            Target = null,
            PracticedOn = null,
            ComplementIsInvalid = true,
            InvalidComplementFingerprint = "{\"papel\":12}",
        });

        Assert.Equal(
            new[]
            {
                "tipo-desconhecido",
                "autor-ausente",
                "alvo-ausente",
                "momento-ausente",
                "complemento-invalido",
            },
            record.Reasons);
    }

    [Fact(DisplayName = nameof(InvitationAcceptedWithoutReasonIsConforming))]
    public void InvitationAcceptedWithoutReasonIsConforming()
    {
        var record = CreateRecord(CreateAct() with
        {
            Type = "convite-interno-aceito",
            Reason = null,
            Complement = null,
        });

        Assert.Equal(AuditRecord.Conforming, record.Conformity);
        Assert.Empty(record.Reasons);
        Assert.Null(record.Reason);
    }

    private static AuditRecord CreateRecord(AdministrativeAct act) => AuditRecord.Create(act, ReceivedOn);

    private static AdministrativeAct CreateAct()
        => new(
            Guid.Parse("6a030001-0000-7000-8000-000000000001"),
            "identidade",
            "papel-concedido",
            Guid.Parse("6a030001-0000-7000-8000-000000000002"),
            new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
            new AdministrativeActReference("conta-interna", Guid.Parse("6a030001-0000-7000-8000-000000000003")),
            new AdministrativeActReference("conta-interna", Guid.Parse("6a030001-0000-7000-8000-000000000004")),
            new Dictionary<string, string> { ["papel"] = "professor" },
            "Motivo unitário");

    private static AdministrativeAct Map(AtoPraticado act)
        => new(
            act.FatoId,
            act.Origem ?? string.Empty,
            act.Tipo,
            act.TenantId,
            act.PraticadoEm,
            act.Autor is null ? null : new AdministrativeActReference(act.Autor.Tipo, act.Autor.Id),
            act.Alvo is null ? null : new AdministrativeActReference(act.Alvo.Tipo, act.Alvo.Id),
            act.Complemento,
            act.Motivo,
            act.ComplementoInvalido,
            act.ComplementoOriginalCanonico);
}
