namespace Kalm.Identity;

public sealed record CurrentUserResponse(bool IsAuthenticated, string? DisplayName, IReadOnlyCollection<string> Permissions);
