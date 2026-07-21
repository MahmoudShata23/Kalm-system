using Kalm.Api.Features.Authorization;

namespace Kalm.Api.Features.Authentication;

public sealed record ManagementSessionSnapshot(
    Guid SessionId,
    Guid UserId,
    Guid OrganizationId,
    string Username,
    string DisplayName,
    string PreferredLanguage,
    DateTimeOffset InactivityExpiresAtUtc,
    DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset LastReauthenticatedAtUtc,
    EffectiveAuthorizationSnapshot Authorization);
