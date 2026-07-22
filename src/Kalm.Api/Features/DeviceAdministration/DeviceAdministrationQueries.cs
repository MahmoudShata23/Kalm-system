using Kalm.Organization;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.DeviceAdministration;

public sealed class DeviceAdministrationQueries(OrganizationDbContext organization)
{
    public async Task<DeviceListResponse> ListAsync(Guid organizationId, string status, Guid? branchId, string? search, int page, int pageSize, CancellationToken token)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        IQueryable<Device> query = organization.Devices.AsNoTracking().Where(device => device.OrganizationId == organizationId);
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryStatus(status, out DeviceStatus parsed)) throw new ArgumentException("Invalid device status.", nameof(status));
            query = query.Where(device => device.Status == parsed);
        }
        if (branchId is not null) query = query.Where(device => device.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(search)) { string pattern = $"%{search.Trim()}%"; query = query.Where(device => EF.Functions.ILike(device.Name, pattern)); }
        int count = await query.CountAsync(token);
        DeviceSummaryResponse[] items = await (
            from device in query
            join branch in organization.Branches.AsNoTracking() on new { device.BranchId, device.OrganizationId } equals new { BranchId = branch.Id, branch.OrganizationId }
            orderby device.Name, device.Id
            select new DeviceSummaryResponse(device.Id, device.BranchId, branch.Name, device.Name, Type(device.Type), device.Platform, Status(device.Status), device.PairedAtUtc, device.LastSeenAtUtc, device.UpdatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(token);
        return new(items, page, pageSize, count);
    }

    public async Task<DeviceVersionedDetail?> GetAsync(Guid organizationId, Guid deviceId, CancellationToken token)
        => await (
            from device in organization.Devices.AsNoTracking()
            join branch in organization.Branches.AsNoTracking() on new { device.BranchId, device.OrganizationId } equals new { BranchId = branch.Id, branch.OrganizationId }
            where device.Id == deviceId && device.OrganizationId == organizationId
            select new DeviceVersionedDetail(new DeviceDetailResponse(device.Id, device.BranchId, branch.Name, device.Name, Type(device.Type), device.Platform, Status(device.Status), device.PairedAtUtc, device.LastSeenAtUtc, device.CreatedAtUtc, device.UpdatedAtUtc), device.Version))
            .SingleOrDefaultAsync(token);

    public async Task<DeviceOptionsResponse> OptionsAsync(Guid organizationId, CancellationToken token)
    {
        DeviceBranchOptionResponse[] branches = await organization.Branches.AsNoTracking().Where(branch => branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active).OrderBy(branch => branch.Code).Select(branch => new DeviceBranchOptionResponse(branch.Id, branch.Name, branch.Code)).ToArrayAsync(token);
        return new(branches, [new("posTerminal", "POS terminal", "نقطة بيع"), new("kdsScreen", "KDS screen", "شاشة تحضير"), new("adminTerminal", "Admin terminal", "جهاز إدارة"), new("printAgent", "Print agent", "وكيل طباعة")]);
    }

    internal static bool TryType(string value, out DeviceType type) => Enum.TryParse(value, true, out type) && Enum.IsDefined(type);
    internal static string Type(DeviceType type) => char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];
    internal static string Status(DeviceStatus status) => char.ToLowerInvariant(status.ToString()[0]) + status.ToString()[1..];
    private static bool TryStatus(string value, out DeviceStatus status) => Enum.TryParse(value, true, out status) && Enum.IsDefined(status);
}

public sealed record DeviceVersionedDetail(DeviceDetailResponse Device, long Version);
