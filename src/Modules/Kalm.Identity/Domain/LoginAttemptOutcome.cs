namespace Kalm.Identity.Domain;

public enum LoginAttemptOutcome
{
    Succeeded,
    InvalidCredentials,
    Locked,
    Ineligible
}
