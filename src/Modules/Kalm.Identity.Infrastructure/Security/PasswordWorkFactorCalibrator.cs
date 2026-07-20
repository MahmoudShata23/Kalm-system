using System.Diagnostics;
using System.Security.Cryptography;

namespace Kalm.Identity.Infrastructure.Security;

public static class PasswordWorkFactorCalibrator
{
    private static readonly string[] RepresentativePasswords =
    [
        new('a', 15),
        new('b', 64),
        new('c', 128)
    ];

    public static PasswordCalibrationResult Calibrate(int startingIterations, int targetMedianMilliseconds, int maximumP95Milliseconds)
    {
        int iterations = Math.Max(startingIterations, PasswordHashingOptions.MinimumIterations);
        while (true)
        {
            PasswordCalibrationResult result = Measure(iterations);
            if (result.MedianMilliseconds >= targetMedianMilliseconds || result.P95Milliseconds >= maximumP95Milliseconds)
            {
                return result;
            }

            iterations = checked((int)Math.Ceiling(iterations * Math.Max(1.1, targetMedianMilliseconds / Math.Max(1d, result.MedianMilliseconds))));
        }
    }

    private static PasswordCalibrationResult Measure(int iterations)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        foreach (string password in RepresentativePasswords)
        {
            _ = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, 64);
        }

        var timings = new List<double>(RepresentativePasswords.Length * 5);
        foreach (string password in RepresentativePasswords)
        {
            for (int sample = 0; sample < 5; sample++)
            {
                long started = Stopwatch.GetTimestamp();
                byte[] result = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, 64);
                timings.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                CryptographicOperations.ZeroMemory(result);
            }
        }

        timings.Sort();
        double median = timings[timings.Count / 2];
        double p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
        CryptographicOperations.ZeroMemory(salt);
        return new PasswordCalibrationResult(iterations, median, p95);
    }
}

public sealed record PasswordCalibrationResult(int Iterations, double MedianMilliseconds, double P95Milliseconds);
