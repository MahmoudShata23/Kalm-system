namespace Kalm.Identity.Application.PinAuthentication;

public interface IPinHasher
{
    string Hash(string pin);
    bool Verify(string pin, string encodedHash);
}
