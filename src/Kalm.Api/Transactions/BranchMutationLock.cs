using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Transactions;

internal static class BranchMutationLock
{
    public static async Task AcquireAsync(
        OrganizationDbContext organization,
        Guid organizationId,
        IEnumerable<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        foreach (Guid branchId in branchIds.Distinct().Order())
        {
            string lockKey = $"branch-administration:{organizationId:D}:{branchId:D}";
            await organization.Database.ExecuteSqlInterpolatedAsync(
                $"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);
        }
    }
}
