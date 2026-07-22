namespace Kalm.Api.Features.Authentication;

public static class ManagementAuthenticationConstants
{
    public const string Scheme = "Kalm.Management.v1";
    public const string CookieName = "__Host-Kalm.Management";
    public const string SessionIdClaim = "kalm:session_id";
    public const string SchemeVersionClaim = "kalm:scheme_version";
    public const string SchemeVersion = "1";
    public const string LoginRateLimitPolicy = "management-login";
    public const string PinLoginRateLimitPolicy = "pin-login";
    public const string SessionItemKey = "Kalm.Management.Session";
}
