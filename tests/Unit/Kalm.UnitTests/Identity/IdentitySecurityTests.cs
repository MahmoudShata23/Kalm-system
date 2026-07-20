using System.Security.Cryptography;
using System.Text;
using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Kalm.UnitTests.Identity;

public sealed class IdentitySecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly ITestOutputHelper _output;

    public IdentitySecurityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PasswordPolicy_CountsUnicodeScalarsWithoutTrimmingOrCompositionRules()
    {
        PasswordPolicy.Validate("  pass phrase ☕️  ");
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(new string('x', 14)));
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(new string('x', 129)));
    }

    [Fact]
    public void PasswordHasher_UsesVersionedSha512FormatAndFixedVerification()
    {
        var hasher = CreateHasher(PasswordHashingOptions.MinimumIterations);
        string encoded = hasher.Hash("a secure phrase ☕ 2026");

        Assert.StartsWith("$kalm$pbkdf2-sha512$v=1$i=220000$s=", encoded, StringComparison.Ordinal);
        Assert.True(hasher.Verify("a secure phrase ☕ 2026", encoded).Succeeded);
        Assert.False(hasher.Verify("a different phrase 2026", encoded).Succeeded);
    }

    [Fact]
    public void PasswordHasher_RequestsOnlyUpwardWorkFactorRehash()
    {
        string encoded = CreateHasher(PasswordHashingOptions.MinimumIterations).Hash("a secure phrase ☕ 2026");

        PasswordVerificationResult result = CreateHasher(PasswordHashingOptions.MinimumIterations + 1).Verify("a secure phrase ☕ 2026", encoded);

        Assert.True(result.Succeeded);
        Assert.True(result.RequiresRehash);
    }

    [Fact]
    public void Fingerprint_IsKeyedVersionedAndNotPlainSha256()
    {
        byte[] firstKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        byte[] secondKey = Enumerable.Range(2, 32).Select(value => (byte)value).ToArray();
        const string identifier = "MANAGER@EXAMPLE.COM";
        var first = CreateFingerprint(firstKey, 7);
        var same = CreateFingerprint(firstKey, 7);
        var different = CreateFingerprint(secondKey, 8);
        string plainSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier)));

        Assert.Equal(first.Fingerprint(identifier), same.Fingerprint(identifier));
        Assert.NotEqual(first.Fingerprint(identifier), different.Fingerprint(identifier));
        Assert.NotEqual(plainSha256, first.Fingerprint(identifier));
        Assert.Equal(7, first.ActiveKeyVersion);
    }

    [Fact]
    public void NewUserAndCredential_RequireSetupBeforeActivation()
    {
        User user = User.Create(Guid.NewGuid(), Guid.NewGuid(), new Username("manager"), null, new DisplayName("Cafe Manager"), "en", Now);
        PasswordCredential credential = PasswordCredential.Create(Guid.NewGuid(), user.Id, Now);

        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.Equal(PasswordCredentialStatus.PendingSetup, credential.Status);
        Assert.Throws<InvalidOperationException>(() => user.Activate(credential, Now));

        credential.CompleteSetup("$kalm$pbkdf2-sha512$v=1$i=220000$s=value$h=value", Now);
        user.Activate(credential, Now);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Credential_LocksOnFifthFailureAndLockedAttemptsDoNotExtendIt()
    {
        PasswordCredential credential = PasswordCredential.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        credential.CompleteSetup("$kalm$pbkdf2-sha512$v=1$i=220000$s=value$h=value", Now);
        for (int attempt = 1; attempt < 5; attempt++)
        {
            Assert.False(credential.RegisterFailure(Now.AddMinutes(attempt), 5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)));
        }

        Assert.True(credential.RegisterFailure(Now.AddMinutes(5), 5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)));
        DateTimeOffset? lockedUntil = credential.LockedUntilUtc;
        Assert.False(credential.RegisterFailure(Now.AddMinutes(6), 5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)));
        Assert.Equal(lockedUntil, credential.LockedUntilUtc);
    }

    [Fact]
    public void SessionActivity_NeverExtendsBeyondAbsoluteExpiry()
    {
        UserSession session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), Now, TimeSpan.FromMinutes(20), TimeSpan.FromHours(8));
        for (int elapsedMinutes = 15; elapsedMinutes <= 7 * 60 + 45; elapsedMinutes += 15)
        {
            session.RecordActivity(Now.AddMinutes(elapsedMinutes), TimeSpan.FromMinutes(20));
        }

        session.RecordActivity(Now.AddHours(7).AddMinutes(50), TimeSpan.FromMinutes(20));
        Assert.Equal(session.AbsoluteExpiresAtUtc, session.InactivityExpiresAtUtc);
    }

    [Fact]
    public void PasswordWorkFactorCalibration_MeasuresRepresentativeLengthsAgainstApprovedTargets()
    {
        PasswordCalibrationResult result = PasswordWorkFactorCalibrator.Calibrate(
            PasswordHashingOptions.MinimumIterations,
            targetMedianMilliseconds: 250,
            maximumP95Milliseconds: 500);

        _output.WriteLine(
            "PBKDF2-HMAC-SHA512 calibration: {0} iterations, median {1:F2} ms, p95 {2:F2} ms.",
            result.Iterations,
            result.MedianMilliseconds,
            result.P95Milliseconds);
        Assert.True(result.Iterations >= PasswordHashingOptions.MinimumIterations);
        Assert.True(result.MedianMilliseconds >= 250 || result.P95Milliseconds >= 500);
    }

    private static Pbkdf2PasswordHasher CreateHasher(int iterations)
        => new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    private static HmacSecurityFingerprintProvider CreateFingerprint(byte[] key, int version)
        => new(Options.Create(new SecurityFingerprintOptions { ActiveKeyVersion = version, ActiveKeyBase64 = Convert.ToBase64String(key) }));
}
