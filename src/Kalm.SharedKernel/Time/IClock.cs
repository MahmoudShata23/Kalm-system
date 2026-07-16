namespace Kalm.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
