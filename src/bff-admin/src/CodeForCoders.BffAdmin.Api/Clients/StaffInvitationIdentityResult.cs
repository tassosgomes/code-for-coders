using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record StaffInvitationIdentityResult(
    int StatusCode,
    string? Code,
    StaffInvitationCreatedV1? Created,
    StaffInvitationPageV1? Page,
    StaffInvitationPreviewV1? Preview = null,
    StaffSessionCreatedV1? Session = null);
