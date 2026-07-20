namespace Kalm.Identity.Application.ManagementAuthentication;

public sealed record PasswordVerificationResult(bool Succeeded, bool RequiresRehash);
