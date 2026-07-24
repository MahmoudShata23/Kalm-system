using System.Text.Json;
using Kalm.Audit.Domain;

namespace Kalm.Api.Features.AuditViewer;

internal static class AuditViewerMetadataPresenter
{
    private static readonly HashSet<string> BranchFields = new(StringComparer.Ordinal)
    {
        "name", "code", "localeCode", "timeZoneId", "businessDayRollover"
    };
    private static readonly HashSet<string> CategoryFields = new(StringComparer.Ordinal)
    {
        "arabicName", "englishName", "displayOrder", "posColorToken", "iconCode"
    };
    private static readonly HashSet<string> ProductFields = new(StringComparer.Ordinal)
    {
        "categoryId", "arabicName", "englishName", "arabicDescription", "englishDescription",
        "sku", "productType", "displayOrder"
    };
    private static readonly HashSet<string> VariantFields = new(StringComparer.Ordinal)
    {
        "arabicName", "englishName", "code", "barcode", "sizeCode",
        "temperatureCode", "servingFormatCode", "displayOrder"
    };

    public static AuditSafeMetadataResponse? Present(AuditEntry entry)
    {
        using JsonDocument? before = Parse(entry.BeforeJson);
        using JsonDocument? after = Parse(entry.AfterJson);
        var changedFields = new SortedSet<string>(StringComparer.Ordinal);
        string? previousStatus = null;
        string? newStatus = null;
        int? registeredDevices = null;
        int? activeDevices = null;
        int? activeCredentials = null;
        int? activeSessions = null;
        int? activeAssignments = null;
        int? activeRoleAssignments = null;
        int? sessionsRevoked = null;
        int? affectedCount = null;
        Guid? relatedUserId = null;
        Guid? relatedBranchId = null;
        Guid? relatedDeviceId = null;

        switch (entry.Action)
        {
            case AuditAction.BranchUpdated:
                AddStringArray(after, "changedFields", BranchFields, changedFields);
                break;
            case AuditAction.CategoryUpdated:
                AddStringArray(after, "changedFields", CategoryFields, changedFields);
                break;
            case AuditAction.ProductUpdated:
                AddStringArray(after, "changedFields", ProductFields, changedFields);
                break;
            case AuditAction.VariantUpdated:
                AddStringArray(after, "changedFields", VariantFields, changedFields);
                break;
            case AuditAction.UserProfileChanged:
                AddChangedProperties(before, after, ["username", "email", "displayName", "preferredLanguage"], changedFields);
                break;
            case AuditAction.RoleRenamed:
                changedFields.Add("name");
                break;
            case AuditAction.RolePermissionSetChanged:
                changedFields.Add("permissionSet");
                break;
            case AuditAction.UserRoleAssigned:
            case AuditAction.UserRoleRevoked:
                changedFields.Add("roles");
                break;
            case AuditAction.UserBranchAccessChanged:
                changedFields.Add("branchAccess");
                break;
            case AuditAction.DeviceUpdated:
                AddChangedProperties(before, after, ["name", "type"], changedFields);
                if (Boolean(after, "securityChanged") == true)
                {
                    changedFields.Add("security");
                }
                break;
        }

        if (entry.Action is AuditAction.BranchActivated
            or AuditAction.BranchDeactivated
            or AuditAction.BranchStatusChanged
            or AuditAction.UserActivated
            or AuditAction.UserSuspended
            or AuditAction.RoleArchived
            or AuditAction.DeviceRevoked
            or AuditAction.CategoryActivated
            or AuditAction.CategoryArchived
            or AuditAction.ProductActivated
            or AuditAction.ProductArchived
            or AuditAction.VariantActivated
            or AuditAction.VariantArchived)
        {
            previousStatus = String(before, "status");
            newStatus = String(after, "status");
        }

        if (entry.Action == AuditAction.BranchAdministrationRejected
            && Object(after, "dependencyCounts") is JsonElement dependencies)
        {
            registeredDevices = Integer(dependencies, "registeredDeviceCount");
            activeDevices = Integer(dependencies, "activeDeviceCount");
            activeCredentials = Integer(dependencies, "activeCredentialCount");
            activeSessions = Integer(dependencies, "activeSessionCount");
            activeAssignments = Integer(dependencies, "activeUserAssignmentCount");
        }

        if (entry.Action == AuditAction.RoleAdministrationRejected)
        {
            activeRoleAssignments = Integer(after, "activeAssignmentCount");
        }

        if (entry.Action is AuditAction.UserSuspended
            or AuditAction.UserPasswordSet
            or AuditAction.UserPasswordReset
            or AuditAction.UserPinSet
            or AuditAction.UserPinReset)
        {
            sessionsRevoked = Integer(after, "sessionsRevoked");
        }

        if (entry.Action is AuditAction.DevicePaired or AuditAction.DeviceCredentialRotated)
        {
            relatedDeviceId = Identifier(after, "deviceId");
            relatedBranchId = Identifier(after, "branchId");
        }

        if (entry.Action is AuditAction.CategoriesReordered or AuditAction.VariantsReordered)
        {
            affectedCount = Integer(after, "affectedCount");
        }

        if (entry.Action == AuditAction.CatalogMutationRejected)
        {
            affectedCount = Integer(after, "activeProductCount");
        }

        if (entry.Action is AuditAction.UserRoleAssigned
            or AuditAction.UserRoleRevoked
            or AuditAction.UserBranchAccessChanged)
        {
            relatedUserId = entry.EntityId;
        }

        bool empty = changedFields.Count == 0
            && previousStatus is null && newStatus is null
            && registeredDevices is null && activeDevices is null && activeCredentials is null
            && activeSessions is null && activeAssignments is null && activeRoleAssignments is null
            && sessionsRevoked is null && relatedUserId is null && relatedBranchId is null && relatedDeviceId is null;
        empty = empty && affectedCount is null;
        return empty ? null : new AuditSafeMetadataResponse(
            changedFields.ToArray(), previousStatus, newStatus,
            registeredDevices, activeDevices, activeCredentials, activeSessions, activeAssignments,
            activeRoleAssignments, sessionsRevoked, affectedCount, relatedUserId, relatedBranchId, relatedDeviceId);
    }

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
    }

    private static JsonElement? Root(JsonDocument? document)
        => document?.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement : null;

    private static JsonElement? Object(JsonDocument? document, string name)
        => Root(document) is JsonElement root && Property(root, name) is JsonElement value && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string? String(JsonDocument? document, string name)
        => Root(document) is JsonElement root && Property(root, name) is JsonElement value && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? Boolean(JsonDocument? document, string name)
        => Root(document) is JsonElement root && Property(root, name) is JsonElement value && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static int? Integer(JsonDocument? document, string name)
        => Root(document) is JsonElement root ? Integer(root, name) : null;

    private static int? Integer(JsonElement root, string name)
        => Property(root, name) is JsonElement value && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsed) && parsed >= 0 ? parsed : null;

    private static Guid? Identifier(JsonDocument? document, string name)
        => Root(document) is JsonElement root
            && Property(root, name) is JsonElement value
            && value.ValueKind == JsonValueKind.String
            && Guid.TryParse(value.GetString(), out Guid parsed)
            && parsed != Guid.Empty ? parsed : null;

    private static JsonElement? Property(JsonElement element, string name)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }
        return null;
    }

    private static void AddStringArray(JsonDocument? document, string name, HashSet<string> allowlist, SortedSet<string> target)
    {
        if (Root(document) is not JsonElement root || Property(root, name) is not JsonElement values || values.ValueKind != JsonValueKind.Array) return;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && value.GetString() is string field && allowlist.Contains(field)) target.Add(field);
        }
    }

    private static void AddChangedProperties(JsonDocument? before, JsonDocument? after, IEnumerable<string> names, SortedSet<string> target)
    {
        JsonElement? beforeRoot = Root(before);
        JsonElement? afterRoot = Root(after);
        foreach (string name in names)
        {
            string oldValue = beforeRoot is JsonElement oldRoot && Property(oldRoot, name) is JsonElement oldProperty ? oldProperty.GetRawText() : string.Empty;
            string newValue = afterRoot is JsonElement newRoot && Property(newRoot, name) is JsonElement newProperty ? newProperty.GetRawText() : string.Empty;
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal)) target.Add(name);
        }
    }
}
