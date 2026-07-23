namespace Kalm.Api.Features.AuditViewer;

internal static class AuditViewerProblemDetails
{
    public static IResult Create(string code)
    {
        (int status, string title, string detail) = code switch
        {
            "audit.not_found" => (404, "Audit record not found", "The requested audit record was not found."),
            "audit.invalid_cursor" => (400, "Audit cursor invalid", "The audit cursor is invalid or no longer matches the authorized query."),
            "audit.invalid_filter" => (422, "Audit filter invalid", "The audit filter parameters are invalid."),
            "audit.interval_required" => (422, "Audit interval required", "A bounded UTC audit interval is required."),
            "audit.interval_too_large" => (422, "Audit interval too large", "The audit interval cannot exceed 90 days."),
            _ => (500, "Audit viewer failed", "The audit request could not be completed.")
        };
        return Results.Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
