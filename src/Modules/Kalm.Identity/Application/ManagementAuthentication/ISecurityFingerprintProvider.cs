namespace Kalm.Identity.Application.ManagementAuthentication;

public interface ISecurityFingerprintProvider
{
    int ActiveKeyVersion { get; }

    string Fingerprint(string value);
}
