using System.Globalization;
using Kalm.Organization;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.BranchAdministration;

public sealed class BranchAdministrationQueries(OrganizationDbContext organization)
{
    public async Task<BranchListResponse> ListAsync(
        Guid organizationId,
        string status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (search?.Trim().Length > 120)
        {
            throw new ArgumentException("Branch search is too long.", nameof(search));
        }

        IQueryable<Branch> query = organization.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId);

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryStatus(status, out BranchStatus parsedStatus))
            {
                throw new ArgumentException("Invalid branch status.", nameof(status));
            }

            query = query.Where(branch => branch.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(branch => EF.Functions.ILike(branch.Name, pattern) || EF.Functions.ILike(branch.Code, pattern));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        Branch[] branches = await query
            .OrderBy(branch => branch.Code)
            .ThenBy(branch => branch.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        BranchSummaryResponse[] items = branches
            .Select(branch => new BranchSummaryResponse(
                branch.Id,
                branch.Name,
                branch.Code,
                branch.LocaleCode,
                branch.TimeZoneId,
                branch.BusinessDayRollover.ToString("HH:mm", CultureInfo.InvariantCulture),
                Status(branch.Status),
                branch.UpdatedAtUtc))
            .ToArray();

        return new BranchListResponse(items, page, pageSize, totalCount);
    }

    public async Task<BranchVersionedDetail?> GetAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        Branch? branch = await organization.Branches
            .AsNoTracking()
            .Where(branch => branch.Id == branchId && branch.OrganizationId == organizationId)
            .SingleOrDefaultAsync(cancellationToken);
        return branch is null
            ? null
            : new BranchVersionedDetail(
                new BranchDetailResponse(
                    branch.Id,
                    branch.Name,
                    branch.Code,
                    branch.LocaleCode,
                    branch.TimeZoneId,
                    branch.BusinessDayRollover.ToString("HH:mm", CultureInfo.InvariantCulture),
                    Status(branch.Status),
                    branch.CreatedAtUtc,
                    branch.UpdatedAtUtc),
                branch.Version);
    }

    internal static bool TryStatus(string value, out BranchStatus status)
        => Enum.TryParse(value, true, out status) && Enum.IsDefined(status);

    internal static string Status(BranchStatus status)
        => char.ToLowerInvariant(status.ToString()[0]) + status.ToString()[1..];
}

public sealed record BranchVersionedDetail(BranchDetailResponse Branch, long Version);
