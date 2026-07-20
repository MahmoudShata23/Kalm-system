namespace Kalm.Identity.Infrastructure.Security;

public sealed class SecurityFingerprintOptions
{
    public const string SectionName = "SecurityFingerprint";

    public int ActiveKeyVersion { get; set; }
    public string ActiveKeyBase64 { get; set; } = string.Empty;
}
