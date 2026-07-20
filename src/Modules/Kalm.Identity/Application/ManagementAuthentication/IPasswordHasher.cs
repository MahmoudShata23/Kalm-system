namespace Kalm.Identity.Application.ManagementAuthentication;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationResult Verify(string password, string encodedHash);
}
