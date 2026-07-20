namespace Kalm.Identity.Infrastructure.Security;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";
    public const int MinimumIterations = 220_000;

    public int Iterations { get; set; } = MinimumIterations;
    public int TargetMedianMilliseconds { get; init; } = 250;
    public int MaximumP95Milliseconds { get; init; } = 500;
}
