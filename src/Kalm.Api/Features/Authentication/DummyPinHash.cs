using Kalm.Identity.Application.PinAuthentication;

namespace Kalm.Api.Features.Authentication;

public sealed class DummyPinHash
{
    public DummyPinHash(IPinHasher hasher) => EncodedHash = hasher.Hash("000000");
    public string EncodedHash { get; }
}
