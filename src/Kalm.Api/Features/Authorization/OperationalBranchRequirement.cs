using Microsoft.AspNetCore.Authorization;

namespace Kalm.Api.Features.Authorization;

public sealed record OperationalBranchRequirement : IAuthorizationRequirement;

public sealed record OperationalBranchResource(Guid OrganizationId, Guid BranchId);
