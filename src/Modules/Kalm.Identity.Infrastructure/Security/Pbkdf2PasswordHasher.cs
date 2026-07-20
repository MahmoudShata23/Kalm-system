using System.Globalization;
using System.Security.Cryptography;
using Kalm.Identity.Application.ManagementAuthentication;
using Microsoft.Extensions.Options;

namespace Kalm.Identity.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltLength = 32;
    private const int DerivedKeyLength = 64;
    private readonly int _iterations;

    public Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options)
    {
        _iterations = options.Value.Iterations;
        if (_iterations < PasswordHashingOptions.MinimumIterations)
        {
            throw new OptionsValidationException(PasswordHashingOptions.SectionName, typeof(PasswordHashingOptions), ["Configured password work factor is below 220000 iterations."]);
        }
    }

    public string Hash(string password)
    {
        PasswordPolicy.Validate(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA512, DerivedKeyLength);
        try
        {
            return string.Create(CultureInfo.InvariantCulture, $"$kalm$pbkdf2-sha512$v=1$i={_iterations}$s={Base64UrlEncode(salt)}$h={Base64UrlEncode(derivedKey)}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public PasswordVerificationResult Verify(string password, string encodedHash)
    {
        PasswordPolicy.Validate(password);
        if (!TryParse(encodedHash, out int iterations, out byte[]? salt, out byte[]? expected))
        {
            return new PasswordVerificationResult(false, false);
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, DerivedKeyLength);
        try
        {
            bool succeeded = CryptographicOperations.FixedTimeEquals(actual, expected);
            return new PasswordVerificationResult(succeeded, succeeded && iterations < _iterations);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];
        string[] parts = (value ?? string.Empty).Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6 || parts[0] != "kalm" || parts[1] != "pbkdf2-sha512" || parts[2] != "v=1" || !parts[3].StartsWith("i=", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[3].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations < PasswordHashingOptions.MinimumIterations)
        {
            return false;
        }

        if (!parts[4].StartsWith("s=", StringComparison.Ordinal) || !parts[5].StartsWith("h=", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            salt = Base64UrlDecode(parts[4].AsSpan(2));
            hash = Base64UrlDecode(parts[5].AsSpan(2));
            if (salt.Length == SaltLength && hash.Length == DerivedKeyLength)
            {
                return true;
            }

            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
            salt = [];
            hash = [];
            return false;
        }
        catch (FormatException)
        {
            salt = [];
            hash = [];
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(ReadOnlySpan<char> value)
    {
        string base64 = value.ToString().Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
