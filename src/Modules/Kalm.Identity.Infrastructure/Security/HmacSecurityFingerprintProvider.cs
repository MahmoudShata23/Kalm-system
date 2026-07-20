using System.Security.Cryptography;
using System.Text;
using Kalm.Identity.Application.ManagementAuthentication;
using Microsoft.Extensions.Options;

namespace Kalm.Identity.Infrastructure.Security;

public sealed class HmacSecurityFingerprintProvider : ISecurityFingerprintProvider
{
    private readonly byte[] _key;

    public HmacSecurityFingerprintProvider(IOptions<SecurityFingerprintOptions> options)
    {
        ActiveKeyVersion = options.Value.ActiveKeyVersion;
        if (ActiveKeyVersion <= 0 || string.IsNullOrWhiteSpace(options.Value.ActiveKeyBase64))
        {
            throw new OptionsValidationException(SecurityFingerprintOptions.SectionName, typeof(SecurityFingerprintOptions), ["An active versioned fingerprint key is required."]);
        }

        try
        {
            _key = Convert.FromBase64String(options.Value.ActiveKeyBase64);
        }
        catch (FormatException)
        {
            throw new OptionsValidationException(SecurityFingerprintOptions.SectionName, typeof(SecurityFingerprintOptions), ["Fingerprint key must be valid Base64."]);
        }

        if (_key.Length < 32)
        {
            throw new OptionsValidationException(SecurityFingerprintOptions.SectionName, typeof(SecurityFingerprintOptions), ["Fingerprint key must contain at least 32 random bytes."]);
        }
    }

    public int ActiveKeyVersion { get; }

    public string Fingerprint(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        byte[] digest = HMACSHA256.HashData(_key, bytes);
        try
        {
            return Convert.ToHexString(digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(digest);
        }
    }
}
