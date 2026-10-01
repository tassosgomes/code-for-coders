using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class OfferAuditReferenceEnricher(IStaffSessionIdentityClient identity, IOfferReferenceClient commerce)
{
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(Guid sessionId,
        IEnumerable<AuditRecordIdentityReferenceV1?> references, CancellationToken cancellationToken)
    {
        var ids = references.Where(reference => reference is { Type: "oferta" } && reference.Id != Guid.Empty)
            .Select(reference => reference!.Id).Distinct().ToArray();
        var labels = new Dictionary<Guid, string>();
        if (ids.Length == 0) return labels;
        var access = await identity.ValidateSessionAsync(sessionId, "commerce", cancellationToken);
        if (access.StatusCode != 200 || string.IsNullOrWhiteSpace(access.Session?.AccessToken)
            || !access.Session.Roles.Contains("administrador", StringComparer.Ordinal)) return labels;
        foreach (var batch in ids.Chunk(50))
        {
            var resolved = await commerce.ResolveAsync(access.Session.AccessToken, batch, cancellationToken);
            foreach (var reference in resolved) labels[reference.Key] = reference.Value;
        }
        return labels;
    }

    public static AuditRecordIdentityReferenceV1? AddLabel(AuditRecordIdentityReferenceV1? reference, IReadOnlyDictionary<Guid, string> labels)
        => reference is { Type: "oferta" } ? reference with { Label = labels.GetValueOrDefault(reference.Id) } : reference;
}
