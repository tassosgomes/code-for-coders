using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CourseAuditReferenceEnricher(IStaffSessionIdentityClient identity, ICourseAuthoringClient learning)
{
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(Guid sessionId,
        IEnumerable<AuditRecordIdentityReferenceV1?> references, CancellationToken cancellationToken)
    {
        var ids = references.Where(reference => reference is { Type: "curso" } && reference.Id != Guid.Empty)
            .Select(reference => reference!.Id).Distinct().ToArray();
        var labels = new Dictionary<Guid, string>();
        if (ids.Length == 0) return labels;
        var access = await identity.ValidateSessionAsync(sessionId, "learning", cancellationToken);
        if (access.StatusCode != 200 || access.Session?.AccessToken is null
            || !access.Session.Roles.Contains("administrador", StringComparer.Ordinal)) return labels;
        foreach (var batch in ids.Chunk(50))
        {
            var result = await learning.SendAsync(new("internal/v1/course-references/resolve", access.Session.AccessToken,
                string.Empty, string.Empty, new { CourseIds = batch }, "POST"), cancellationToken);
            if (result.Status != 200 || result.References is null) continue;
            foreach (var reference in result.References.Data)
                if (batch.Contains(reference.CourseId) && !string.IsNullOrWhiteSpace(reference.Title)) labels[reference.CourseId] = reference.Title;
        }
        return labels;
    }

    public static AuditRecordIdentityReferenceV1? AddLabel(AuditRecordIdentityReferenceV1? reference, IReadOnlyDictionary<Guid, string> labels)
        => reference is { Type: "curso" } && labels.TryGetValue(reference.Id, out var title) ? reference with { Label = title } : reference;
}
