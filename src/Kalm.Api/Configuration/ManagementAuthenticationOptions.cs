namespace Kalm.Api.Configuration;

public sealed class ManagementAuthenticationOptions
{
    public const string SectionName = "ManagementAuthentication";

    public int InactivityMinutes { get; init; } = 20;
    public int AbsoluteLifetimeHours { get; init; } = 8;
    public int ReauthenticationMinutes { get; init; } = 5;
    public int FailureThreshold { get; init; } = 5;
    public int FailureWindowMinutes { get; init; } = 15;
    public int LockoutMinutes { get; init; } = 15;
    public int LoginRequestsPerMinute { get; init; } = 10;
}
