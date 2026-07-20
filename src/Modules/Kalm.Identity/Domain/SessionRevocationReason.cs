namespace Kalm.Identity.Domain;

public enum SessionRevocationReason
{
    Logout,
    UserSuspended,
    CredentialChanged,
    IdleExpired,
    AbsoluteExpired
}
