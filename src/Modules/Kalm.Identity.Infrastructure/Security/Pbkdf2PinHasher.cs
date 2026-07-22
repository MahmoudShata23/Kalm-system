using System.Globalization;
using System.Security.Cryptography;
using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity.Application.PinAuthentication;
using Microsoft.Extensions.Options;

namespace Kalm.Identity.Infrastructure.Security;

public sealed class Pbkdf2PinHasher(IOptions<PasswordHashingOptions> options) : IPinHasher
{
    private const int SaltLength = 32;
    private const int KeyLength = 64;
    private const string Purpose = "Kalm.Pin.v1:";
    private readonly int _iterations = options.Value.Iterations;

    public string Hash(string pin)
    {
        Validate(pin);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Purpose + pin, salt, _iterations, HashAlgorithmName.SHA512, KeyLength);
        try { return string.Create(CultureInfo.InvariantCulture, $"$kalm$pin-pbkdf2-sha512$v=1$i={_iterations}$s={Encode(salt)}$h={Encode(key)}"); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public bool Verify(string pin, string encodedHash)
    {
        if (!IsValid(pin) || !TryParse(encodedHash, out int iterations, out byte[] salt, out byte[] expected)) return false;
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Purpose + pin, salt, iterations, HashAlgorithmName.SHA512, KeyLength);
        try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
        finally { CryptographicOperations.ZeroMemory(actual); CryptographicOperations.ZeroMemory(expected); CryptographicOperations.ZeroMemory(salt); }
    }

    public static void Validate(string pin) { if (!IsValid(pin)) throw new ArgumentException("PIN must contain exactly six numeric digits.", nameof(pin)); }
    private static bool IsValid(string? pin) => pin is { Length: 6 } && pin.All(character => character is >= '0' and <= '9');
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) { string b = value.Replace('-', '+').Replace('_', '/'); return Convert.FromBase64String(b.PadRight(b.Length + ((4 - b.Length % 4) % 4), '=')); }
    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0; salt = []; hash = [];
        string[] p = (value ?? string.Empty).Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length != 6 || p[0] != "kalm" || p[1] != "pin-pbkdf2-sha512" || p[2] != "v=1" || !p[3].StartsWith("i=", StringComparison.Ordinal) || !int.TryParse(p[3].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations < PasswordHashingOptions.MinimumIterations || !p[4].StartsWith("s=", StringComparison.Ordinal) || !p[5].StartsWith("h=", StringComparison.Ordinal)) return false;
        try { salt = Decode(p[4][2..]); hash = Decode(p[5][2..]); return salt.Length == SaltLength && hash.Length == KeyLength; }
        catch (FormatException) { salt = []; hash = []; return false; }
    }
}
