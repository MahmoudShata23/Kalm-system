using System.Security.Cryptography;
using System.Text;

namespace Kalm.Api.Features.DeviceAdministration;

internal static class DeviceSecurity
{
    public static string GenerateChallenge() => Encode(RandomNumberGenerator.GetBytes(20));
    public static string GenerateCredential() => Encode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
