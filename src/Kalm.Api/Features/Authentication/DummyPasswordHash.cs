using Kalm.Identity.Application.ManagementAuthentication;

namespace Kalm.Api.Features.Authentication;

public sealed class DummyPasswordHash
{
    public DummyPasswordHash(IPasswordHasher hasher)
    {
        EncodedHash = hasher.Hash("dummy verification phrase only");
    }

    public string EncodedHash { get; }
}
