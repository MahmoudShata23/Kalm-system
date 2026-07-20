using System.Net.Mail;
using System.Text;

namespace Kalm.Identity.Domain.ValueObjects;

public sealed record EmailAddress
{
    public EmailAddress(string value)
    {
        Value = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC);
        if (Value.Length > 254 || !MailAddress.TryCreate(Value, out MailAddress? parsed) || !string.Equals(parsed.Address, Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email address is invalid.", nameof(value));
        }

        NormalizedValue = Value.ToUpperInvariant();
    }

    public string Value { get; }

    public string NormalizedValue { get; }
}
