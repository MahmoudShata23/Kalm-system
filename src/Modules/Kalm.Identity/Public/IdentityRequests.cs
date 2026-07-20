namespace Kalm.Identity;

public sealed class LoginRequest
{
    public string Identifier { get; init; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
