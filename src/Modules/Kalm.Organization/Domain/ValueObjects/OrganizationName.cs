namespace Kalm.Organization.Domain.ValueObjects;

public sealed record OrganizationName
{
    public OrganizationName(string value, int maximumLength)
    {
        Value = Normalize(value, maximumLength, "name");
    }

    public string Value { get; }

    public static string Normalize(string value, int maximumLength, string fieldName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 2 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{fieldName} must contain between 2 and {maximumLength} characters.", fieldName);
        }

        return normalized;
    }
}
