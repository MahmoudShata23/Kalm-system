using Microsoft.AspNetCore.Authorization;

namespace Kalm.Api.Features.Authorization;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
