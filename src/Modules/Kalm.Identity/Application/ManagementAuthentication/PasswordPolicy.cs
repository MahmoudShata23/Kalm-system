namespace Kalm.Identity.Application.ManagementAuthentication;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;

    public static void Validate(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        int scalarCount = password.EnumerateRunes().Count();
        if (scalarCount is < MinimumLength or > MaximumLength)
        {
            throw new ArgumentException($"Password must contain between {MinimumLength} and {MaximumLength} Unicode scalar values.", nameof(password));
        }
    }
}
