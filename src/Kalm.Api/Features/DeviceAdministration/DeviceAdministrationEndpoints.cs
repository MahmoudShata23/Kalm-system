using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Transactions;
using Kalm.Organization;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Kalm.Api.Features.DeviceAdministration;

public static class DeviceAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapDeviceAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder management = endpoints.MapGroup("/api/v1/management/devices").WithTags("Device Administration").RequireAuthorization(KalmPolicies.DeviceAdministration);
        management.MapGet("", ListAsync).WithName("ListManagementDevices").Produces<DeviceListResponse>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(422);
        management.MapGet("/options", OptionsAsync).WithName("GetManagementDeviceOptions").Produces<DeviceOptionsResponse>().ProducesProblem(401).ProducesProblem(403);
        management.MapGet("/{deviceId:guid}", GetAsync).WithName("GetManagementDevice").Produces<DeviceDetailResponse>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        management.MapPost("", CreateAsync).WithName("CreateManagementDevice").Produces<DeviceDetailResponse>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(422);
        management.MapPut("/{deviceId:guid}", UpdateAsync).WithName("UpdateManagementDevice").Produces<DeviceDetailResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(412).ProducesProblem(422).ProducesProblem(428);
        management.MapPost("/{deviceId:guid}/pairing-challenge", ChallengeAsync).WithName("CreateDevicePairingChallenge").Produces<DevicePairingChallengeResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        management.MapPost("/{deviceId:guid}/revoke", RevokeAsync).WithName("RevokeManagementDevice").Produces(204).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(412).ProducesProblem(428);

        endpoints.MapPost("/api/v1/devices/pair", PairAsync).AllowAnonymous().WithTags("Device Authentication").WithName("PairDevice").Produces(204).ProducesProblem(401).ProducesProblem(422);
        endpoints.MapGet("/api/v1/devices/eligible-users", EligibleUsersAsync).AllowAnonymous().WithTags("Device Authentication").WithName("GetEligibleDeviceUsers").Produces<EligibleEmployeesResponse>().ProducesProblem(401);
        return endpoints;
    }

    private static async Task<IResult> EligibleUsersAsync(HttpContext context, DeviceCredentialResolver credentials, DeviceAuthenticationQueries queries, CancellationToken token)
    {
        DeviceRequestContext? device = await credentials.ResolveAsync(context, token);
        return device is null ? Problem("device.credential_invalid") : Results.Ok(await queries.EligibleAsync(device, token));
    }

    private static Task<DeviceListResponse> ListAsync(HttpContext context, DeviceAdministrationQueries queries, string status = "all", Guid? branchId = null, string? search = null, int page = 1, int pageSize = 25, CancellationToken token = default)
        => queries.ListAsync(Session(context).OrganizationId, status, branchId, search, page, pageSize, token);
    private static Task<DeviceOptionsResponse> OptionsAsync(HttpContext context, DeviceAdministrationQueries queries, CancellationToken token)
        => queries.OptionsAsync(Session(context).OrganizationId, token);
    private static async Task<IResult> GetAsync(Guid deviceId, HttpContext context, DeviceAdministrationQueries queries, CancellationToken token)
    {
        DeviceVersionedDetail? detail = await queries.GetAsync(Session(context).OrganizationId, deviceId, token);
        return detail is null ? Problem("device.not_found") : Versioned(context, detail);
    }
    private static async Task<IResult> CreateAsync(DeviceCreateRequest request, [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf, HttpContext context, IAntiforgery antiforgery, DeviceAdministrationAuditTransactionCoordinator coordinator, DeviceAdministrationQueries queries, CancellationToken token)
    {
        _ = csrf; if (!await ValidCsrf(context, antiforgery)) return Problem("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        DeviceOperationResult result = await coordinator.CreateAsync(session.OrganizationId, session.UserId, request, context.TraceIdentifier, token);
        if (!result.Succeeded) return Problem(result.ErrorCode!, result.CurrentVersion);
        DeviceVersionedDetail detail = (await queries.GetAsync(session.OrganizationId, result.DeviceId, token))!;
        SetVersion(context, detail.Version); context.Response.Headers.Location = $"/api/v1/management/devices/{result.DeviceId:D}"; return Results.Created(context.Response.Headers.Location, detail.Device);
    }
    private static async Task<IResult> UpdateAsync(Guid deviceId, DeviceUpdateRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf, HttpContext context, IAntiforgery antiforgery, DeviceAdministrationAuditTransactionCoordinator coordinator, DeviceAdministrationQueries queries, CancellationToken token)
    {
        _ = csrf; IResult? condition = ParseEtag(ifMatch, out long version); if (condition is not null) return condition; if (!await ValidCsrf(context, antiforgery)) return Problem("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context); DeviceOperationResult result = await coordinator.UpdateAsync(session.OrganizationId, session.UserId, deviceId, version, request, context.TraceIdentifier, token);
        if (!result.Succeeded) return Problem(result.ErrorCode!, result.CurrentVersion);
        return Versioned(context, (await queries.GetAsync(session.OrganizationId, deviceId, token))!);
    }
    private static async Task<IResult> ChallengeAsync(Guid deviceId, [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf, HttpContext context, IAntiforgery antiforgery, DeviceAdministrationAuditTransactionCoordinator coordinator, CancellationToken token)
    {
        _ = csrf; if (!await ValidCsrf(context, antiforgery)) return Problem("auth.csrf_invalid"); ManagementSessionSnapshot session = Session(context);
        DeviceChallengeResult result = await coordinator.CreateChallengeAsync(session.OrganizationId, session.UserId, deviceId, context.TraceIdentifier, token);
        if (!result.Succeeded) return Problem(result.ErrorCode!);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        return Results.Ok(new DevicePairingChallengeResponse(result.DeviceId, result.Challenge!, result.ExpiresAtUtc!.Value));
    }
    private static async Task<IResult> RevokeAsync(Guid deviceId, [FromHeader(Name = "If-Match")] string? ifMatch, [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf, HttpContext context, IAntiforgery antiforgery, DeviceAdministrationAuditTransactionCoordinator coordinator, CancellationToken token)
    {
        _ = csrf; IResult? condition = ParseEtag(ifMatch, out long version); if (condition is not null) return condition; if (!await ValidCsrf(context, antiforgery)) return Problem("auth.csrf_invalid"); ManagementSessionSnapshot session = Session(context);
        DeviceOperationResult result = await coordinator.RevokeAsync(session.OrganizationId, session.UserId, deviceId, version, context.TraceIdentifier, token);
        if (!result.Succeeded) return Problem(result.ErrorCode!, result.CurrentVersion); SetVersion(context, result.Version); return Results.NoContent();
    }
    private static async Task<IResult> PairAsync(DevicePairRequest request, HttpContext context, DeviceAdministrationAuditTransactionCoordinator coordinator, IClock clock, CancellationToken token)
    {
        if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.PairingChallenge) || request.PairingChallenge.Length > 128) return Problem("device.pairing_failed");
        DevicePairResult result = await coordinator.PairAsync(request.DeviceId, request.PairingChallenge, context.TraceIdentifier, token);
        if (!result.Succeeded) return Problem("device.pairing_failed"); DeviceCredentialResolver.SetCookie(context, result.Credential!, clock.UtcNow); return Results.NoContent();
    }
    private static ManagementSessionSnapshot Session(HttpContext context) => context.Items[ManagementAuthenticationConstants.SessionItemKey] as ManagementSessionSnapshot ?? throw new InvalidOperationException("Authoritative management session unavailable.");
    private static IResult Versioned(HttpContext context, DeviceVersionedDetail detail) { SetVersion(context, detail.Version); return Results.Ok(detail.Device); }
    private static void SetVersion(HttpContext context, long version) { context.Response.Headers.ETag = $"\"{version}\""; context.Response.Headers.CacheControl = "no-store"; }
    private static IResult? ParseEtag(string? value, out long version)
    {
        version = 0; if (string.IsNullOrWhiteSpace(value)) return Problem("device.precondition_required");
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"' || value.Contains(',') || value == "*" || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase) || !long.TryParse(value.AsSpan(1, value.Length - 2), out version) || version < 1 || value != $"\"{version}\"") return Problem("device.invalid_precondition"); return null;
    }
    private static async Task<bool> ValidCsrf(HttpContext context, IAntiforgery antiforgery) { try { await antiforgery.ValidateRequestAsync(context); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static IResult Problem(string code, long? current = null)
    {
        (int status, string title, string detail) = code switch
        {
            "device.not_found" => (404, "Device not found", "The requested device was not found."),
            "device.revoked" => (409, "Device revoked", "The revoked device cannot be changed."),
            "device.branch_invalid" => (422, "Branch invalid", "Select an active branch from this organization."),
            "device.validation_failed" => (422, "Device invalid", "The device request is invalid."),
            "device.precondition_required" => (428, "Precondition required", "A current strong If-Match value is required."),
            "device.invalid_precondition" => (400, "Invalid precondition", "If-Match must contain one strong quoted device version."),
            "device.concurrency_conflict" => (412, "Device changed", "The device was changed by another request."),
            "device.pairing_failed" => (401, "Pairing failed", "The pairing challenge is invalid or unavailable."),
            "device.credential_invalid" => (401, "Device unavailable", "A valid paired device is required."),
            "auth.csrf_invalid" => (400, "Antiforgery validation failed", "The antiforgery token is missing or invalid."),
            _ => (500, "Device administration failed", "The operation could not be completed.")
        };
        Dictionary<string, object?> extensions = new() { ["code"] = code }; if (current is not null) extensions["currentEtag"] = $"\"{current}\"";
        return Results.Problem(statusCode: status, title: title, detail: detail, extensions: extensions);
    }
}
