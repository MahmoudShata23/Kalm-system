namespace Kalm.Api.Features.Authentication;

public sealed class PinLoginRequest
{
    public Guid UserId { get; init; }
    public string Pin { get; set; } = string.Empty;
}
public sealed record EligibleEmployeeResponse(Guid Id, string DisplayName);
public sealed record EligibleEmployeesResponse(IReadOnlyCollection<EligibleEmployeeResponse> Items);
public sealed record PinLoginResponse(string DisplayName, string PreferredLanguage);
