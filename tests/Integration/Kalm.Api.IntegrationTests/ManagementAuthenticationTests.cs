using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Persistence;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Domain;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Security;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Api.IntegrationTests;

public sealed class ManagementAuthenticationTests
{
    private const string Password = "a secure management phrase ☕";
    private static readonly DateTimeOffset InitialTime = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CsrfLoginMeAndLogout_UseSecureCookiesAndServerSession()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        var clock = new MutableClock(InitialTime);
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, clock, database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);

        (string csrf, HttpResponseMessage csrfResponse) = await GetCsrfAsync(client);
        using (csrfResponse)
        {
            Assert.Equal("no-store", csrfResponse.Headers.CacheControl?.ToString());
            Assert.Contains("no-cache", csrfResponse.Headers.Pragma.Select(value => value.Name));
            string antiforgeryCookie = Assert.Single(csrfResponse.Headers.GetValues("Set-Cookie"));
            Assert.Contains("__Host-Kalm.Antiforgery=", antiforgeryCookie, StringComparison.Ordinal);
            Assert.Contains("secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        }

        using HttpResponseMessage login = await LoginAsync(client, csrf, Password);
        login.EnsureSuccessStatusCode();
        string authenticationCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith(ManagementAuthenticationConstants.CookieName, StringComparison.Ordinal));
        Assert.Contains("secure", authenticationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", authenticationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", authenticationCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", authenticationCookie, StringComparison.OrdinalIgnoreCase);

        CookieAuthenticationOptions cookieOptions = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ManagementAuthenticationConstants.Scheme);
        string cookieValue = authenticationCookie.Split(';')[0].Split('=', 2)[1];
        var ticket = cookieOptions.TicketDataFormat.Unprotect(cookieValue);
        Assert.NotNull(ticket);
        Assert.Equal(
            [ManagementAuthenticationConstants.SchemeVersionClaim, ManagementAuthenticationConstants.SessionIdClaim],
            ticket.Principal.Claims.Select(claim => claim.Type).OrderBy(value => value).ToArray());

        using HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
        me.EnsureSuccessStatusCode();
        using JsonDocument meBody = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.True(meBody.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("manager", meBody.RootElement.GetProperty("username").GetString());
        Assert.Empty(meBody.RootElement.GetProperty("permissions").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, meBody.RootElement.GetProperty("branchAccess").ValueKind);

        string logoutCsrf = (await GetCsrfAsync(client)).Token;
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add("X-XSRF-TOKEN", logoutCsrf);
        using HttpResponseMessage logout = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        await using var identity = database.CreateIdentityContext();
        UserSession session = await identity.UserSessions.SingleAsync();
        Assert.Equal(SessionRevocationReason.Logout, session.RevocationReason);
        await using var audit = database.CreateAuditContext();
        Assert.Contains(await audit.AuditEntries.Select(entry => entry.Action).ToListAsync(), action => action == Kalm.Audit.Domain.AuditAction.ManagementLogoutSucceeded);
    }

    [Fact]
    public async Task LoginFailures_LockOnFifthAttemptAndReturnSameExternalProblem()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            string csrf = (await GetCsrfAsync(client)).Token;
            using HttpResponseMessage response = await LoginAsync(client, csrf, "an incorrect password phrase");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal("auth.invalid_credentials", body.RootElement.GetProperty("code").GetString());
        }

        await using var identity = database.CreateIdentityContext();
        PasswordCredential credential = await identity.PasswordCredentials.SingleAsync();
        Assert.Equal(5, credential.FailedAttemptCount);
        Assert.Equal(InitialTime.AddMinutes(15), credential.LockedUntilUtc);
        Assert.Equal(5, await identity.LoginAttempts.CountAsync());
    }

    [Fact]
    public async Task UnknownSuspendedAndInactiveCredentialLogins_ReturnTheSameExternalProblem()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        string csrf = (await GetCsrfAsync(client)).Token;

        using HttpResponseMessage unknown = await LoginAsync(client, csrf, Password, identifier: "unknown-user");
        await database.ExecuteAsync("update identity.users set status = 'Suspended'");
        using HttpResponseMessage suspended = await LoginAsync(client, csrf, Password);
        await database.ExecuteAsync("update identity.users set status = 'Active'; update identity.password_credentials set status = 'Disabled'");
        using HttpResponseMessage disabled = await LoginAsync(client, csrf, Password);

        AuthenticationProblem unknownProblem = await ReadAuthenticationProblemAsync(unknown);
        Assert.Equal(unknownProblem, await ReadAuthenticationProblemAsync(suspended));
        Assert.Equal(unknownProblem, await ReadAuthenticationProblemAsync(disabled));
        Assert.Equal(new AuthenticationProblem(401, "auth.invalid_credentials", "The identifier or password is invalid, or the account is unavailable."), unknownProblem);
    }

    [Fact]
    public async Task InvalidCsrf_PreventsLoginMutation()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);

        using HttpResponseMessage response = await LoginAsync(client, "invalid", Password);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var identity = database.CreateIdentityContext();
        Assert.Empty(await identity.LoginAttempts.ToListAsync());
        Assert.Empty(await identity.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task ExpiredSession_IsRejectedWithoutExtendingExpiry()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        var clock = new MutableClock(InitialTime);
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, clock, database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        using HttpResponseMessage login = await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password);
        login.EnsureSuccessStatusCode();

        clock.UtcNow = InitialTime.AddMinutes(21);
        using HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
        using JsonDocument body = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.False(body.RootElement.GetProperty("isAuthenticated").GetBoolean());

        await using var identity = database.CreateIdentityContext();
        UserSession session = await identity.UserSessions.SingleAsync();
        Assert.Equal(InitialTime.AddMinutes(20), session.InactivityExpiresAtUtc);
    }

    [Fact]
    public async Task AbsoluteSessionExpiry_IsRejectedAtEightHoursEvenWhenIdleExpiryWouldRemainValid()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        var clock = new MutableClock(InitialTime);
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, clock, database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        using HttpResponseMessage login = await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password);
        login.EnsureSuccessStatusCode();

        await database.ExecuteAsync(
            $"update identity.user_sessions set inactivity_expires_at_utc = absolute_expires_at_utc where absolute_expires_at_utc = '{InitialTime.AddHours(8):O}'");
        clock.UtcNow = InitialTime.AddHours(8).AddMinutes(-1);
        using HttpResponseMessage beforeExpiry = await client.GetAsync("/api/v1/auth/me");
        using JsonDocument beforeBody = await JsonDocument.ParseAsync(await beforeExpiry.Content.ReadAsStreamAsync());
        Assert.True(beforeBody.RootElement.GetProperty("isAuthenticated").GetBoolean());

        clock.UtcNow = InitialTime.AddHours(8);
        using HttpResponseMessage atExpiry = await client.GetAsync("/api/v1/auth/me");
        using JsonDocument atExpiryBody = await JsonDocument.ParseAsync(await atExpiry.Content.ReadAsStreamAsync());
        Assert.False(atExpiryBody.RootElement.GetProperty("isAuthenticated").GetBoolean());

        await using var identity = database.CreateIdentityContext();
        UserSession session = await identity.UserSessions.SingleAsync();
        Assert.Equal(InitialTime.AddHours(8), session.AbsoluteExpiresAtUtc);
        Assert.Equal(session.AbsoluteExpiresAtUtc, session.InactivityExpiresAtUtc);
    }

    [Fact]
    public async Task UntrustedForwardedForCannotBypassPerSourceLoginRateLimit()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        using WebApplicationFactory<Program> factory = CreateFactory(
            database.ConnectionString,
            new MutableClock(InitialTime),
            database.KeyPath,
            new Dictionary<string, string?> { ["ManagementAuthentication:LoginRequestsPerMinute"] = "2" });
        using HttpClient client = CreateHttpsClient(factory);
        string csrf = (await GetCsrfAsync(client)).Token;

        using HttpResponseMessage first = await LoginAsync(client, csrf, "an incorrect password phrase", "198.51.100.1");
        using HttpResponseMessage second = await LoginAsync(client, csrf, "an incorrect password phrase", "198.51.100.2");
        using HttpResponseMessage limited = await LoginAsync(client, csrf, "an incorrect password phrase", "198.51.100.3");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        await using var identity = database.CreateIdentityContext();
        Assert.Equal(2, await identity.LoginAttempts.CountAsync());
    }

    [Fact]
    public async Task AuditInsertFailure_RollsBackSuccessfulLoginMutation()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        await database.ExecuteAsync(
            "create function audit.reject_auth_audit() returns trigger language plpgsql as $$ begin raise exception 'forced audit failure'; end; $$; create trigger trg_reject_auth_audit before insert on audit.audit_logs for each row execute function audit.reject_auth_audit();");
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);

        using HttpResponseMessage response = await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            && cookies.Any(value => value.StartsWith(ManagementAuthenticationConstants.CookieName, StringComparison.Ordinal)));
        await using var identity = database.CreateIdentityContext();
        Assert.Empty(await identity.UserSessions.ToListAsync());
        Assert.Empty(await identity.LoginAttempts.ToListAsync());
    }

    [Fact]
    public async Task AuditInsertFailure_RollsBackLogoutAndKeepsCurrentCookie()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        using HttpResponseMessage login = await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password);
        login.EnsureSuccessStatusCode();
        await database.ExecuteAsync(
            "create function audit.reject_logout_audit() returns trigger language plpgsql as $$ begin if new.action = 'ManagementLogoutSucceeded' then raise exception 'forced logout audit failure'; end if; return new; end; $$; create trigger trg_reject_logout_audit before insert on audit.audit_logs for each row execute function audit.reject_logout_audit();");

        string csrf = (await GetCsrfAsync(client)).Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using (var identity = database.CreateIdentityContext())
        {
            Assert.Null((await identity.UserSessions.SingleAsync()).RevokedAtUtc);
        }

        await using (var audit = database.CreateAuditContext())
        {
            Assert.DoesNotContain(
                await audit.AuditEntries.Select(entry => entry.Action).ToListAsync(),
                action => action == Kalm.Audit.Domain.AuditAction.ManagementLogoutSucceeded);
        }

        using HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
        using JsonDocument body = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.True(body.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task ProvisionedAssignedAuthorization_IsResolvedAndSortedOnEveryMeRequest()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        Guid branchId = await database.ProvisionAuthorizationAsync(BranchAccessScope.AssignedBranches, secondBranch: false);
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        using HttpResponseMessage login = await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password);
        login.EnsureSuccessStatusCode();

        using HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
        using JsonDocument body = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.Equal(
            new[] { PermissionCodes.ManagementAccess, PermissionCodes.UsersView }.OrderBy(code => code),
            body.RootElement.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
        JsonElement branchAccess = body.RootElement.GetProperty("branchAccess");
        Assert.Equal("assignedBranches", branchAccess.GetProperty("scope").GetString());
        Assert.Equal(branchId, branchAccess.GetProperty("branchIds")[0].GetGuid());
        Assert.Equal(branchId, branchAccess.GetProperty("operationalBranchIds")[0].GetGuid());

        await using (OrganizationDbContext organization = database.CreateOrganizationContext())
        {
            (await organization.Branches.SingleAsync()).ChangeStatus(
                BranchStatus.Setup, InitialTime.AddMinutes(2));
            await organization.SaveChangesAsync();
        }

        using JsonDocument setupBranchBody = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me"));
        JsonElement setupBranchAccess = setupBranchBody.RootElement.GetProperty("branchAccess");
        Assert.Equal(branchId, setupBranchAccess.GetProperty("branchIds")[0].GetGuid());
        Assert.Empty(setupBranchAccess.GetProperty("operationalBranchIds").EnumerateArray());
    }

    [Fact]
    public async Task EffectivePermissions_UnionMultipleRolesDeduplicateAndIgnoreRetiredPermissions()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        await database.ProvisionAuthorizationAsync(BranchAccessScope.AssignedBranches, secondBranch: false);
        await using (IdentityDbContext identity = database.CreateIdentityContext())
        {
            User user = await identity.Users.SingleAsync();
            var secondRole = Role.Create(
                Guid.NewGuid(), user.OrganizationId, new RoleName("Display names do not authorize"),
                "ignored.system-key", InitialTime);
            var archivedRole = Role.Create(
                Guid.NewGuid(), user.OrganizationId, new RoleName("Archived role name"), null, InitialTime);
            identity.Roles.AddRange(secondRole, archivedRole);
            Permission[] secondRolePermissions = await identity.Permissions
                .Where(permission => permission.Code == PermissionCodes.ManagementAccess
                    || permission.Code == PermissionCodes.BackupsManage
                    || permission.Code == PermissionCodes.RolesManage
                    || permission.Code == PermissionCodes.UsersManage)
                .ToArrayAsync();
            foreach (Permission permission in secondRolePermissions)
            {
                RolePermission grant = RolePermission.Grant(Guid.NewGuid(), secondRole.Id, permission.Id, InitialTime);
                if (permission.Code == PermissionCodes.BackupsManage)
                {
                    grant.Revoke(InitialTime.AddMinutes(1));
                }

                identity.RolePermissions.Add(grant);
            }

            Permission auditPermission = await identity.Permissions.SingleAsync(
                permission => permission.Code == PermissionCodes.AuditView);
            identity.RolePermissions.Add(RolePermission.Grant(
                Guid.NewGuid(), archivedRole.Id, auditPermission.Id, InitialTime));
            archivedRole.Archive(InitialTime.AddMinutes(1));
            var unknownPermission = Permission.Create(
                Guid.NewGuid(), new PermissionCode("unknown.permission"), InitialTime);
            identity.Permissions.Add(unknownPermission);
            identity.RolePermissions.Add(RolePermission.Grant(
                Guid.NewGuid(), secondRole.Id, unknownPermission.Id, InitialTime));
            identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(
                Guid.NewGuid(), user.OrganizationId, user.Id, secondRole.Id, InitialTime));
            identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(
                Guid.NewGuid(), user.OrganizationId, user.Id, archivedRole.Id, InitialTime));
            secondRolePermissions.Single(permission => permission.Code == PermissionCodes.UsersManage)
                .Retire(InitialTime.AddMinutes(1));
            user.AdvanceAuthorizationVersion(InitialTime.AddMinutes(1));
            await identity.SaveChangesAsync();
        }

        using WebApplicationFactory<Program> factory = CreateFactory(
            database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        (await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password)).EnsureSuccessStatusCode();

        using JsonDocument body = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me"));
        Assert.Equal(
            new[] { PermissionCodes.ManagementAccess, PermissionCodes.RolesManage, PermissionCodes.UsersView }
                .OrderBy(code => code, StringComparer.Ordinal),
            body.RootElement.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task AllOrganizationBranches_ListsEveryBranchButOnlyActiveBranchesAsOperational()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        Guid activeBranchId = await database.ProvisionAuthorizationAsync(BranchAccessScope.AllOrganizationBranches, secondBranch: true);
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        (await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password)).EnsureSuccessStatusCode();

        using JsonDocument body = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me"));
        JsonElement branchAccess = body.RootElement.GetProperty("branchAccess");
        Assert.Equal("allOrganizationBranches", branchAccess.GetProperty("scope").GetString());
        Assert.Equal(2, branchAccess.GetProperty("branchIds").GetArrayLength());
        Assert.Equal(activeBranchId, Assert.Single(branchAccess.GetProperty("operationalBranchIds").EnumerateArray()).GetGuid());

        await using (OrganizationDbContext organization = database.CreateOrganizationContext())
        {
            (await organization.Organizations.SingleAsync()).ChangeStatus(
                OrganizationStatus.Suspended, InitialTime.AddMinutes(2));
            await organization.SaveChangesAsync();
        }

        using JsonDocument inactiveOrganizationBody = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me"));
        Assert.Empty(inactiveOrganizationBody.RootElement.GetProperty("branchAccess")
            .GetProperty("operationalBranchIds").EnumerateArray());
    }

    [Fact]
    public async Task RemovingPermission_TakesEffectNextRequestWithoutRevokingAuthenticationSession()
    {
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        await database.ProvisionAuthorizationAsync(BranchAccessScope.AssignedBranches, secondBranch: false);
        using WebApplicationFactory<Program> factory = CreateFactory(database.ConnectionString, new MutableClock(InitialTime), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        (await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password)).EnsureSuccessStatusCode();

        await database.RemovePermissionAsync(PermissionCodes.UsersView);

        using JsonDocument body = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/me"));
        Assert.True(body.RootElement.GetProperty("isAuthenticated").GetBoolean());
        string[] permissions = body.RootElement.GetProperty("permissions").EnumerateArray().Select(element => element.GetString()!).ToArray();
        Assert.DoesNotContain(PermissionCodes.UsersView, permissions);
        Assert.Contains(PermissionCodes.ManagementAccess, permissions);
        Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("branchAccess").ValueKind);
        await using var identity = database.CreateIdentityContext();
        Assert.Null((await identity.UserSessions.SingleAsync()).RevokedAtUtc);
        Assert.Equal(3, (await identity.Users.SingleAsync()).AuthorizationVersion);
    }

    [Fact]
    public async Task PasswordAdministration_ReturnsNoContentAndAuditsOnlySafeMetadata()
    {
        const string sensitiveEmail = "manager.private@kalm.local";
        const string replacementPassword = "a replacement management phrase";
        await using var database = await AuthDatabase.CreateAsync();
        await database.MigrateAndSeedAsync();
        await database.ProvisionAuthorizationAsync(
            BranchAccessScope.AssignedBranches, secondBranch: false, grantUserManage: true);

        Guid userId;
        long version;
        await using (IdentityDbContext identity = database.CreateIdentityContext())
        {
            User user = await identity.Users.SingleAsync();
            user.UpdateProfile(
                new Username(user.Username),
                new EmailAddress(sensitiveEmail),
                new DisplayName(user.DisplayName),
                user.PreferredLanguage,
                authorizationChanged: false,
                InitialTime.AddMinutes(2));
            await identity.SaveChangesAsync();
            userId = user.Id;
            version = user.Version;
        }

        using WebApplicationFactory<Program> factory = CreateFactory(
            database.ConnectionString, new MutableClock(InitialTime.AddMinutes(2)), database.KeyPath);
        using HttpClient client = CreateHttpsClient(factory);
        (await LoginAsync(client, (await GetCsrfAsync(client)).Token, Password)).EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/management/users/{userId:D}/password")
        {
            Content = JsonContent.Create(new { password = replacementPassword })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", (await GetCsrfAsync(client)).Token);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Equal($"\"{version + 1}\"", response.Headers.ETag?.Tag);

        await using AuditDbContext audit = database.CreateAuditContext();
        var passwordAudit = await audit.AuditEntries.SingleAsync(
            entry => entry.EntityId == userId && entry.Action == Kalm.Audit.Domain.AuditAction.UserPasswordReset);
        string auditPayload = (passwordAudit.BeforeJson ?? string.Empty) + (passwordAudit.AfterJson ?? string.Empty);
        Assert.DoesNotContain(sensitiveEmail, auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(replacementPassword, auditPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salt", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionId", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ticket", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("request", auditPayload, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        MutableClock clock,
        string keyPath,
        IReadOnlyDictionary<string, string?>? overrides = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["PasswordHashing:Iterations"] = PasswordHashingOptions.MinimumIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["SecurityFingerprint:ActiveKeyVersion"] = "1",
                    ["SecurityFingerprint:ActiveKeyBase64"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
                    ["DataProtection:KeyRingPath"] = keyPath
                };
                if (overrides is not null)
                {
                    foreach ((string key, string? value) in overrides)
                    {
                        values[key] = value;
                    }
                }

                configuration.AddInMemoryCollection(values);
            });
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
            });
        });

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<(string Token, HttpResponseMessage Response)> GetCsrfAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfPayload>();
        return (body?.RequestToken ?? throw new InvalidOperationException("Missing request token."), response);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string csrf,
        string password,
        string? forwardedFor = null,
        string identifier = "manager")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { identifier, password })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request);
    }

    private static async Task<AuthenticationProblem> ReadAuthenticationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return new AuthenticationProblem(
            body.RootElement.GetProperty("status").GetInt32(),
            body.RootElement.GetProperty("code").GetString(),
            body.RootElement.GetProperty("detail").GetString());
    }

    private sealed record CsrfPayload(string RequestToken);

    private sealed record AuthenticationProblem(int Status, string? Code, string? Detail);

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class AuthDatabase : IAsyncDisposable
    {
        private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";
        private readonly string _databaseName;
        private readonly string _admin;

        private AuthDatabase(string databaseName, string admin, string connectionString, string keyPath)
        {
            _databaseName = databaseName;
            _admin = admin;
            ConnectionString = connectionString;
            KeyPath = keyPath;
        }

        public string ConnectionString { get; }
        public string KeyPath { get; }

        public static async Task<AuthDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string name = $"kalm_auth_{Guid.NewGuid():N}";
            var builder = new NpgsqlConnectionStringBuilder(admin) { Database = name };
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"create database \"{name}\"";
            await command.ExecuteNonQueryAsync();
            string keyPath = Path.Combine(Path.GetTempPath(), "kalm-auth-tests", name);
            return new AuthDatabase(name, admin, builder.ConnectionString, keyPath);
        }

        public IdentityDbContext CreateIdentityContext() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);
        public OrganizationDbContext CreateOrganizationContext() => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
        public AuditDbContext CreateAuditContext() => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);

        public async Task MigrateAndSeedAsync()
        {
            await using var platform = new KalmDbContext(new DbContextOptionsBuilder<KalmDbContext>().UseNpgsql(ConnectionString).Options);
            await using var organization = CreateOrganizationContext();
            await using var identity = CreateIdentityContext();
            await using var audit = CreateAuditContext();
            await platform.Database.MigrateAsync();
            await organization.Database.MigrateAsync();
            await identity.Database.MigrateAsync();
            await audit.Database.MigrateAsync();

            var organizationAggregate = OrganizationAggregate.Create(Guid.NewGuid(), new OrganizationName("Kalm", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), InitialTime);
            organization.Organizations.Add(organizationAggregate);
            await organization.SaveChangesAsync();
            var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = PasswordHashingOptions.MinimumIterations }));
            var user = User.Create(Guid.NewGuid(), organizationAggregate.Id, new Username("manager"), null, new DisplayName("Management User"), "en", InitialTime);
            var credential = PasswordCredential.Create(Guid.NewGuid(), user.Id, InitialTime);
            credential.CompleteSetup(hasher.Hash(Password), InitialTime);
            user.Activate(credential, InitialTime);
            identity.Users.Add(user);
            identity.PasswordCredentials.Add(credential);
            await identity.SaveChangesAsync();
        }

        public async Task<Guid> ProvisionAuthorizationAsync(
            BranchAccessScope scope,
            bool secondBranch,
            bool grantUserManage = false)
        {
            await using var identity = CreateIdentityContext();
            await using var organization = CreateOrganizationContext();
            User user = await identity.Users.SingleAsync();
            OrganizationAggregate organizationAggregate = await organization.Organizations.SingleAsync();
            organizationAggregate.ChangeStatus(OrganizationStatus.Active, InitialTime.AddMinutes(1));
            var activeBranch = Branch.Create(
                Guid.NewGuid(), organizationAggregate.Id, new OrganizationName("Cairo", 120), new BranchCode("CAI-01"),
                new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), InitialTime);
            activeBranch.ChangeStatus(BranchStatus.Active, InitialTime.AddMinutes(1));
            organization.Branches.Add(activeBranch);
            if (secondBranch)
            {
                var suspended = Branch.Create(
                    Guid.NewGuid(), organizationAggregate.Id, new OrganizationName("Second", 120), new BranchCode("CAI-02"),
                    new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), InitialTime);
                suspended.ChangeStatus(BranchStatus.Suspended, InitialTime.AddMinutes(1));
                organization.Branches.Add(suspended);
            }

            var role = Role.Create(Guid.NewGuid(), user.OrganizationId, new RoleName("Any display name"), null, InitialTime);
            identity.Roles.Add(role);
            Permission[] permissions = await identity.Permissions
                .Where(permission => permission.Code == PermissionCodes.ManagementAccess
                    || permission.Code == PermissionCodes.UsersView
                    || (grantUserManage && permission.Code == PermissionCodes.UsersManage))
                .ToArrayAsync();
            foreach (Permission permission in permissions)
            {
                identity.RolePermissions.Add(RolePermission.Grant(Guid.NewGuid(), role.Id, permission.Id, InitialTime));
            }

            identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(Guid.NewGuid(), user.OrganizationId, user.Id, role.Id, InitialTime));
            user.AdvanceAuthorizationVersion(InitialTime.AddMinutes(1));
            var access = UserBranchAccess.Create(Guid.NewGuid(), user.OrganizationId, user.Id, scope, InitialTime);
            organization.UserBranchAccesses.Add(access);
            if (scope == BranchAccessScope.AssignedBranches)
            {
                organization.UserBranchAssignments.Add(UserBranchAssignment.Assign(
                    Guid.NewGuid(), access.Id, user.OrganizationId, activeBranch.Id, InitialTime));
            }

            await organization.SaveChangesAsync();
            await identity.SaveChangesAsync();
            return activeBranch.Id;
        }

        public async Task RemovePermissionAsync(string permissionCode)
        {
            await using var identity = CreateIdentityContext();
            DateTimeOffset now = InitialTime.AddMinutes(2);
            RolePermission grant = await (
                from candidate in identity.RolePermissions
                join permission in identity.Permissions on candidate.PermissionId equals permission.Id
                where candidate.RevokedAtUtc == null && permission.Code == permissionCode
                select candidate).SingleAsync();
            grant.Revoke(now);

            (await identity.Users.SingleAsync()).AdvanceAuthorizationVersion(now);
            await identity.SaveChangesAsync();
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(_admin);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"drop database if exists \"{_databaseName}\" with (force)";
            await command.ExecuteNonQueryAsync();
            if (Directory.Exists(KeyPath))
            {
                Directory.Delete(KeyPath, recursive: true);
            }
        }
    }
}
