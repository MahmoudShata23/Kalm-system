namespace Kalm.Api.Features.Authentication;

public sealed record ManagementSessionSnapshot(
    Guid SessionId,
    Guid UserId,
    string Username,
    string DisplayName,
    string PreferredLanguage,
    DateTimeOffset InactivityExpiresAtUtc,
    DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset LastReauthenticatedAtUtc);
