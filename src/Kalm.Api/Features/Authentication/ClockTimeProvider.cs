using Kalm.SharedKernel.Time;

namespace Kalm.Api.Features.Authentication;

public sealed class ClockTimeProvider(IClock clock) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => clock.UtcNow;
}
