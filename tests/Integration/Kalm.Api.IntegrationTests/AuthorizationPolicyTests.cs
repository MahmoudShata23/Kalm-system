using System.Security.Claims;
using Kalm.Api.Configuration;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kalm.Api.IntegrationTests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task ManagementPolicy_UsesAuthorizationServiceCompiledPermissionAndServerSnapshot()
    {
        Guid userId = Guid.NewGuid();
        Guid organizationId = Guid.NewGuid();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = CreateSession(
            userId, organizationId, [PermissionCodes.ManagementAccess], null);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("subject", userId.ToString("D"))], "test"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization(KalmPolicies.AddKalmAuthorization);
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult approved = await authorization.AuthorizeAsync(principal, null, KalmPolicies.ManagementAccess);
        Assert.True(approved.Succeeded);

        AuthorizationResult unknown = await authorization.AuthorizeAsync(
            principal, null, new PermissionRequirement("unknown.permission"));
        Assert.False(unknown.Succeeded);

        accessor.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = CreateSession(
            userId, organizationId, [], null);
        Assert.False((await authorization.AuthorizeAsync(principal, null, KalmPolicies.ManagementAccess)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), null, KalmPolicies.ManagementAccess)).Succeeded);
    }

    [Fact]
    public async Task RoleAdministrationPolicy_RequiresManagementAccessAndRolesManageTogether()
    {
        Guid userId = Guid.NewGuid();
        Guid organizationId = Guid.NewGuid();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("subject", userId.ToString("D"))], "test"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization(KalmPolicies.AddKalmAuthorization);
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        accessor.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = CreateSession(
            userId, organizationId, [PermissionCodes.ManagementAccess], null);
        Assert.False((await authorization.AuthorizeAsync(principal, null, KalmPolicies.RoleAdministration)).Succeeded);

        accessor.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = CreateSession(
            userId, organizationId, [PermissionCodes.RolesManage], null);
        Assert.False((await authorization.AuthorizeAsync(principal, null, KalmPolicies.RoleAdministration)).Succeeded);

        accessor.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = CreateSession(
            userId, organizationId, [PermissionCodes.ManagementAccess, PermissionCodes.RolesManage], null);
        Assert.True((await authorization.AuthorizeAsync(principal, null, KalmPolicies.RoleAdministration)).Succeeded);
    }

    [Fact]
    public async Task OperationalBranchHandler_RequiresMatchingOrganizationAndOperationalBranch()
    {
        Guid userId = Guid.NewGuid();
        Guid organizationId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        var branchAccess = new EffectiveBranchAccessSnapshot(
            "assignedBranches", [branchId], new HashSet<Guid> { branchId });
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = CreateSession(
            userId, organizationId, [PermissionCodes.ManagementAccess], branchAccess);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("subject", userId.ToString("D"))], "test"));
        var requirement = new OperationalBranchRequirement();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddSingleton<IAuthorizationHandler, OperationalBranchAuthorizationHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult allowed = await authorization.AuthorizeAsync(
            principal, new OperationalBranchResource(organizationId, branchId), requirement);
        Assert.True(allowed.Succeeded);

        AuthorizationResult wrongOrganization = await authorization.AuthorizeAsync(
            principal, new OperationalBranchResource(Guid.NewGuid(), branchId), requirement);
        Assert.False(wrongOrganization.Succeeded);

        AuthorizationResult inactiveBranch = await authorization.AuthorizeAsync(
            principal, new OperationalBranchResource(organizationId, Guid.NewGuid()), requirement);
        Assert.False(inactiveBranch.Succeeded);

        AuthorizationResult anonymous = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            new OperationalBranchResource(organizationId, branchId),
            requirement);
        Assert.False(anonymous.Succeeded);
    }

    [Fact]
    public async Task CookieChallengeAndForbiddenResponsesUse401And403WithoutRedirects()
    {
        var events = new ManagementCookieEvents(
            null!, null!, Options.Create(new ManagementAuthenticationOptions()), null!);
        var scheme = new AuthenticationScheme("test", "test", typeof(CookieAuthenticationHandler));
        var options = new CookieAuthenticationOptions();

        var unauthenticated = new DefaultHttpContext();
        var login = new RedirectContext<CookieAuthenticationOptions>(
            unauthenticated, scheme, options, new AuthenticationProperties(), "/management/login");
        await events.RedirectToLogin(login);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthenticated.Response.StatusCode);
        Assert.False(unauthenticated.Response.Headers.ContainsKey("Location"));

        var authenticatedWithoutPermission = new DefaultHttpContext();
        var denied = new RedirectContext<CookieAuthenticationOptions>(
            authenticatedWithoutPermission, scheme, options, new AuthenticationProperties(), "/management/access-denied");
        await events.RedirectToAccessDenied(denied);
        Assert.Equal(StatusCodes.Status403Forbidden, authenticatedWithoutPermission.Response.StatusCode);
        Assert.False(authenticatedWithoutPermission.Response.Headers.ContainsKey("Location"));
    }

    private static ManagementSessionSnapshot CreateSession(
        Guid userId,
        Guid organizationId,
        IReadOnlyCollection<string> permissions,
        EffectiveBranchAccessSnapshot? branchAccess)
    {
        DateTimeOffset now = new(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
        return new ManagementSessionSnapshot(
            Guid.NewGuid(), userId, organizationId, "manager", "Manager", "en",
            now.AddMinutes(20), now.AddHours(8), now,
            new EffectiveAuthorizationSnapshot(
                userId, organizationId, new HashSet<string>(permissions, StringComparer.Ordinal), branchAccess));
    }
}
