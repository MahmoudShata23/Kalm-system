namespace Kalm.Identity;

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? Username,
    string? DisplayName,
    string? PreferredLanguage,
    DateTimeOffset? InactivityExpiresAtUtc,
    DateTimeOffset? AbsoluteExpiresAtUtc,
    DateTimeOffset? ReauthenticationValidUntilUtc,
    IReadOnlyCollection<string> Permissions);

public sealed record CsrfTokenResponse(string RequestToken);
