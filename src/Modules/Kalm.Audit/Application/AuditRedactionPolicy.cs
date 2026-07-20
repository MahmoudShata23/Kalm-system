using System.Text.Json;

namespace Kalm.Audit.Application;

public static class AuditRedactionPolicy
{
    private static readonly HashSet<string> ProhibitedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pin", "hash", "salt", "pepper", "cookie", "token", "pairingCode", "csrf", "authorization", "requestBody"
    };

    public static string? CreateJson(IReadOnlyDictionary<string, string?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var safe = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string key, string? value) in values)
        {
            if (ProhibitedKeys.Contains(key))
            {
                throw new ArgumentException($"Audit payload field '{key}' is not permitted.", nameof(values));
            }

            safe.Add(key, value);
        }

        return JsonSerializer.Serialize(safe);
    }
}
